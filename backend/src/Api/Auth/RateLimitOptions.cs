namespace Universal.Transfers.Api.Auth;

public class RateLimitOptions
{
    public int PermitLimit { get; set; } = 10;
    public int WindowMinutes { get; set; } = 1;
}
