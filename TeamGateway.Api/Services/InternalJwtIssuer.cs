using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeamGateway.Api.Options;

namespace TeamGateway.Api.Services;

public interface IInternalJwtIssuer
{
    string Create(ClaimsPrincipal principal);
}

public sealed class InternalJwtIssuer : IInternalJwtIssuer
{
    private static readonly HashSet<string> ReservedClaimTypes =
    [JwtRegisteredClaimNames.Aud, JwtRegisteredClaimNames.Exp, JwtRegisteredClaimNames.Iat, JwtRegisteredClaimNames.Iss];

    private readonly InternalJwtOptions _options;
    private readonly SigningCredentials _credentials;

    public InternalJwtIssuer(IOptions<InternalJwtOptions> options)
    {
        _options = options.Value;
        var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKeyPemPath);
        _credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    }

    public string Create(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("The authenticated principal has no sub claim.");

        var claims = principal.Claims
            .Where(claim => claim.Type != JwtRegisteredClaimNames.Sub
                && !ReservedClaimTypes.Contains(claim.Type))
            .Append(new Claim(JwtRegisteredClaimNames.Sub, subject))
            .ToList();

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(_options.Lifetime),
            signingCredentials: _credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
