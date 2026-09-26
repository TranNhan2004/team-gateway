namespace TeamGateway.Api.Options;

public sealed class InternalJwtOptions
{
    public const string SectionName = "InternalJwt";

    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string PrivateKeyPemPath { get; init; } = null!;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(5);
}
