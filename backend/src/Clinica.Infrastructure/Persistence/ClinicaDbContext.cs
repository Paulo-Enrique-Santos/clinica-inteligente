using System.Linq.Expressions;
using Clinica.Domain.Appointments;
using Clinica.Domain.Patients;
using Clinica.Domain.Payments;
using Clinica.Domain.Procedures;
using Clinica.Domain.Professionals;
using Clinica.Domain.Stock;
using Clinica.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    /// <summary>
    /// Usado pelo filtro global. Nao lanca de proposito: sem tenant resolvido devolve
    /// <see cref="Guid.Empty"/>, que nao casa com linha nenhuma. Na duvida, nega.
    /// </summary>
    public Guid CurrentTenantId => _tenant.IsResolved ? _tenant.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicaDbContext).Assembly);

        // ---------------------------------------------------------------------
        // Todo instante vira UTC antes de tocar o banco.
        //
        // O Npgsql recusa DateTimeOffset com offset diferente de zero em
        // 'timestamp with time zone' — e a aplicacao recebe horario no fuso da
        // clinica (-03:00), porque e assim que a recepcao pensa. Sem esta conversao,
        // agendar e listar a agenda quebram com erro de driver.
        //
        // Fica como convencao, e nao como .ToUniversalTime() espalhado pelos
        // endpoints, porque basta um caminho esquecer para o bug voltar.
        var paraUtc = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            valor => valor.ToUniversalTime(),
            valor => valor);

        var paraUtcOpcional = new ValueConverter<DateTimeOffset?, DateTimeOffset?>(
            valor => valor.HasValue ? valor.Value.ToUniversalTime() : valor,
            valor => valor);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(paraUtc);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(paraUtcOpcional);
                }
            }

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
