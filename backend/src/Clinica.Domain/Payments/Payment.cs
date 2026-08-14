using Clinica.Domain.Appointments;
using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Payments;

public enum PaymentStatus
{
    Pendente,
    Pago,
    Cancelado,

    /// <summary>
    /// Foi pago e depois desfeito: cartão recusado, chargeback, devolução.
    ///
    /// Estado próprio em vez de voltar para Pendente, porque a clínica precisa
    /// distinguir "nunca entrou" de "entrou e voltou" — são conversas diferentes com a
    /// paciente e linhas diferentes no relatório.
    /// </summary>
    Estornado,
}

public enum PaymentMethod
{
    Pix,
    Dinheiro,
    Credito,
    Debito,
    Transferencia,
}

/// <summary>
/// Cobrança de um atendimento.
///
/// A partir da Fase 8, é esta entidade que dispara o agente de cobrança: vence hoje e
/// continua pendente, a paciente recebe mensagem.
/// </summary>
public class Payment : TenantEntity
{
    /// <summary>
    /// Cobrança de um atendimento avulso.
    ///
    /// Opcional desde a Fase G: quando a paciente fecha um protocolo, as cobranças
    /// nascem do orçamento e ainda não existe agendamento para amarrá-las.
    /// </summary>
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    /// <summary>Cobrança que veio do fechamento de um protocolo.</summary>
    public Guid? TreatmentPlanId { get; set; }

    /// <summary>Parcela N de M. Nulo em cobrança única.</summary>
    public int? InstallmentNumber { get; set; }
    public int? InstallmentCount { get; set; }

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pendente;

    /// <summary>Só faz sentido depois de pago — antes disso, ninguém sabe como será.</summary>
    public PaymentMethod? Method { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// "Vencido" NÃO é um status gravado, e sim uma conta feita na hora.
    ///
    /// Status vencido no banco envelhece: alguém teria que rodar uma rotina diária para
    /// virar Pendente em Vencido, e no dia em que essa rotina falhasse o financeiro veria
    /// cobrança em atraso como se estivesse em dia. Derivar da data nunca mente.
    /// </summary>
    public bool IsOverdue(DateOnly hoje) => Status == PaymentStatus.Pendente && DueDate < hoje;
}
