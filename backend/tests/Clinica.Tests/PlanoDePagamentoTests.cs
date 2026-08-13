using Clinica.Domain.Treatments;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Cálculo das parcelas. Testes puros: sem banco, sem HTTP — as bordas que dão problema
/// numa clínica são de centavo e de data.
/// </summary>
public class PlanoDePagamentoTests
{
    private static readonly DateOnly Vencimento = new(2027, 3, 10);

    [Fact]
    public void A_vista_gera_uma_cobranca_com_o_total()
    {
        var parcelas = PlanoDePagamento.Gerar(1200m, FormaDePagamento.AVista, Vencimento);

        var unica = Assert.Single(parcelas);
        Assert.Equal(1200m, unica.Valor);
        Assert.Equal(Vencimento, unica.Vencimento);
    }

    [Fact]
    public void Parcelamento_nao_perde_centavo()
    {
        var parcelas = PlanoDePagamento.Gerar(100m, FormaDePagamento.Parcelado, Vencimento, parcelas: 3);

        // 33,33 + 33,33 + 33,34. Distribuir igual deixaria a clínica cobrando 99,99.
        Assert.Equal(3, parcelas.Count);
        Assert.Equal(100m, parcelas.Sum(p => p.Valor));
        Assert.Equal(33.34m, parcelas[^1].Valor);
    }

    [Fact]
    public void Parcelas_vencem_de_mes_em_mes()
    {
        var parcelas = PlanoDePagamento.Gerar(900m, FormaDePagamento.Parcelado, Vencimento, parcelas: 3);

        Assert.Equal(new DateOnly(2027, 3, 10), parcelas[0].Vencimento);
        Assert.Equal(new DateOnly(2027, 4, 10), parcelas[1].Vencimento);
        Assert.Equal(new DateOnly(2027, 5, 10), parcelas[2].Vencimento);
    }

    [Fact]
    public void Vencimento_no_dia_31_cai_no_ultimo_dia_do_mes_curto()
    {
        var parcelas = PlanoDePagamento.Gerar(
            300m, FormaDePagamento.Parcelado, new DateOnly(2027, 1, 31), parcelas: 3);

        // Fevereiro não tem 31. Sem esse cuidado, a data estouraria ou pularia para março.
        Assert.Equal(new DateOnly(2027, 2, 28), parcelas[1].Vencimento);
        Assert.Equal(new DateOnly(2027, 3, 31), parcelas[2].Vencimento);
    }

    [Fact]
    public void Sinal_cobra_a_entrada_hoje_e_o_restante_nos_meses_seguintes()
    {
        var parcelas = PlanoDePagamento.Gerar(
            1000m, FormaDePagamento.SinalMaisParcelas, Vencimento, parcelas: 2, sinal: 400m);

        Assert.Equal(3, parcelas.Count);
        Assert.Equal(400m, parcelas[0].Valor);
        Assert.Equal(Vencimento, parcelas[0].Vencimento);

        // O restante (600) em 2x, começando no mês seguinte.
        Assert.Equal(300m, parcelas[1].Valor);
        Assert.Equal(new DateOnly(2027, 4, 10), parcelas[1].Vencimento);
        Assert.Equal(1000m, parcelas.Sum(p => p.Valor));
    }

    [Fact]
    public void Sinal_maior_que_o_total_vira_pagamento_a_vista()
    {
        var parcelas = PlanoDePagamento.Gerar(
            500m, FormaDePagamento.SinalMaisParcelas, Vencimento, parcelas: 3, sinal: 800m);

        // Erro de digitação não pode gerar parcela negativa.
        var unica = Assert.Single(parcelas);
        Assert.Equal(500m, unica.Valor);
    }

    [Fact]
    public void Total_zerado_nao_gera_cobranca()
    {
        Assert.Empty(PlanoDePagamento.Gerar(0m, FormaDePagamento.Parcelado, Vencimento, parcelas: 3));
    }
}
