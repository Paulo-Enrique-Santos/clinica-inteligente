using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Clinica.Api.Tenancy;

/// <summary>
/// O Keycloak entrega os papeis aninhados em <c>realm_access.roles</c>, que o ASP.NET Core
/// nao entende nativamente — sem esta traducao, <c>[Authorize(Roles = "...")]</c> nunca
/// casa e todo endpoint com papel retorna 403 sem explicacao.
/// </summary>
public sealed class KeycloakRolesTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            return Task.FromResult(principal);
        }

        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return Task.FromResult(principal);
        }

        using var document = JsonDocument.Parse(realmAccess);

        if (!document.RootElement.TryGetProperty("roles", out var roles) ||
            roles.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(principal);
        }

        foreach (var role in roles.EnumerateArray())
        {
            var name = role.GetString();

            // Esta transformacao roda a cada autenticacao; sem a checagem, os claims
            // se acumulariam no principal.
            if (!string.IsNullOrWhiteSpace(name) && !identity.HasClaim(ClaimTypes.Role, name))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, name));
            }
        }

        return Task.FromResult(principal);
    }
}
