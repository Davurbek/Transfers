using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Universal.Transfers.Api.Auth;
using Universal.Transfers.Api.Messaging;
using Universal.Transfers.Application;
using Universal.Transfers.Application.Auth;
using Universal.Transfers.Application.Messaging;
using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Infrastructure;
using Universal.Transfers.Infrastructure.Common.Persistence;
using Universal.Transfers.Infrastructure.Messaging.Kafka;
using Universal.Transfers.Infrastructure.Seeding;
using Universal.Transfers.Infrastructure.Messaging.MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var connectionString = builder.Configuration.GetConnectionString("Dashboard") ?? "Server=.;Database=UniversalDocumentFlow;Integrated Security=True;MultipleActiveResultSets=true;Encrypt=False";
var demoPassword = builder.Configuration.GetValue<string>("SeedData:DemoPassword") ?? "Passw0rd!";

const string CorsPolicy = "dashboard-ui";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173", "http://localhost:5290", "http://127.0.0.1:5173", "http://127.0.0.1:5290"];

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

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

if (useKafka || useMassTransit)
{
    builder.Services.AddSingleton<ICommandPublisher, KafkaCommandPublisher>();
}

if (!useKafka && !useMassTransit)
{
    builder.Services.AddSingleton<SimulatedBroker>();
    builder.Services.AddSingleton<ICommandPublisher>(sp => sp.GetRequiredService<SimulatedBroker>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulatedBroker>());
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
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddControllers()
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InboxMessages')
        CREATE TABLE [dbo].[InboxMessages] (
            [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [IdempotencyKey] NVARCHAR(200) NOT NULL,
            [EventType] NVARCHAR(500) NOT NULL,
            [ProcessedAt] DATETIME2 NOT NULL,
            CONSTRAINT [UQ_InboxMessages_IdempotencyKey] UNIQUE ([IdempotencyKey])
        )");
    await DbSeeder.SeedAsync(db, demoPassword);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
