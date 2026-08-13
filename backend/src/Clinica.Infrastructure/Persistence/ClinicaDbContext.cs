using System.Linq.Expressions;
using Clinica.Domain.Patients;
using Clinica.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Persistence;

/// <summary>
/// Contexto de dados da aplicacao.
///
/// Duas responsabilidades de tenancy vivem aqui, e nenhuma delas depende de o desenvolvedor
/// lembrar de algo na hora de escrever a query:
///   1. filtro global por tenant em toda entidade <see cref="TenantEntity"/>;
///   2. preenchimento e travamento do <c>TenantId</c> na escrita.
/// A terceira barreira (RLS) fica no banco. Ver ADR 0001.
/// </summary>
public class ClinicaDbContext(DbContextOptions<ClinicaDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    private readonly ITenantContext _tenant = tenant;

    public DbSet<Patient> Patients => Set<Patient>();

    /// <summary>
    /// Usado pelo filtro global. Nao lanca de proposito: sem tenant resolvido devolve
    /// <see cref="Guid.Empty"/>, que nao casa com linha nenhuma. Na duvida, nega.
    /// </summary>
    public Guid CurrentTenantId => _tenant.IsResolved ? _tenant.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicaDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(TenantEntity.TenantId)).IsRequired();
            builder.HasIndex(nameof(TenantEntity.TenantId));

            // Monta, por reflexao: e => e.TenantId == this.CurrentTenantId
            //
            // Aplicar em laco (em vez de entidade por entidade) e o ponto principal:
            // entidade nova nasce filtrada sem ninguem precisar lembrar de nada.
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(parameter, nameof(TenantEntity.TenantId)),
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId))),
                parameter);

            builder.HasQueryFilter(filter);
        }
    }

    public override int SaveChanges()
    {
        ApplyTenancy();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenancy();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Carimba o tenant na escrita e impede que ele seja trocado depois.
    /// </summary>
    private void ApplyTenancy()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (!_tenant.IsResolved)
                    {
                        throw new InvalidOperationException(
                            $"Tentativa de inserir {entry.Entity.GetType().Name} sem tenant resolvido. " +
                            "Registro orfao e pior do que erro: falha alto de proposito.");
                    }

                    // Ignora o que veio no objeto. O tenant e o do token, ponto.
                    entry.Entity.TenantId = _tenant.TenantId;
                    entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    GuardSameTenant(entry);
                    // Trava a coluna: nem por bug, nem por payload malicioso um registro
                    // muda de clinica.
                    entry.Property(nameof(TenantEntity.TenantId)).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Deleted:
                    GuardSameTenant(entry);
                    break;
            }
        }
    }

    private void GuardSameTenant(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TenantEntity> entry)
    {
        // Le o valor ORIGINAL (o que esta no banco), nao o atual: senao bastaria o
        // atacante sobrescrever a propriedade em memoria para passar pela checagem.
        var original = entry.Property(nameof(TenantEntity.TenantId)).OriginalValue;

        if (!Equals(original, CurrentTenantId))
        {
            throw new InvalidOperationException(
                $"Escrita cruzada entre clinicas bloqueada em {entry.Entity.GetType().Name}: " +
                $"registro pertence a outro tenant.");
        }
    }
}
