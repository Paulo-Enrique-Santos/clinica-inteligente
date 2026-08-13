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

    /// <summary>Quando a doutora fechou o atendimento.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// O que aconteceu no atendimento, escrito pela profissional.
    ///
    /// Separado de <see cref="Notes"/>, que é recado de agendamento ("chegou atrasada",
    /// "pediu horário à tarde"). Misturar os dois faria a recepção ler observação
    /// clínica e a doutora procurar informação de agenda no lugar errado.
    /// </summary>
    public string? ExecutionNotes { get; set; }

    /// <summary>
    /// Quando perguntar à paciente como ela está.
    ///
    /// Aqui apenas registramos a intenção; quem envia é o agente de pós-procedimento
    /// (Fase 8). Guardar a data desde já significa que, no dia em que o agente entrar,
    /// ele encontra o histórico pronto em vez de começar do zero.
    /// </summary>
    public DateTimeOffset? FollowUpAt { get; set; }

    public DateTimeOffset? FollowUpSentAt { get; set; }

    /// <summary>
    /// Um atendimento cancelado libera o horário; os demais o ocupam. Esta regra também
    /// existe no banco, na constraint de sobreposição.
    /// </summary>
    public bool OccupiesSchedule => Status != AppointmentStatus.Cancelado;
}
