using Clinica.Domain.Procedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class ProcedureConfiguration : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder.ToTable("procedures");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);

        // decimal(10,2) e não double: dinheiro em ponto flutuante gera centavo
        // fantasma que ninguém consegue explicar para a dona da clínica.
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.Property(p => p.SuppliesCost).HasPrecision(10, 2);

        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
