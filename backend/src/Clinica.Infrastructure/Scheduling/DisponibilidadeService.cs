using Clinica.Domain.Appointments;
using Clinica.Domain.Professionals;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Scheduling;

/// <summary>
/// Responde duas perguntas que precisam concordar entre si:
/// "quais horários estão livres?" e "este horário serve?".
///
/// As duas usam o mesmo <see cref="Expediente"/> de propósito. Se cada uma tivesse a
/// própria conta, o sistema acabaria oferecendo um horário na tela e recusando no envio
/// — que é o tipo de incoerência que faz a recepção perder confiança no sistema.
/// </summary>
public class DisponibilidadeService(ClinicaDbContext db)
{
    /// <summary>De quanto em quanto tempo os horários são oferecidos.</summary>
    private const int PassoEmMinutos = 15;

    /// <summary>
    /// Fuso da clínica. Mesma premissa do front: o Brasil não tem horário de verão desde
    /// 2019. Vira configuração por tenant quando houver clínica em outro fuso.
    /// </summary>
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    public async Task<Expediente> ExpedienteDeAsync(
        Guid profissionalId,
        DateOnly data,
        CancellationToken ct = default)
    {
        var padraoDoDia = await db.WorkSchedules
            .FirstOrDefaultAsync(
                w => w.ProfessionalId == profissionalId && w.DayOfWeek == data.DayOfWeek,
                ct);

        var padrao = padraoDoDia is null ? null : Expediente.De(padraoDoDia);

        var excecao = await db.ScheduleExceptions
            .FirstOrDefaultAsync(e => e.ProfessionalId == profissionalId && e.Date == data, ct);

        if (excecao is not null)
        {
            return Expediente.De(excecao, padrao);
        }

        if (padrao is not null)
        {
            return padrao;
        }

        // Nenhum expediente cadastrado para esta profissional em dia nenhum: o sistema
        // não restringe. Mas se ela TEM quadro semanal e este dia não está nele, é
        // porque ela não atende neste dia — aí sim, fechado.
        var temQuadro = await db.WorkSchedules
            .AnyAsync(w => w.ProfessionalId == profissionalId, ct);

        return temQuadro ? Expediente.Fechado : Expediente.SemRestricao;
    }

    public async Task<IReadOnlyList<TimeOnly>> HorariosLivresAsync(
        Guid profissionalId,
        DateOnly data,
        int duracaoEmMinutos,
        CancellationToken ct = default)
    {
        var expediente = await ExpedienteDeAsync(profissionalId, data, ct);

        if (!expediente.Atende || duracaoEmMinutos <= 0)
        {
            return [];
        }

        var ocupados = await OcupadosAsync(profissionalId, data, ct);
        var livres = new List<TimeOnly>();

        // Sem expediente cadastrado, agendar é livre — mas a tela precisa de horários
        // concretos para sugerir. Estes limites são só a janela de sugestão; não
        // impedem a recepção de marcar fora dela.
        var inicioDaBusca = expediente.Irrestrito ? new TimeOnly(8, 0) : expediente.Inicio;
        var fimDaBusca = expediente.Irrestrito ? new TimeOnly(20, 0) : expediente.Fim;

        for (var hora = inicioDaBusca;
             hora < fimDaBusca;
             hora = hora.AddMinutes(PassoEmMinutos))
        {
            var fim = hora.AddMinutes(duracaoEmMinutos);

            // AddMinutes dá a volta à meia-noite; um atendimento que "termina" antes de
            // começar significa que passou do fim do dia.
            if (fim <= hora)
            {
                break;
            }

            if (!expediente.Comporta(hora, fim) || (expediente.Irrestrito && fim > fimDaBusca))
            {
                continue;
            }

            var conflita = ocupados.Any(o => hora < o.Fim && fim > o.Inicio);

            if (!conflita)
            {
                livres.Add(hora);
            }
        }

        return livres;
    }

    /// <summary>O atendimento cabe no expediente E o horário está livre?</summary>
    public async Task<ResultadoDeEncaixe> PodeAgendarAsync(
        Guid profissionalId,
        DateTimeOffset inicio,
        int duracaoEmMinutos,
        Guid? ignorarAtendimento = null,
        CancellationToken ct = default)
    {
        var local = inicio.ToOffset(Fuso);
        var data = DateOnly.FromDateTime(local.DateTime);
        var horaInicio = TimeOnly.FromDateTime(local.DateTime);
        var horaFim = horaInicio.AddMinutes(duracaoEmMinutos);

        var expediente = await ExpedienteDeAsync(profissionalId, data, ct);

        if (!expediente.Atende)
        {
            return new ResultadoDeEncaixe(false, "A profissional nao atende nesta data.");
        }

        if (!expediente.Comporta(horaInicio, horaFim))
        {
            var intervalo = expediente.IntervaloInicio is { } i && expediente.IntervaloFim is { } f
                ? $" com intervalo das {i:HH\\:mm} as {f:HH\\:mm}"
                : "";

            return new ResultadoDeEncaixe(
                false,
                $"O atendimento nao cabe no expediente ({expediente.Inicio:HH\\:mm} as " +
                $"{expediente.Fim:HH\\:mm}{intervalo}). " +
                $"Este procedimento leva {duracaoEmMinutos} minutos.");
        }

        var ocupados = await OcupadosAsync(profissionalId, data, ct, ignorarAtendimento);
        var conflita = ocupados.Any(o => horaInicio < o.Fim && horaFim > o.Inicio);

        return conflita
            ? new ResultadoDeEncaixe(false, "Ja existe atendimento neste horario.")
            : new ResultadoDeEncaixe(true, null);
    }

    private async Task<List<(TimeOnly Inicio, TimeOnly Fim)>> OcupadosAsync(
        Guid profissionalId,
        DateOnly data,
        CancellationToken ct,
        Guid? ignorar = null)
    {
        var inicioDoDia = new DateTimeOffset(data.ToDateTime(TimeOnly.MinValue), Fuso);
        var fimDoDia = inicioDoDia.AddDays(1);

        var atendimentos = await db.Appointments
            .Where(a => a.ProfessionalId == profissionalId
                        && a.StartsAt >= inicioDoDia
                        && a.StartsAt < fimDoDia
                        && a.Status != AppointmentStatus.Cancelado
                        && (ignorar == null || a.Id != ignorar))
            .Select(a => new { a.StartsAt, a.EndsAt })
            .ToListAsync(ct);

        return atendimentos
            .Select(a => (
                TimeOnly.FromDateTime(a.StartsAt.ToOffset(Fuso).DateTime),
                TimeOnly.FromDateTime(a.EndsAt.ToOffset(Fuso).DateTime)))
            .ToList();
    }
}

public sealed record ResultadoDeEncaixe(bool Pode, string? Motivo);
