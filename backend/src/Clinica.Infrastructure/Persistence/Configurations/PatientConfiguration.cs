using Clinica.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.PhoneE164).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(4000);

        // Unicidade POR CLINICA, nao global: o mesmo telefone pode ser paciente de duas
        // clinicas diferentes, e uma nao pode descobrir isso sobre a outra.
        builder.HasIndex(p => new { p.TenantId, p.PhoneE164 }).IsUnique();
    }
}
