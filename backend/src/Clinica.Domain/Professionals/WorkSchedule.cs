using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Professionals;

/// <summary>
/// Expediente padrão de uma profissional num dia da semana.
///
/// Padrão semanal em vez de dia a dia: a clínica define uma vez e não precisa preencher
/// agenda toda segunda-feira. O dia que fugir do padrão vira uma
/// <see cref="ScheduleException"/>.
/// </summary>
public class WorkSchedule : TenantEntity
{
    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }

    /// <summary>Intervalo de almoço. Ou os dois campos vêm, ou nenhum.</summary>
    public TimeOnly? BreakStartsAt { get; set; }
    public TimeOnly? BreakEndsAt { get; set; }
}

/// <summary>
/// O dia que foge do padrão: congresso, folga, plantão diferente.
///
/// Sobrepõe o expediente semanal daquela data. Se <see cref="Closed"/>, a profissional
/// não atende — e nenhum horário é oferecido.
/// </summary>
public class ScheduleException : TenantEntity
{
    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DateOnly Date { get; set; }

    public bool Closed { get; set; }

    public TimeOnly? StartsAt { get; set; }
    public TimeOnly? EndsAt { get; set; }
    public TimeOnly? BreakStartsAt { get; set; }
    public TimeOnly? BreakEndsAt { get; set; }

    public string? Reason { get; set; }
}
