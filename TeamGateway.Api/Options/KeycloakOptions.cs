namespace TeamGateway.Api.Options;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";

    public string Authority { get; init; } = null!;
    public string ClientId { get; init; } = null!;
    public string ClientSecret { get; init; } = null!;
    public string CallbackPath { get; init; } = null!;
    public string SignedOutCallbackPath { get; init; } = null!;
    public TimeSpan RefreshBeforeExpiry { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan RefreshLockTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
