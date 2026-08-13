using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clinica.Tests.Infra;

/// <summary>
/// Substitui o JWT do Keycloak nos testes: o principal vem de headers, e cada teste
/// escolhe de qual clinica e com qual papel esta falando.
///
/// A troca e deliberada e tem limite conhecido: estes testes cobrem a TENANCY (filtro
/// global, RLS, carimbo na escrita), nao a validacao de token. Subir Keycloak a cada
/// execucao custaria minutos por rodada para exercitar codigo que e da Microsoft, nao
/// nosso. A validacao de token propriamente dita fica coberta manualmente e, mais adiante,
/// por um teste de fumaca no ambiente completo.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // Nao chamar de "Scheme": a classe base ja tem um membro com esse nome.
    public const string SchemeName = "Test";
    public const string TenantHeader = "X-Test-Tenant";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TenantHeader, out var tenant) ||
            string.IsNullOrWhiteSpace(tenant))
        {
            // Sem header, requisicao anonima — o que permite testar o 401.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString()),
            new(ClaimTypes.NameIdentifier, "usuario-de-teste"),
            new("preferred_username", "usuario-de-teste"),
        };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
