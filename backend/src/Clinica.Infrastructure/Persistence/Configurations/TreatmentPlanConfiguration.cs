using Clinica.Domain.Treatments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class TreatmentPlanConfiguration : IEntityTypeConfiguration<TreatmentPlan>
{
    public void Configure(EntityTypeBuilder<TreatmentPlan> builder)
    {
        builder.ToTable("treatment_plans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(4000);

        builder.HasOne(p => p.Patient).WithMany()
            .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Professional).WithMany()
            .HasForeignKey(p => p.ProfessionalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.OriginAppointment).WithMany()
            .HasForeignKey(p => p.OriginAppointmentId).OnDelete(DeleteBehavior.SetNull);

        // Itens só existem dentro do protocolo: apagar o protocolo leva os itens junto.
        builder.HasMany(p => p.Items).WithOne(i => i.TreatmentPlan)
            .HasForeignKey(i => i.TreatmentPlanId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.PatientId, p.Status });
    }
}

public class PlanItemConfiguration : IEntityTypeConfiguration<PlanItem>
{
    public void Configure(EntityTypeBuilder<PlanItem> builder)
    {
        builder.ToTable("plan_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(10, 2);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasOne(i => i.Procedure).WithMany()
            .HasForeignKey(i => i.ProcedureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.TreatmentPlanId });
    }
}
