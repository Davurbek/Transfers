using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using Universal.Transfers.Api.Auth;
using Universal.Transfers.Api.Common.Middlewares;
using Universal.Transfers.Application;
using Universal.Transfers.Application.Auth;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Infrastructure;
using Universal.Transfers.Infrastructure.Common.Persistence;
using Universal.Transfers.Infrastructure.Messaging.Kafka;
using Universal.Transfers.Infrastructure.Seeding;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.SigningKey.Length < 32)
        throw new InvalidOperationException("JWT SigningKey must be at least 32 characters. Set Jwt:SigningKey via environment variable or user secrets.");
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            context.Configuration.GetValue<string>("Serilog:File:Path") ?? "logs/api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: context.Configuration.GetValue<int?>("Serilog:File:RetainedFileCountLimit") ?? 31,
            fileSizeLimitBytes: context.Configuration.GetValue<long?>("Serilog:File:FileSizeLimitBytes") ?? 10L * 1024 * 1024,
            rollOnFileSizeLimit: true));

    var demoPassword = builder.Configuration.GetValue<string>("SeedData:DemoPassword")
        ?? throw new InvalidOperationException("SeedData:DemoPassword is not configured. Set it via environment variable or user secrets.");

    const string CorsPolicy = "dashboard-ui";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured.");

    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApplicationServices();

    builder.Services.AddSingleton<ICommandPublisher, KafkaCommandPublisher>();

    var useKafka = builder.Configuration.GetValue<bool>("Kafka:Enabled");
    var useMassTransit = builder.Configuration.GetValue<bool>("MassTransit:Enabled");

    if (useMassTransit)
    {
        builder.Services.AddMassTransitMessaging(builder.Configuration);
    }

    if (useKafka)
    {
        builder.Services.AddKafkaMessaging(builder.Configuration);
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "unique_name",
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

    var authRateLimit = builder.Configuration.GetSection("RateLimiting:Auth").Get<RateLimitOptions>() ?? new RateLimitOptions();
    var mutationRateLimit = builder.Configuration.GetSection("RateLimiting:Mutations").Get<RateLimitOptions>() ?? new RateLimitOptions();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authRateLimit.PermitLimit,
                    Window = TimeSpan.FromMinutes(authRateLimit.WindowMinutes),
                    QueueLimit = 0,
                }));

        options.AddPolicy("mutations", httpContext =>
        {
            var userId = httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.GetUserId().ToString()
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: userId,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = mutationRateLimit.PermitLimit,
                    Window = TimeSpan.FromMinutes(mutationRateLimit.WindowMinutes),
                    QueueLimit = 0,
                });
        });
    });

    builder.Services.AddCors(options =>
        options.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    builder.Services.AddControllers(options =>
        {
            options.Filters.Add<Universal.Transfers.Api.Common.Filters.ValidationFilter>();
        })
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Transfers Operational Dashboard API",
            Version = "v1",
            Description = "Read-mostly operational dashboard for the cross-border Transfers service. Clean Architecture.",
        });

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT access token (without the 'Bearer ' prefix).",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
        };
        c.AddSecurityDefinition("Bearer", scheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db, demoPassword);

        //if (app.Environment.IsDevelopment())
        //{
        //    var logger = app.Services.GetRequiredService<ILogger<Program>>();
        //    logger.LogInformation("Seeding sample transactions via EventProjector for development...");
        //    var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        //    var t = DateTime.UtcNow.AddDays(-7);
        //    await projector.ProjectAsync(new TransactionInitiatedEvent("TX-SEED-001", null, "CREDIT", "PARTNER_A", "PAYME", 500000, "USD", "1234", t), default);
        //    await projector.ProjectAsync(new TransactionCreditCompletedEvent("TX-SEED-001", 1, t.AddSeconds(1)), default);
        //    await projector.ProjectAsync(new TransactionRegistrationCompletedEvent("TX-SEED-001", "PARTNER_A", 1, t.AddSeconds(2)), default);
        //    await projector.ProjectAsync(new TransactionInitiatedEvent("TX-SEED-002", null, "CREDIT", "PARTNER_B", "PAYME", 1200000, "USD", "5678", t.AddHours(1)), default);
        //    await projector.ProjectAsync(new TransactionCreditCompletedEvent("TX-SEED-002", 1, t.AddHours(1).AddSeconds(1)), default);
        //    await projector.ProjectAsync(new TransactionInitiatedEvent("TX-SEED-003", null, "CREDIT", "PARTNER_A", "HUMO", 75000, "EUR", "9012", t.AddHours(2)), default);
        //    await projector.ProjectAsync(new TransactionPausedEvent("TX-SEED-003", "Manual review", null, TransactionStatus.ConfirmSucceeded, t.AddHours(2).AddSeconds(1)), default);
        //    await projector.ProjectAsync(new TransactionInitiatedEvent("TX-SEED-004", null, "CREDIT", "PARTNER_C", "PAYME", 300000, "GBP", "3456", t.AddHours(3)), default);
        //    await projector.ProjectAsync(new TransactionCreditCompletedEvent("TX-SEED-004", 1, t.AddHours(3).AddSeconds(1)), default);
        //    await projector.ProjectAsync(new TransactionRegistrationFailedRetryEvent("TX-SEED-004", "PARTNER_C", 1, t.AddHours(3).AddSeconds(10), "INVALID_PARTNER", t.AddHours(3).AddSeconds(2)), default);
        //    await projector.ProjectAsync(new TransactionRegistrationRetryRequestedEvent("TX-SEED-004", t.AddHours(3).AddSeconds(3)), default);
        //    logger.LogInformation("Sample transactions seeded successfully (idempotent - skipped if already exist)");
        //}
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseCors(CorsPolicy);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", async (AppDbContext db) =>
    {
        try
        {
            await db.Database.CanConnectAsync();
            return Results.Ok(new { status = "healthy", database = "connected" });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 503);
        }
    }).AllowAnonymous();
    app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" })).AllowAnonymous();
    app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
