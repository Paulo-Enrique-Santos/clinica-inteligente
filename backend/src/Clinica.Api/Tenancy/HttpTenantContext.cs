using Clinica.Domain.Tenancy;

namespace Clinica.Api.Tenancy;

/// <summary>
/// Implementacao de producao do <see cref="ITenantContext"/>: le o tenant do claim
/// <c>tenant_id</c> do token, e de mais lugar nenhum.
///
/// Nao existe fallback para header, query string ou body. Essa ausencia e a feature:
/// se o cliente nao tem como informar o tenant, ele nao tem como pedir o tenant errado.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string ClaimName = "tenant_id";

    private bool _resolved;
    private Guid? _cached;

    private Guid? Value
    {
        get
        {
            // Escopo de requisicao: resolve uma vez, reusa. Evita reparsear o claim a
            // cada consulta do filtro global.
            if (_resolved)
            {
                return _cached;
            }

            var raw = accessor.HttpContext?.User.FindFirst(ClaimName)?.Value;
            _cached = Guid.TryParse(raw, out var tenantId) ? tenantId : null;
            _resolved = true;

            return _cached;
        }
    }

    public bool IsResolved => Value is not null;

    public Guid TenantId => Value ?? throw new InvalidOperationException(
        "Requisicao autenticada sem claim 'tenant_id' valido. Verifique o protocol mapper " +
        "do client no Keycloak (ver infra/README.md).");
}
