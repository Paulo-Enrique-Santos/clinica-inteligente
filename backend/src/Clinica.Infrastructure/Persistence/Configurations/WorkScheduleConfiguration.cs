using Clinica.Domain.Professionals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        builder.ToTable("work_schedules");
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Professional)
            .WithMany()
            .HasForeignKey(w => w.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um expediente por dia da semana. Dois registros para a mesma terça criariam
        // duas respostas para "a que horas ela atende?".
        builder.HasIndex(w => new { w.TenantId, w.ProfessionalId, w.DayOfWeek }).IsUnique();
    }
}

public class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        builder.ToTable("schedule_exceptions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Reason).HasMaxLength(300);

        builder.HasOne(e => e.Professional)
            .WithMany()
            .HasForeignKey(e => e.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.ProfessionalId, e.Date }).IsUnique();
    }
}
