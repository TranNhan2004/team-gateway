namespace TeamGateway.Api.Options;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Authentication:Cookie";

    public string CookieName { get; init; } = null!;
    public bool HttpOnly { get; init; }
    public string SameSite { get; init; } = null!;
    public string SecurePolicy { get; init; } = null!;
    public string Path { get; init; } = "/";
    public TimeSpan ExpireTimeSpan { get; init; }
    public bool SlidingExpiration { get; init; }
}
