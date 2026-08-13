namespace Clinica.Domain.Treatments;

public enum FormaDePagamento
{
    /// <summary>Uma cobrança só.</summary>
    AVista,

    /// <summary>Sinal agora, restante parcelado.</summary>
    SinalMaisParcelas,

    /// <summary>Tudo parcelado (cartão ou PIX recorrente — muda a forma, não o plano).</summary>
    Parcelado,
}

public sealed record Parcela(int Numero, int Total, decimal Valor, DateOnly Vencimento);

/// <summary>
/// Transforma "como a paciente vai pagar" numa lista de cobranças com datas.
///
/// Lógica pura, sem banco: as bordas que dão problema em clínica são de centavo e de
/// data, e testá-las não deveria exigir subir Postgres.
///
/// Cartão parcelado e PIX recorrente produzem o MESMO plano — o que muda é a forma de
/// pagamento registrada na baixa. Por isso não existe um modo para cada: seriam dois
/// caminhos de código para o mesmo cálculo.
/// </summary>
public static class PlanoDePagamento
{
    public static IReadOnlyList<Parcela> Gerar(
        decimal total,
        FormaDePagamento forma,
        DateOnly primeiroVencimento,
        int parcelas = 1,
        decimal sinal = 0)
    {
        if (total <= 0)
        {
            return [];
        }

        return forma switch
        {
            FormaDePagamento.AVista =>
                [new Parcela(1, 1, total, primeiroVencimento)],

            FormaDePagamento.SinalMaisParcelas =>
                ComSinal(total, sinal, primeiroVencimento, parcelas),

            _ => Dividir(total, primeiroVencimento, Math.Max(1, parcelas)),
        };
    }

    private static List<Parcela> ComSinal(
        decimal total,
        decimal sinal,
        DateOnly primeiroVencimento,
        int parcelas)
    {
        // Sinal maior que o total é erro de digitação; tratar como pagamento à vista
        // evita gerar uma parcela negativa.
        if (sinal >= total)
        {
            return [new Parcela(1, 1, total, primeiroVencimento)];
        }

        var restante = Dividir(total - sinal, primeiroVencimento.AddMonths(1), Math.Max(1, parcelas));

        var lista = new List<Parcela>(restante.Count + 1)
        {
            new(1, restante.Count + 1, sinal, primeiroVencimento),
        };

        // O sinal é a parcela 1, então as demais deslizam uma casa.
        lista.AddRange(restante.Select(p =>
            new Parcela(p.Numero + 1, restante.Count + 1, p.Valor, p.Vencimento)));

        return lista;
    }

    private static List<Parcela> Dividir(decimal valor, DateOnly primeiroVencimento, int parcelas)
    {
        // Arredonda para baixo e joga a diferença na ÚLTIMA parcela.
        //
        // 100 em 3 vezes dá 33,33 + 33,33 + 33,34. Distribuir igual e deixar sobrar um
        // centavo é o tipo de erro que a clínica só descobre quando o total cobrado não
        // bate com o combinado.
        var base_ = Math.Floor(valor / parcelas * 100) / 100;
        var lista = new List<Parcela>(parcelas);

        for (var i = 1; i <= parcelas; i++)
        {
            var ultimo = i == parcelas;
            var montante = ultimo ? valor - (base_ * (parcelas - 1)) : base_;

            lista.Add(new Parcela(i, parcelas, montante, primeiroVencimento.AddMonths(i - 1)));
        }

        return lista;
    }
}
