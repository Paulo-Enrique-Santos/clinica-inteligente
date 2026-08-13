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
                    p.Appointment!.Patient!.FullName,
                    p.Appointment.Patient.PhoneE164,
                    p.Appointment.Procedure!.Name,
                    p.Appointment.StartsAt,
                    p.Amount,
                    p.DueDate,
                    p.Status.ToString(),
                    p.Status == PaymentStatus.Pendente && p.DueDate < hoje,
                    p.Method == null ? null : p.Method.ToString(),
                    p.PaidAt))
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

public record PaymentResponse(
    Guid Id,
    Guid AppointmentId,
    string PatientName,
    string PatientPhone,
    string ProcedureName,
    DateTimeOffset AppointmentAt,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    /// <summary>Calculado na consulta, nunca gravado — status vencido no banco envelhece.</summary>
    bool Overdue,
    string? Method,
    DateTimeOffset? PaidAt);
