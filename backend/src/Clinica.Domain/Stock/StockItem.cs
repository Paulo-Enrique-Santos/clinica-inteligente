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
public enum StockControlMode
{
    /// <summary>
    /// A profissional informa quanto usou ao fechar o atendimento.
    ///
    /// Para insumo caro e mensurável: preenchimento, toxina, agulha. O consumo real é
    /// o que permite comparar com o previsto e enxergar a margem de verdade.
    /// </summary>
    Informado,

    /// <summary>
    /// Baixa de uma embalagem inteira quando ela é aberta, e o sistema para de contar.
    ///
    /// Para creme e luva: um frasco atende "umas cinquenta" pacientes, e o quanto se
    /// passa é no olho. Pedir a quantidade por atendimento produziria número inventado —
    /// pior do que não medir, porque pareceria dado.
    /// </summary>
    PorAbertura,
}

public class StockItem : TenantEntity
{
    public required string Name { get; set; }

    /// <summary>
    /// Como o insumo é COMPRADO: frasco, caixa, unidade.
    ///
    /// Separado da unidade de consumo porque quase nunca são a mesma coisa — compra-se
    /// um frasco, aplica-se em ml.
    /// </summary>
    public required string PurchaseUnit { get; set; }

    /// <summary>Quanto vem em cada embalagem: 10 (ml), 50 (pares), 1 (unidade).</summary>
    public decimal ContentPerUnit { get; set; } = 1;

    /// <summary>Como o insumo é CONSUMIDO: ml, par, un.</summary>
    public required string Unit { get; set; }

    public StockControlMode ControlMode { get; set; } = StockControlMode.Informado;

    /// <summary>Na unidade de consumo, igual ao saldo. Abaixo disto, a tela acende alerta.</summary>
    public decimal MinimumQuantity { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>
    /// Quantas embalagens fechadas o saldo representa, e quanto sobra em aberto.
    ///
    /// É aproximação deliberada: com várias profissionais, 23ml podem estar em dois
    /// frascos abertos, não em um. O número serve para comprar e para saber que há
    /// produto aproveitável — não para dizer qual frasco está onde. Rastrear frasco a
    /// frasco exigiria a doutora apontar o lote a cada aplicação, o que é controle de
    /// farmácia, não de clínica.
    /// </summary>
    public (int Fechadas, decimal Aberto) Repartir(decimal saldo)
    {
        if (ContentPerUnit <= 0)
        {
            return (0, saldo);
        }

        var fechadas = (int)Math.Floor(saldo / ContentPerUnit);
        return (Math.Max(0, fechadas), saldo - (fechadas * ContentPerUnit));
    }
}
