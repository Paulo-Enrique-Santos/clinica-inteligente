using Clinica.Domain.Patients;
using Clinica.Domain.Procedures;
using Clinica.Domain.Professionals;
using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Appointments;

/// <summary>
/// Um atendimento na agenda.
///
/// É a entidade central do sistema: dela dependem o financeiro (o que cobrar), o estoque
/// (o que dar baixa) e, a partir da Fase 4, quase todos os agentes — confirmar, remarcar,
/// perguntar como foi o pós-procedimento.
/// </summary>
public class Appointment : TenantEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid ProcedureId { get; set; }
    public Procedure? Procedure { get; set; }

    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>
    /// Calculado a partir da duração do procedimento no momento do agendamento, e não
    /// derivado na leitura. Se a clínica mudar a duração padrão depois, os atendimentos já
    /// marcados mantêm o horário combinado com a paciente.
    /// </summary>
    public DateTimeOffset EndsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Agendado;

    /// <summary>Preço no momento do agendamento — tabela muda, o combinado não.</summary>
    public decimal Price { get; set; }

    public string? Notes { get; set; }

    /// <summary>Motivo do cancelamento, quando houver. Alimenta o relatório de gestão.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Um atendimento cancelado libera o horário; os demais o ocupam. Esta regra também
    /// existe no banco, na constraint de sobreposição.
    /// </summary>
    public bool OccupiesSchedule => Status != AppointmentStatus.Cancelado;
}
