using Clinica.Domain.Payments;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .RequireAuthorization(p => p.RequireRole("OWNER", "FINANCE"))
            .WithTags("Financeiro");

        group.MapGet("/", async (
            string? status,
            bool? somenteVencidos,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = db.Payments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<PaymentStatus>(status, ignoreCase: true, out var filtro))
            {
                query = query.Where(p => p.Status == filtro);
            }

            if (somenteVencidos == true)
            {
                query = query.Where(p => p.Status == PaymentStatus.Pendente && p.DueDate < hoje);
            }

            var cobrancas = await query
                .OrderBy(p => p.DueDate)
                .Select(p => new PaymentResponse(
                    p.Id,
                    p.AppointmentId,
                    p.TreatmentPlanId,
                    // Cobrança de protocolo não tem atendimento amarrado: o nome vem do
                    // paciente do protocolo.
                    p.Appointment != null
                        ? p.Appointment.Patient!.FullName
                        : db.TreatmentPlans
                            .Where(t => t.Id == p.TreatmentPlanId)
                            .Select(t => t.Patient!.FullName)
                            .FirstOrDefault() ?? "—",
                    p.Appointment != null ? p.Appointment.Patient!.PhoneE164 : "",
                    p.Appointment != null
                        ? p.Appointment.Procedure!.Name
                        : (p.InstallmentCount > 1
                            ? $"Protocolo — parcela {p.InstallmentNumber}/{p.InstallmentCount}"
                            : "Protocolo"),
                    p.Appointment != null ? p.Appointment.StartsAt : (DateTimeOffset?)null,
                    p.Amount,
                    p.DueDate,
                    p.Status.ToString(),
                    p.Status == PaymentStatus.Pendente && p.DueDate < hoje,
                    p.Method == null ? null : p.Method.ToString(),
                    p.PaidAt,
                    p.InstallmentNumber,
                    p.InstallmentCount))
                .ToListAsync(ct);

            return Results.Ok(cobrancas);
        })
        .WithName("ListarCobrancas");

        group.MapPost("/", async (CreatePaymentRequest request, ClinicaDbContext db, CancellationToken ct) =>
        {
            if (request.Amount <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Amount)] = ["Valor deve ser maior que zero."],
                });
            }

            var atendimento = await db.Appointments
                .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct);

            if (atendimento is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.AppointmentId)] = ["Atendimento nao encontrado nesta clinica."],
                });
            }

            var cobranca = new Payment
            {
                AppointmentId = atendimento.Id,
                Amount = request.Amount,
                DueDate = request.DueDate,
                Notes = request.Notes?.Trim(),
                Status = PaymentStatus.Pendente,
            };

            db.Payments.Add(cobranca);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/payments/{cobranca.Id}", cobranca.Id);
        })
        .WithName("CriarCobranca");

        group.MapPost("/{id:guid}/baixa", async (
            Guid id,
            SettlePaymentRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var metodo))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Method)] =
                        [$"Metodo invalido. Use um de: {string.Join(", ", Enum.GetNames<PaymentMethod>())}."],
                });
            }

            var cobranca = await db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (cobranca is null)
            {
                return Results.NotFound();
            }

            if (cobranca.Status == PaymentStatus.Pago)
            {
                // Idempotente: dar baixa duas vezes não é erro, mas também não sobrescreve
                // a data do pagamento original.
                return Results.NoContent();
            }

            cobranca.Status = PaymentStatus.Pago;
            cobranca.Method = metodo;
            cobranca.PaidAt = request.PaidAt ?? DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DarBaixaEmCobranca");

        group.MapGet("/resumo", async (ClinicaDbContext db, CancellationToken ct) =>
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
            var inicioDaSerie = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-5);

            var cobrancas = await db.Payments
                .Where(p => p.Status != PaymentStatus.Cancelado)
                .Select(p => new { p.Amount, p.DueDate, p.Status, p.PaidAt })
                .ToListAsync(ct);

            var recebido = cobrancas.Where(c => c.Status == PaymentStatus.Pago).Sum(c => c.Amount);
            var pendente = cobrancas.Where(c => c.Status == PaymentStatus.Pendente).Sum(c => c.Amount);
            var vencido = cobrancas
                .Where(c => c.Status == PaymentStatus.Pendente && c.DueDate < hoje)
                .Sum(c => c.Amount);
            var estornado = cobrancas.Where(c => c.Status == PaymentStatus.Estornado).Sum(c => c.Amount);

            // Série dos últimos seis meses. Recebido usa a data do pagamento; a receber
            // usa o vencimento — são perguntas diferentes: uma é "quanto entrou", a
            // outra "quanto deveria entrar".
            var serie = Enumerable.Range(0, 6)
                .Select(i => inicioDaSerie.AddMonths(i))
                .Select(mes =>
                {
                    var fim = mes.AddMonths(1);

                    return new
                    {
                        mes = mes.ToString("yyyy-MM"),
                        recebido = cobrancas
                            .Where(c => c.Status == PaymentStatus.Pago
                                        && c.PaidAt != null
                                        && DateOnly.FromDateTime(c.PaidAt.Value.UtcDateTime) >= mes
                                        && DateOnly.FromDateTime(c.PaidAt.Value.UtcDateTime) < fim)
                            .Sum(c => c.Amount),
                        aReceber = cobrancas
                            .Where(c => c.Status == PaymentStatus.Pendente
                                        && c.DueDate >= mes && c.DueDate < fim)
                            .Sum(c => c.Amount),
                    };
                })
                .ToList();

            return Results.Ok(new { recebido, pendente, vencido, estornado, serie });
        })
        .WithName("ResumoFinanceiro");

        group.MapPost("/{id:guid}/estornar", async (
            Guid id,
            EstornoRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var cobranca = await db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

            if (cobranca is null)
            {
                return Results.NotFound();
            }

            if (cobranca.Status != PaymentStatus.Pago)
            {
                return Results.Problem(
                    title: "Cobranca nao esta paga",
                    detail: "So se estorna o que entrou.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // A data do pagamento permanece: saber QUANDO entrou continua importando
            // depois do estorno, tanto para conciliar com a maquininha quanto para
            // explicar à paciente.
            cobranca.Status = PaymentStatus.Estornado;
            cobranca.Notes = string.IsNullOrWhiteSpace(request.Motivo)
                ? cobranca.Notes
                : $"{cobranca.Notes} | Estorno: {request.Motivo.Trim()}".TrimStart('|', ' ');

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("EstornarCobranca");

        group.MapPost("/{id:guid}/cancelar", async (
            Guid id,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var cobranca = await db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (cobranca is null)
            {
                return Results.NotFound();
            }

            if (cobranca.Status == PaymentStatus.Pago)
            {
                return Results.Problem(
                    title: "Cobranca ja paga",
                    detail: "Cobranca paga nao se cancela: registre um estorno.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            cobranca.Status = PaymentStatus.Cancelado;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("CancelarCobranca");

        return app;
    }
}

public record CreatePaymentRequest(
    Guid AppointmentId,
    decimal Amount,
    DateOnly DueDate,
    string? Notes);

public record SettlePaymentRequest(string Method, DateTimeOffset? PaidAt);

public record EstornoRequest(string? Motivo);

public record PaymentResponse(
    Guid Id,
    Guid? AppointmentId,
    Guid? TreatmentPlanId,
    string PatientName,
    string PatientPhone,
    string ProcedureName,
    DateTimeOffset? AppointmentAt,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    /// <summary>Calculado na consulta, nunca gravado — status vencido no banco envelhece.</summary>
    bool Overdue,
    string? Method,
    DateTimeOffset? PaidAt,
    int? InstallmentNumber,
    int? InstallmentCount);
