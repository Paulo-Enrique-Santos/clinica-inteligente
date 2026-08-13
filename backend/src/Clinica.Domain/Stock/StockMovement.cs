using Clinica.Domain.Appointments;
using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Stock;

public enum StockMovementType
{
    /// <summary>Compra, reposição, devolução do fornecedor.</summary>
    Entrada,

    /// <summary>Uso em procedimento, perda, validade vencida.</summary>
    Saida,

    /// <summary>Correção após contagem física. Sempre com motivo.</summary>
    Ajuste,
}

/// <summary>
/// Cada entrada e saída de insumo. É o livro-razão do estoque: o saldo é a soma disto,
/// e nada apaga uma movimentação — correção se faz com um <see cref="StockMovementType.Ajuste"/>,
/// que fica registrado.
/// </summary>
public class StockMovement : TenantEntity
{
    public Guid StockItemId { get; set; }
    public StockItem? StockItem { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>
    /// Em <see cref="StockMovementType.Entrada"/> e <see cref="StockMovementType.Saida"/> é
    /// sempre positiva — quem dá a direção é o <see cref="Type"/>.
    ///
    /// Em <see cref="StockMovementType.Ajuste"/> vem com sinal, porque a contagem física
    /// tanto pode achar produto a mais quanto a menos.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Preenchido quando a saída veio de um atendimento. É o que vai permitir, na Fase 13,
    /// responder quanto de insumo cada procedimento realmente consome — e não quanto a
    /// tabela diz que consome.
    /// </summary>
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string? Reason { get; set; }

    /// <summary>Quanto esta movimentação soma ao saldo.</summary>
    public decimal SignedQuantity => Type switch
    {
        StockMovementType.Entrada => Math.Abs(Quantity),
        StockMovementType.Saida => -Math.Abs(Quantity),
        _ => Quantity, // Ajuste já carrega o sinal
    };
}
