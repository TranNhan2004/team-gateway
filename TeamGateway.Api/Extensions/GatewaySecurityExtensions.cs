using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TeamGateway.Api.Constants;
using TeamGateway.Api.Options;
using TeamGateway.Api.Services;
using TeamGateway.Api.Stores;

namespace TeamGateway.Api.Extensions;

public static class GatewaySecurityExtensions
{
    public static IServiceCollection AddGatewaySecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddGatewaySecurityOptions(configuration);

        var cookie = configuration.GetRequiredSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>()!;
        var keycloak = configuration.GetRequiredSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()!;
        var antiforgery = configuration.GetRequiredSection(GatewayAntiforgeryOptions.SectionName).Get<GatewayAntiforgeryOptions>()!;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(configuration["Redis:ConnectionString"]!);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<RedisTicketStore>();
        services.AddSingleton<IDistributedRefreshLock, DistributedRefreshLock>();
        services.AddSingleton<IInternalJwtIssuer, InternalJwtIssuer>();
        services.AddScoped<IOidcTokenRefreshService, OidcTokenRefreshService>();
        services.AddHttpClient(nameof(OidcTokenRefreshService));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = antiforgery.HeaderName;
            options.Cookie.Name = antiforgery.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = antiforgery.Path;
            options.Cookie.SameSite = Enum.Parse<SameSiteMode>(antiforgery.SameSite, true);
            options.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(antiforgery.SecurePolicy, true);
        });

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemes.ApplicationCookie;
                options.DefaultSignInScheme = AuthenticationSchemes.ApplicationCookie;
                options.DefaultChallengeScheme = AuthenticationSchemes.Keycloak;
            })
            .AddCookie(AuthenticationSchemes.ApplicationCookie, options =>
            {
                options.Cookie.Name = cookie.CookieName;
                options.Cookie.HttpOnly = cookie.HttpOnly;
                options.Cookie.Path = cookie.Path;
                options.Cookie.SameSite = Enum.Parse<SameSiteMode>(cookie.SameSite, true);
                options.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(cookie.SecurePolicy, true);
                options.ExpireTimeSpan = cookie.ExpireTimeSpan;
                options.SlidingExpiration = cookie.SlidingExpiration;
                options.LoginPath = "/api/v1/auth/login";
                options.AccessDeniedPath = "/api/v1/auth/access-denied";
                options.Events.OnValidatePrincipal = async context =>
                {
                    var refresher = context.HttpContext.RequestServices
                        .GetRequiredService<IOidcTokenRefreshService>();
                    var status = await refresher.RefreshIfNeededAsync(
                        context.Properties,
                        context.HttpContext.RequestAborted);

                    if (status == TokenRefreshStatus.Failed)
                    {
                        context.RejectPrincipal();
                    }
                    else if (status == TokenRefreshStatus.Refreshed)
                    {
                        context.ShouldRenew = true;
                    }
                };
            })
            .AddOpenIdConnect(AuthenticationSchemes.Keycloak, options =>
            {
                options.Authority = keycloak.Authority;
                options.ClientId = keycloak.ClientId;
                options.ClientSecret = keycloak.ClientSecret;
                options.CallbackPath = keycloak.CallbackPath;
                options.SignedOutCallbackPath = keycloak.SignedOutCallbackPath;
                options.SignInScheme = AuthenticationSchemes.ApplicationCookie;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.MapInboundClaims = false;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles"
                };
            });

        services
            .AddOptions<CookieAuthenticationOptions>(AuthenticationSchemes.ApplicationCookie)
            .Configure<RedisTicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

        return services;
    }

    private static void AddGatewaySecurityOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>().BindConfiguration(RedisOptions.SectionName)
            .Validate(x => !string.IsNullOrWhiteSpace(x.ConnectionString), "Redis:ConnectionString is required.")
            .ValidateOnStart();
        services.AddOptions<AuthCookieOptions>().BindConfiguration(AuthCookieOptions.SectionName)
            .Validate(x => !string.IsNullOrWhiteSpace(x.CookieName), "Authentication:Cookie:CookieName is required.")
            .Validate(x => x.ExpireTimeSpan > TimeSpan.Zero, "Authentication:Cookie:ExpireTimeSpan must be positive.")
            .ValidateOnStart();
        services.AddOptions<KeycloakOptions>().BindConfiguration(KeycloakOptions.SectionName)
            .Validate(x => !string.IsNullOrWhiteSpace(x.Authority), "Authentication:Keycloak:Authority is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ClientId), "Authentication:Keycloak:ClientId is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ClientSecret), "Authentication:Keycloak:ClientSecret is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.CallbackPath), "Authentication:Keycloak:CallbackPath is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.SignedOutCallbackPath), "Authentication:Keycloak:SignedOutCallbackPath is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ChangePasswordAction), "Authentication:Keycloak:ChangePasswordAction is required.")
            .Validate(x => x.RefreshBeforeExpiry > TimeSpan.Zero, "Authentication:Keycloak:RefreshBeforeExpiry must be positive.")
            .ValidateOnStart();
        services.AddOptions<GatewayAntiforgeryOptions>().BindConfiguration(GatewayAntiforgeryOptions.SectionName)
            .Validate(x => !string.IsNullOrWhiteSpace(x.HeaderName), "Antiforgery:HeaderName is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.CookieName), "Antiforgery:CookieName is required.")
            .ValidateOnStart();
        services.AddOptions<InternalJwtOptions>().BindConfiguration(InternalJwtOptions.SectionName)
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "InternalJwt:Issuer is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "InternalJwt:Audience is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.PrivateKeyPemPath), "InternalJwt:PrivateKeyPem is required.")
            .Validate(x => x.Lifetime > TimeSpan.Zero, "InternalJwt:Lifetime must be positive.")
            .ValidateOnStart();
    }
}
