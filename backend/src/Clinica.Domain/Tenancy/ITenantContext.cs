namespace Clinica.Domain.Tenancy;

/// <summary>
/// Fonte unica de verdade sobre "de qual clinica e esta requisicao".
///
/// A implementacao de producao le o claim <c>tenant_id</c> do token e nada mais.
/// Nao existe sobrecarga que aceite tenant por parametro, e isso e deliberado: se
/// existisse, alguem eventualmente a chamaria com valor vindo do cliente.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Falso em contextos sem usuario autenticado (migrations, design time, health check).
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Clinica da requisicao atual. Lanca se <see cref="IsResolved"/> for falso —
    /// falhar alto e melhor do que gravar registro orfao.
    /// </summary>
    Guid TenantId { get; }
}
