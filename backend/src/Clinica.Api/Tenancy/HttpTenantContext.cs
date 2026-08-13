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
    private Guid? _assumido;

    /// <summary>
    /// Assume uma clínica sem token de usuário. Existe para UM caso: a ficha de anamnese
    /// que a paciente preenche por link, sem login.
    ///
    /// A porta é estreita de propósito — só funciona quando não há usuário autenticado, e
    /// só é chamada depois de o link ter sido validado (token aleatório, com validade e
    /// uso único). Se houvesse claim, o claim manda; nenhuma requisição autenticada
    /// consegue trocar de clínica por aqui.
    /// </summary>
    public void AssumirPorLinkValidado(Guid tenantId)
    {
        if (Value is not null)
        {
            throw new InvalidOperationException(
                "Requisicao ja tem tenant pelo token; assumir outro nao e permitido.");
        }

        _assumido = tenantId;
    }

    private Guid? Value
    {
        get
        {
            if (_assumido is { } assumido)
            {
                return assumido;
            }

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
