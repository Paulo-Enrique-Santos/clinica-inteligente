namespace Clinica.Domain.Tenancy;

/// <summary>
/// Base de toda entidade de negocio do sistema.
///
/// Herdar daqui nao e opcional: e o que faz o filtro global do EF Core e a policy de RLS
/// do Postgres alcancarem a entidade automaticamente. Uma tabela de negocio que nao herda
/// de <see cref="TenantEntity"/> e um vazamento entre clinicas esperando para acontecer,
/// e existe teste na suite que falha exatamente nesse caso (ver ADR 0001).
/// </summary>
public abstract class TenantEntity
{
    /// <summary>
    /// UUID v7: ordenavel por tempo, o que da localidade de indice no Postgres.
    /// Diferente do v4, nao fragmenta o B-tree a cada insert.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Clinica dona do registro. NUNCA e preenchido a partir de input do cliente:
    /// vem do claim do token, via ITenantContext, e o DbContext o aplica sozinho.
    /// </summary>
    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
