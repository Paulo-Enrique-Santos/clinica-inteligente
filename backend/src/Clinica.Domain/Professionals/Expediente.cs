namespace Clinica.Domain.Professionals;

/// <summary>
/// O expediente de uma profissional numa data concreta, já resolvido: exceção sobrepondo
/// o padrão semanal.
///
/// Existe para que a regra de "cabe neste horário?" seja calculada num lugar só. Ela é
/// usada tanto para oferecer horários livres quanto para validar o agendamento — e as
/// duas respostas precisam concordar, senão o sistema oferece um horário e depois recusa.
/// </summary>
public sealed record Expediente(
    bool Atende,
    TimeOnly Inicio,
    TimeOnly Fim,
    TimeOnly? IntervaloInicio,
    TimeOnly? IntervaloFim,
    bool Irrestrito = false)
{
    public static readonly Expediente Fechado =
        new(false, default, default, null, null);

    /// <summary>
    /// Profissional sem expediente cadastrado: qualquer horário serve.
    ///
    /// Negar seria coerente com o resto do sistema, mas erra o alvo. Aqui não há
    /// fronteira de segurança — tenant, papel e conflito de horário continuam valendo.
    /// Negar só significaria que a clínica cria uma doutora e não consegue marcar nada
    /// até preencher o quadro semanal inteiro, logo no dia em que está conhecendo o
    /// sistema. A restrição passa a valer quando a clínica define o expediente.
    /// </summary>
    public static readonly Expediente SemRestricao =
        new(true, TimeOnly.MinValue, TimeOnly.MaxValue, null, null, Irrestrito: true);

    public static Expediente De(WorkSchedule padrao) =>
        new(true, padrao.StartsAt, padrao.EndsAt, padrao.BreakStartsAt, padrao.BreakEndsAt);

    /// <summary>Aplica a exceção do dia sobre o padrão semanal, quando houver.</summary>
    public static Expediente De(ScheduleException excecao, Expediente? padrao)
    {
        if (excecao.Closed)
        {
            return Fechado;
        }

        // Exceção pode alterar só parte do dia (ex.: "hoje começo às 11h"), então o que
        // ela não informar continua vindo do padrão.
        return new Expediente(
            true,
            excecao.StartsAt ?? padrao?.Inicio ?? new TimeOnly(8, 0),
            excecao.EndsAt ?? padrao?.Fim ?? new TimeOnly(18, 0),
            excecao.BreakStartsAt ?? padrao?.IntervaloInicio,
            excecao.BreakEndsAt ?? padrao?.IntervaloFim);
    }

    /// <summary>
    /// O atendimento cabe inteiro no expediente, sem invadir o intervalo?
    ///
    /// É esta regra que faz um procedimento de 2 horas não poder começar às 17h num
    /// expediente que termina às 18h.
    /// </summary>
    public bool Comporta(TimeOnly inicio, TimeOnly fim)
    {
        if (!Atende || fim <= inicio)
        {
            return false;
        }

        if (Irrestrito)
        {
            return true;
        }

        if (inicio < Inicio || fim > Fim)
        {
            return false;
        }

        if (IntervaloInicio is { } almocoInicio && IntervaloFim is { } almocoFim)
        {
            // Sobreposição com o almoço: começa antes de o almoço acabar e termina
            // depois de ele começar.
            var invade = inicio < almocoFim && fim > almocoInicio;
            if (invade)
            {
                return false;
            }
        }

        return true;
    }
}
