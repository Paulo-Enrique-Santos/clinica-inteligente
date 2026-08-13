using Clinica.Domain.Appointments;
using Clinica.Domain.Patients;
using Clinica.Domain.Procedures;
using Clinica.Domain.Professionals;
using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Treatments;

public enum TreatmentPlanStatus
{
    /// <summary>A doutora prescreveu; ninguém falou de dinheiro ainda.</summary>
    Proposto,

    /// <summary>A recepção fechou o orçamento e as cobranças foram geradas.</summary>
    Aprovado,

    /// <summary>A paciente não quis nada do que foi proposto.</summary>
    Recusado,

    Concluido,
}

public enum PlanItemStatus
{
    Proposto,
    Aceito,

    /// <summary>
    /// A paciente não quis este item agora.
    ///
    /// Recusado, e não apagado: a prescrição da doutora fica registrada. Além de ser
    /// decisão clínica, é o dado que responde quanto do que se prescreve vira venda.
    /// </summary>
    Recusado,
}

/// <summary>
/// O protocolo que a doutora montou numa avaliação: o que a paciente precisa e em que
/// ritmo.
///
/// Separado do atendimento porque um protocolo gera vários — "limpeza agora, botox em 15
/// dias" são dois agendamentos que nascem da mesma conversa. Sem esta entidade, o sistema
/// teria atendimentos soltos e nenhuma memória de que fazem parte do mesmo tratamento.
/// </summary>
public class TreatmentPlan : TenantEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    /// <summary>A avaliação que originou o protocolo, quando houve uma.</summary>
    public Guid? OriginAppointmentId { get; set; }
    public Appointment? OriginAppointment { get; set; }

    public TreatmentPlanStatus Status { get; set; } = TreatmentPlanStatus.Proposto;

    /// <summary>Orientações da doutora, em linguagem de paciente.</summary>
    public string? Notes { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public List<PlanItem> Items { get; set; } = [];
}

/// <summary>Um procedimento prescrito dentro do protocolo.</summary>
public class PlanItem : TenantEntity
{
    public Guid TreatmentPlanId { get; set; }
    public TreatmentPlan? TreatmentPlan { get; set; }

    public Guid ProcedureId { get; set; }
    public Procedure? Procedure { get; set; }

    /// <summary>Quantas sessões deste procedimento.</summary>
    public int Sessions { get; set; } = 1;

    /// <summary>Intervalo recomendado entre as sessões. Nulo = a critério da agenda.</summary>
    public int? IntervalDays { get; set; }

    /// <summary>
    /// Quantos dias depois da aprovação começar.
    ///
    /// É o que traduz "limpeza agora, botox daqui 15 dias" para algo que a recepção
    /// consegue agendar sem reler a observação da doutora.
    /// </summary>
    public int StartAfterDays { get; set; }

    /// <summary>
    /// Preço no momento da prescrição. Congelado como no agendamento: se a tabela mudar
    /// entre a avaliação e o fechamento, vale o que foi combinado com a paciente.
    /// </summary>
    public decimal UnitPrice { get; set; }

    public PlanItemStatus Status { get; set; } = PlanItemStatus.Proposto;

    public string? Notes { get; set; }

    public decimal Total => UnitPrice * Sessions;
}
