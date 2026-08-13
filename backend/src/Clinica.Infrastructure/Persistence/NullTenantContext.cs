using Clinica.Domain.Tenancy;

namespace Clinica.Infrastructure.Persistence;

/// <summary>
/// Contexto sem tenant, para cenarios que legitimamente nao tem um: migrations,
/// design time do EF, health check.
///
/// Ler <see cref="TenantId"/> lanca. Isso e proposital — se algum caminho de aplicacao
/// acabar usando este contexto por engano, queremos uma excecao barulhenta, nao uma
/// consulta silenciosa em cima de dado de outra clinica.
/// </summary>
public sealed class NullTenantContext : ITenantContext
{
    public bool IsResolved => false;

    public Guid TenantId => throw new InvalidOperationException(
        "Nenhum tenant resolvido neste contexto. Se isto apareceu durante uma requisicao " +
        "HTTP, o token nao trouxe o claim tenant_id.");
}
