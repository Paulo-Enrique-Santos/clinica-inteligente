using Clinica.Domain.Professionals;
using Clinica.Domain.Tenancy;
using Clinica.Infrastructure.Persistence;
using Clinica.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/professionals/{profissionalId:guid}")
            .RequireAuthorization()
            .WithTags("Expediente");

        group.MapGet("/schedule", async (
            Guid profissionalId,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var semana = await db.WorkSchedules
                .Where(w => w.ProfessionalId == profissionalId)
                .OrderBy(w => w.DayOfWeek)
                .Select(w => new WorkScheduleResponse(
                    (int)w.DayOfWeek,
                    w.StartsAt,
                    w.EndsAt,
                    w.BreakStartsAt,
                    w.BreakEndsAt))
                .ToListAsync(ct);

            var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

            var excecoes = await db.ScheduleExceptions
                .Where(e => e.ProfessionalId == profissionalId && e.Date >= hoje)
                .OrderBy(e => e.Date)
                .Select(e => new ScheduleExceptionResponse(
                    e.Id, e.Date, e.Closed, e.StartsAt, e.EndsAt,
                    e.BreakStartsAt, e.BreakEndsAt, e.Reason))
                .ToListAsync(ct);

            return Results.Ok(new { semana, excecoes });
        })
        .WithName("ObterExpediente");

        group.MapPut("/schedule", async (
            Guid profissionalId,
            SaveWeeklyScheduleRequest request,
            ClinicaDbContext db,
            IUserContext usuario,
            CancellationToken ct) =>
        {
            if (!await PodeEditarAsync(profissionalId, db, usuario, ct))
            {
                return Results.Forbid();
            }

            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            // Substitui a semana inteira: a tela envia o quadro completo, e reconciliar
            // dia a dia daria margem a sobrar expediente de um dia que a clínica tirou.
            var atuais = await db.WorkSchedules
                .Where(w => w.ProfessionalId == profissionalId)
                .ToListAsync(ct);

            db.WorkSchedules.RemoveRange(atuais);

            foreach (var dia in request.Dias)
            {
                db.WorkSchedules.Add(new WorkSchedule
                {
                    ProfessionalId = profissionalId,
                    DayOfWeek = (DayOfWeek)dia.DayOfWeek,
                    StartsAt = dia.StartsAt,
                    EndsAt = dia.EndsAt,
                    BreakStartsAt = dia.BreakStartsAt,
                    BreakEndsAt = dia.BreakEndsAt,
                });
            }

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DefinirExpedienteSemanal");

        group.MapPost("/schedule/exceptions", async (
            Guid profissionalId,
            SaveScheduleExceptionRequest request,
            ClinicaDbContext db,
            IUserContext usuario,
            CancellationToken ct) =>
        {
            if (!await PodeEditarAsync(profissionalId, db, usuario, ct))
            {
                return Results.Forbid();
            }

            var existente = await db.ScheduleExceptions
                .FirstOrDefaultAsync(
                    e => e.ProfessionalId == profissionalId && e.Date == request.Date, ct);

            var excecao = existente ?? new ScheduleException
            {
                ProfessionalId = profissionalId,
                Date = request.Date,
            };

            excecao.Closed = request.Closed;
            excecao.StartsAt = request.StartsAt;
            excecao.EndsAt = request.EndsAt;
            excecao.BreakStartsAt = request.BreakStartsAt;
            excecao.BreakEndsAt = request.BreakEndsAt;
            excecao.Reason = request.Reason?.Trim();

            if (existente is null)
            {
                db.ScheduleExceptions.Add(excecao);
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(excecao.Id);
        })
        .WithName("DefinirExcecaoDeExpediente");

        group.MapDelete("/schedule/exceptions/{id:guid}", async (
            Guid profissionalId,
            Guid id,
            ClinicaDbContext db,
            IUserContext usuario,
            CancellationToken ct) =>
        {
            if (!await PodeEditarAsync(profissionalId, db, usuario, ct))
            {
                return Results.Forbid();
            }

            var excecao = await db.ScheduleExceptions
                .FirstOrDefaultAsync(e => e.Id == id && e.ProfessionalId == profissionalId, ct);

            if (excecao is null)
            {
                return Results.NotFound();
            }

            db.ScheduleExceptions.Remove(excecao);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("RemoverExcecaoDeExpediente");

        group.MapGet("/slots", async (
            Guid profissionalId,
            DateOnly data,
            Guid procedimentoId,
            ClinicaDbContext db,
            DisponibilidadeService disponibilidade,
            CancellationToken ct) =>
        {
            var procedimento = await db.Procedures
                .FirstOrDefaultAsync(p => p.Id == procedimentoId, ct);

            if (procedimento is null)
            {
                return Results.NotFound();
            }

            // Oferecer só horários que cabem é melhor do que aceitar qualquer um e
            // recusar depois: a recepção não precisa adivinhar que um procedimento de
            // duas horas não entra às 17h.
            var livres = await disponibilidade.HorariosLivresAsync(
                profissionalId, data, procedimento.DurationMinutes, ct);

            return Results.Ok(livres.Select(h => h.ToString("HH\\:mm")).ToArray());
        })
        .WithName("ListarHorariosLivres");

        return app;
    }

    /// <summary>
    /// Dona e recepção organizam o expediente de qualquer profissional; a doutora mexe
    /// apenas no próprio. Ela precisa poder avisar que sai mais cedo sem depender de
    /// outra pessoa — mas mudar o expediente alheio é decisão de quem organiza a agenda.
    /// </summary>
    private static async Task<bool> PodeEditarAsync(
        Guid profissionalId,
        ClinicaDbContext db,
        IUserContext usuario,
        CancellationToken ct)
    {
        if (usuario.HasRole("OWNER") || usuario.HasRole("SECRETARY"))
        {
            return true;
        }

        if (!usuario.HasRole("DOCTOR"))
        {
            return false;
        }

        return await db.Professionals.AnyAsync(
            p => p.Id == profissionalId && p.KeycloakUserId == usuario.UserId, ct);
    }
}

public record WorkScheduleResponse(
    int DayOfWeek,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    TimeOnly? BreakStartsAt,
    TimeOnly? BreakEndsAt);

public record ScheduleExceptionResponse(
    Guid Id,
    DateOnly Date,
    bool Closed,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    TimeOnly? BreakStartsAt,
    TimeOnly? BreakEndsAt,
    string? Reason);

public record SaveWeeklyScheduleRequest(WorkScheduleResponse[] Dias)
{
    public Dictionary<string, string[]>? Validate()
    {
        var erros = new Dictionary<string, string[]>();

        foreach (var dia in Dias)
        {
            var nome = $"dia{dia.DayOfWeek}";

            if (dia.EndsAt <= dia.StartsAt)
            {
                erros[nome] = ["O fim do expediente deve ser depois do inicio."];
                continue;
            }

            var temInicio = dia.BreakStartsAt is not null;
            var temFim = dia.BreakEndsAt is not null;

            // Meio intervalo é pior do que nenhum: o sistema não saberia quando a
            // profissional volta.
            if (temInicio != temFim)
            {
                erros[nome] = ["Informe inicio e fim do intervalo, ou nenhum dos dois."];
            }
            else if (dia.BreakStartsAt is { } bi && dia.BreakEndsAt is { } bf)
            {
                if (bf <= bi)
                {
                    erros[nome] = ["O fim do intervalo deve ser depois do inicio."];
                }
                else if (bi < dia.StartsAt || bf > dia.EndsAt)
                {
                    erros[nome] = ["O intervalo precisa estar dentro do expediente."];
                }
            }
        }

        return erros.Count == 0 ? null : erros;
    }
}

public record SaveScheduleExceptionRequest(
    DateOnly Date,
    bool Closed,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    TimeOnly? BreakStartsAt,
    TimeOnly? BreakEndsAt,
    string? Reason);
