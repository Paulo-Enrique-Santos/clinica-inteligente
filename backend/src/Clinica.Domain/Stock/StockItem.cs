using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Stock;

/// <summary>
/// Insumo controlado pela clínica: toxina, ácido, agulha, luva.
///
/// Repare no que NÃO existe aqui: um campo de saldo atual. O saldo é a soma das
/// movimentações, calculada na consulta.
///
/// Guardar saldo numa coluna é mais rápido de ler e cria uma classe inteira de bugs: basta
/// uma movimentação gravada sem atualizar o total — por exceção, por script manual, por
/// caminho novo que alguém escreveu — para o sistema passar a mentir, e ninguém percebe
/// até faltar produto no meio de um procedimento. Com o volume de uma clínica, somar é
/// barato e sempre verdade.
/// </summary>
public class StockItem : TenantEntity
{
    public required string Name { get; set; }

    /// <summary>Unidade de medida: ml, un, g, caixa.</summary>
    public required string Unit { get; set; }

    /// <summary>Abaixo disto, a tela de estoque acende alerta.</summary>
    public decimal MinimumQuantity { get; set; }

    public bool Active { get; set; } = true;
}
