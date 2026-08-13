using Clinica.Domain.Appointments;
using Clinica.Domain.Tenancy;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinica.Api.Endpoints;

public static class AppointmentEndpoints
{
    /// <summary>Código do Postgres para violação de constraint EXCLUDE.</summary>
    private const string ExclusionViolation = "23P01";

    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/appointments")
            .RequireAuthorization()
            .WithTags("Agenda");

        group.MapGet("/", async (
            DateTimeOffset? de,
            DateTimeOffset? ate,
            Guid? profissionalId,
            ClinicaDbContext db,
            IUserContext usuario,
            CancellationToken ct) =>
        {
            // Sem janela informada, o padrão é o dia de hoje — é o que a recepção
            // quer ver ao abrir a tela.
            var inicio = de ?? DateTimeOffset.UtcNow.Date;
            var fim = ate ?? inicio.AddDays(1);

            var query = db.Appointments
                .Where(a => a.StartsAt >= inicio && a.StartsAt < fim);

            // A doutora vê a própria agenda e só. Quem organiza a agenda da clínica
            // — recepção e dona — escolhe de quem quer ver.
            //
            // A restrição é aplicada no servidor, e não escondendo o filtro na tela:
            // do contrário bastaria alterar a query string para ler a agenda de
            // outra profissional, com nome e telefone das pacientes dela.
            if (SoVeAPropriaAgenda(usuario))
            {
                var minhaFicha = await db.Professionals
                    .Where(p => p.KeycloakUserId == usuario.UserId)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(ct);

                // Sem vínculo, nenhuma agenda. Negar por padrão, como no resto do sistema.
                query = query.Where(a => a.ProfessionalId == (minhaFicha ?? Guid.Empty));
            }
            else if (profissionalId is { } pid)
            {
                query = query.Where(a => a.ProfessionalId == pid);
            }

            var agenda = await query
                .OrderBy(a => a.StartsAt)
                .Select(a => new AppointmentResponse(
                    a.Id,
                    a.PatientId,
                    a.Patient!.FullName,
                    a.Patient.PhoneE164,
                    a.ProcedureId,
                    a.Procedure!.Name,
                    a.ProfessionalId,
                    a.Professional!.DisplayName,
                    a.StartsAt,
                    a.EndsAt,
                    a.Status.ToString(),
                    a.Price,
                    a.Notes))
                .ToListAsync(ct);

            return Results.Ok(agenda);
        })
        .WithName("ListarAgenda");

        group.MapPost("/", async (
            CreateAppointmentRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var procedimento = await db.Procedures.FirstOrDefaultAsync(p => p.Id == request.ProcedureId, ct);
            if (procedimento is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.ProcedureId)] = ["Procedimento nao encontrado."],
                });
            }

            // Existência de paciente e profissional é verificada pela FK do banco, mas
            // conferimos aqui para devolver 400 com mensagem, em vez de 500.
            var pacienteExiste = await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct);
            var profissionalExiste = await db.Professionals.AnyAsync(p => p.Id == request.ProfessionalId, ct);

            if (!pacienteExiste || !profissionalExiste)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["referencia"] = ["Paciente ou profissional nao encontrado nesta clinica."],
                });
            }

            var atendimento = new Appointment
            {
                PatientId = request.PatientId,
                ProcedureId = request.ProcedureId,
                ProfessionalId = request.ProfessionalId,
                StartsAt = request.StartsAt,
                // Congelados no momento do agendamento: se a clínica mudar duração ou
                // preço depois, o que foi combinado com a paciente permanece.
                EndsAt = request.StartsAt.AddMinutes(procedimento.DurationMinutes),
                Price = request.Price ?? procedimento.Price,
                Notes = request.Notes?.Trim(),
                Status = AppointmentStatus.Agendado,
            };

            db.Appointments.Add(atendimento);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (EhConflitoDeHorario(e))
            {
                return ConflitoDeHorario(atendimento.StartsAt, atendimento.EndsAt);
            }

            return Results.Created($"/appointments/{atendimento.Id}", atendimento.Id);
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY"))
        .WithName("CriarAtendimento");

        group.MapPost("/{id:guid}/remarcar", async (
            Guid id,
            RescheduleRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var atendimento = await db.Appointments
                .Include(a => a.Procedure)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (atendimento is null)
            {
                return Results.NotFound();
            }

            if (atendimento.Status is AppointmentStatus.Realizado or AppointmentStatus.Cancelado)
            {
                return Results.Problem(
                    title: "Atendimento nao pode ser remarcado",
                    detail: $"O atendimento esta como '{atendimento.Status}'.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var duracao = atendimento.EndsAt - atendimento.StartsAt;
            atendimento.StartsAt = request.NewStartsAt;
            atendimento.EndsAt = request.NewStartsAt.Add(duracao);
            // Remarcou, precisa confirmar de novo — a paciente ainda não sabe do horário novo.
            atendimento.Status = AppointmentStatus.Agendado;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (EhConflitoDeHorario(e))
            {
                return ConflitoDeHorario(atendimento.StartsAt, atendimento.EndsAt);
            }

            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY"))
        .WithName("RemarcarAtendimento");

        group.MapPost("/{id:guid}/status", async (
            Guid id,
            ChangeStatusRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<AppointmentStatus>(request.Status, ignoreCase: true, out var novo))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Status)] =
                        [$"Status invalido. Use um de: {string.Join(", ", Enum.GetNames<AppointmentStatus>())}."],
                });
            }

            var atendimento = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (atendimento is null)
            {
                return Results.NotFound();
            }

            atendimento.Status = novo;

            if (novo == AppointmentStatus.Cancelado)
            {
                atendimento.CancellationReason = request.Reason?.Trim();
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (EhConflitoDeHorario(e))
            {
                // Acontece ao reativar um atendimento cancelado cujo horário já foi
                // ocupado por outra pessoa nesse meio-tempo.
                return ConflitoDeHorario(atendimento.StartsAt, atendimento.EndsAt);
            }

            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY", "DOCTOR"))
        .WithName("AlterarStatusDoAtendimento");

        return app;
    }

    /// <summary>
    /// Doutora sem papel de organização enxerga apenas a própria agenda.
    ///
    /// Quem acumula DOCTOR com OWNER ou SECRETARY (comum em clínica pequena, onde a dona
    /// também atende) continua vendo tudo — senão a dona perderia a visão do próprio
    /// negócio ao ganhar o papel de quem atende.
    /// </summary>
    private static bool SoVeAPropriaAgenda(IUserContext usuario) =>
        usuario.HasRole("DOCTOR")
        && !usuario.HasRole("OWNER")
        && !usuario.HasRole("SECRETARY");

    /// <summary>
    /// A checagem de conflito vive no banco (constraint EXCLUDE), não numa consulta
    /// prévia. Consultar antes de gravar tem janela de corrida: duas atendentes ao
    /// telefone ao mesmo tempo recebem "está livre" e ambas gravam. Aqui a gente
    /// apenas traduz o erro do banco para uma resposta que a recepção entende.
    /// </summary>
    private static bool EhConflitoDeHorario(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: ExclusionViolation };

    private static IResult ConflitoDeHorario(DateTimeOffset inicio, DateTimeOffset fim) =>
        Results.Problem(
            title: "Horario ja ocupado",
            detail: $"Esta profissional ja tem atendimento entre {inicio:dd/MM HH:mm} e {fim:HH:mm}.",
            statusCode: StatusCodes.Status409Conflict);
}

public record CreateAppointmentRequest(
    Guid PatientId,
    Guid ProcedureId,
    Guid ProfessionalId,
    DateTimeOffset StartsAt,
    decimal? Price,
    string? Notes);

public record RescheduleRequest(DateTimeOffset NewStartsAt);

public record ChangeStatusRequest(string Status, string? Reason);

public record AppointmentResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientPhone,
    Guid ProcedureId,
    string ProcedureName,
    Guid ProfessionalId,
    string ProfessionalName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    decimal Price,
    string? Notes);
