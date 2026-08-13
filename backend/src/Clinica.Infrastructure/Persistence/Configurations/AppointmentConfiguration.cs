using Clinica.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);

        // Texto, não número: quem for investigar um problema direto no banco lê "faltou"
        // em vez de "3".
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Price).HasPrecision(10, 2);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.CancellationReason).HasMaxLength(500);

        // Restrict e não Cascade: apagar uma paciente não pode varrer o histórico de
        // atendimentos, que é registro clínico e financeiro.
        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Procedure)
            .WithMany()
            .HasForeignKey(a => a.ProcedureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Professional)
            .WithMany()
            .HasForeignKey(a => a.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Consulta mais frequente do sistema: "o que tem na agenda deste dia".
        builder.HasIndex(a => new { a.TenantId, a.StartsAt });
        builder.HasIndex(a => new { a.TenantId, a.ProfessionalId, a.StartsAt });
        builder.HasIndex(a => new { a.TenantId, a.PatientId });
    }
}
