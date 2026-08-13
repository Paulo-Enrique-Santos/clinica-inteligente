using Clinica.Domain.Anamnesis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class AnamnesisLinkConfiguration : IEntityTypeConfiguration<AnamnesisLink>
{
    public void Configure(EntityTypeBuilder<AnamnesisLink> builder)
    {
        builder.ToTable("anamnesis_links");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Token).HasMaxLength(64).IsRequired();
        builder.HasIndex(l => l.Token).IsUnique();

        // Sem RLS e sem filtro global: é a tabela pela qual a clínica é DESCOBERTA.
        // Ver o comentário na entidade.
    }
}

public class AnamnesisResponseConfiguration : IEntityTypeConfiguration<AnamnesisResponse>
{
    public void Configure(EntityTypeBuilder<AnamnesisResponse> builder)
    {
        builder.ToTable("anamnesis_responses");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AnswersJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne(r => r.Patient).WithMany()
            .HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.PatientId });
    }
}
