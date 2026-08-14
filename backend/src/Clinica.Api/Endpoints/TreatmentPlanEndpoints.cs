using Clinica.Domain.Payments;
using Clinica.Domain.Tenancy;
using Clinica.Domain.Treatments;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class TreatmentPlanEndpoints
{
    public static IEndpointRouteBuilder MapTreatmentPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/treatment-plans")
            .RequireAuthorization()
            .WithTags("Protocolos");

        group.MapGet("/", async (
            Guid? pacienteId,
            string? status,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var query = db.TreatmentPlans.AsQueryable();

            if (pacienteId is { } pid)
            {
                query = query.Where(p => p.PatientId == pid);
            }

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<TreatmentPlanStatus>(status, true, out var filtro))
            {
                query = query.Where(p => p.Status == filtro);
            }

            var protocolos = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new TreatmentPlanResponse(
                    p.Id,
                    p.PatientId,
                    p.Patient!.FullName,
                    p.ProfessionalId,
                    p.Professional!.DisplayName,
                    p.Status.ToString(),
                    p.Notes,
                    p.CreatedAt,
                    p.ApprovedAt,
                    p.Items
                        .OrderBy(i => i.StartAfterDays)
                        .Select(i => new PlanItemResponse(
                            i.Id,
                            i.ProcedureId,
                            i.Procedure!.Name,
                            i.Sessions,
                            i.IntervalDays,
                            i.StartAfterDays,
                            i.UnitPrice,
                            i.UnitPrice * i.Sessions,
                            i.Status.ToString(),
                            i.Notes))
                        .ToList()))
                .ToListAsync(ct);

            return Results.Ok(protocolos);
        })
        .WithName("ListarProtocolos");

        group.MapPost("/", async (
            CreateTreatmentPlanRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (request.Items is null || request.Items.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = ["Um protocolo sem procedimento nao propoe nada."],
                });
            }

            var idsProcedimentos = request.Items.Select(i => i.ProcedureId).Distinct().ToList();

            var precos = await db.Procedures
                .Where(p => idsProcedimentos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Price, ct);

            if (precos.Count != idsProcedimentos.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = ["Algum procedimento nao existe nesta clinica."],
                });
            }

            if (!await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct) ||
                !await db.Professionals.AnyAsync(p => p.Id == request.ProfessionalId, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["referencia"] = ["Paciente ou profissional nao encontrado nesta clinica."],
                });
            }

            var protocolo = new TreatmentPlan
            {
                PatientId = request.PatientId,
                ProfessionalId = request.ProfessionalId,
                OriginAppointmentId = request.OriginAppointmentId,
                Notes = request.Notes?.Trim(),
                Status = TreatmentPlanStatus.Proposto,
                Items = request.Items.Select(i => new PlanItem
                {
                    ProcedureId = i.ProcedureId,
                    Sessions = Math.Max(1, i.Sessions),
                    IntervalDays = i.IntervalDays,
                    StartAfterDays = Math.Max(0, i.StartAfterDays),
                    // Preço congelado na prescrição: entre a avaliação e o fechamento a
                    // tabela pode mudar, e vale o que foi conversado com a paciente.
                    UnitPrice = precos[i.ProcedureId],
                    Notes = i.Notes?.Trim(),
                    Status = PlanItemStatus.Proposto,
                }).ToList(),
            };

            db.TreatmentPlans.Add(protocolo);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/treatment-plans/{protocolo.Id}", protocolo.Id);
        })
        // A doutora prescreve. Quem fecha o valor é a recepção — por isso ela não
        // aparece no orçamento abaixo.
        .RequireAuthorization(p => p.RequireRole("OWNER", "DOCTOR"))
        .WithName("CriarProtocolo");

        group.MapPost("/{id:guid}/orcamento", async (
            Guid id,
            ApprovePlanRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var protocolo = await db.TreatmentPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (protocolo is null)
            {
                return Results.NotFound();
            }

            if (protocolo.Status == TreatmentPlanStatus.Aprovado)
            {
                return Results.Problem(
                    title: "Protocolo ja aprovado",
                    detail: "Este protocolo ja gerou cobrancas.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var aceitos = (request.AcceptedItemIds ?? []).ToHashSet();

            foreach (var item in protocolo.Items)
            {
                // Recusado, não apagado: a prescrição da doutora continua registrada, e
                // vira o dado de quanto do que se prescreve efetivamente vira venda.
                item.Status = aceitos.Contains(item.Id)
                    ? PlanItemStatus.Aceito
                    : PlanItemStatus.Recusado;
            }

            var total = protocolo.Items
                .Where(i => i.Status == PlanItemStatus.Aceito)
                .Sum(i => i.Total);

            if (total <= 0)
            {
                protocolo.Status = TreatmentPlanStatus.Recusado;
                await db.SaveChangesAsync(ct);

                return Results.Ok(new { total = 0m, parcelas = Array.Empty<object>() });
            }

            if (!Enum.TryParse<FormaDePagamento>(request.Forma, true, out var forma))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Forma)] =
                        [$"Forma invalida. Use: {string.Join(", ", Enum.GetNames<FormaDePagamento>())}."],
                });
            }

            var parcelas = PlanoDePagamento.Gerar(
                total,
                forma,
                request.PrimeiroVencimento,
                request.Parcelas ?? 1,
                request.Sinal ?? 0);

            if (!Enum.TryParse<PaymentMethod>(request.Meio, true, out var meio))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Meio)] =
                        [$"Meio invalido. Use: {string.Join(", ", Enum.GetNames<PaymentMethod>())}."],
                });
            }

            var agora = DateTimeOffset.UtcNow;

            foreach (var parcela in parcelas)
            {
                // Dinheiro e cartão entram efetivados: o dinheiro já passou pela
                // maquininha ou pela gaveta, e ficar pendente obrigaria a recepção a dar
                // baixa manual em algo que já aconteceu.
                //
                // PIX parcelado é o único que fica pendente de verdade — cada parcela
                // depende de a paciente lembrar de pagar. O sinal, quando existe, é
                // pago na hora e por isso já nasce efetivado.
                var jaEfetivada = meio != PaymentMethod.Pix
                    || forma == FormaDePagamento.AVista
                    || (forma == FormaDePagamento.SinalMaisParcelas && parcela.Numero == 1);

                db.Payments.Add(new Payment
                {
                    TreatmentPlanId = protocolo.Id,
                    Amount = parcela.Valor,
                    DueDate = parcela.Vencimento,
                    InstallmentNumber = parcela.Numero,
                    InstallmentCount = parcela.Total,
                    Status = jaEfetivada ? PaymentStatus.Pago : PaymentStatus.Pendente,
                    Method = jaEfetivada ? meio : null,
                    PaidAt = jaEfetivada ? agora : null,
                    Notes = request.Observacoes?.Trim(),
                });
            }

            protocolo.Status = TreatmentPlanStatus.Aprovado;
            protocolo.ApprovedAt = DateTimeOffset.UtcNow;

            // Itens, protocolo e cobranças gravam juntos: protocolo aprovado sem
            // cobrança seria trabalho feito e dinheiro esquecido.
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                total,
                parcelas = parcelas.Select(p => new { p.Numero, p.Total, p.Valor, p.Vencimento }),
            });
        })
        // Fechar valor e forma de pagamento é da recepção e do financeiro.
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY", "FINANCE"))
        .WithName("FecharOrcamento");

        group.MapPost("/{id:guid}/cancelar", async (
            Guid id,
            CancelPlanRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var protocolo = await db.TreatmentPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

            if (protocolo is null)
            {
                return Results.NotFound();
            }

            if (protocolo.Status == TreatmentPlanStatus.Cancelado)
            {
                return Results.NoContent();
            }

            protocolo.Status = TreatmentPlanStatus.Cancelado;
            protocolo.Notes = string.IsNullOrWhiteSpace(request.Motivo)
                ? protocolo.Notes
                : $"{protocolo.Notes} | Cancelado: {request.Motivo.Trim()}".TrimStart('|', ' ');

            // As cobranças pendentes caem junto: manter cobrança viva de tratamento
            // cancelado é a receita para a clínica cobrar alguém por algo que não vai
            // acontecer.
            //
            // O que já foi pago NÃO é tocado. Devolver dinheiro é decisão do financeiro,
            // com estorno registrado — não efeito colateral de cancelar um protocolo.
            var pendentes = await db.Payments
                .Where(p => p.TreatmentPlanId == id && p.Status == PaymentStatus.Pendente)
                .ToListAsync(ct);

            foreach (var cobranca in pendentes)
            {
                cobranca.Status = PaymentStatus.Cancelado;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { cobrancasCanceladas = pendentes.Count });
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY", "FINANCE"))
        .WithName("CancelarProtocolo");

        return app;
    }
}

public record CancelPlanRequest(string? Motivo);

public record CreatePlanItemRequest(
    Guid ProcedureId,
    int Sessions,
    int? IntervalDays,
    int StartAfterDays,
    string? Notes);

public record CreateTreatmentPlanRequest(
    Guid PatientId,
    Guid ProfessionalId,
    Guid? OriginAppointmentId,
    string? Notes,
    CreatePlanItemRequest[]? Items);

public record ApprovePlanRequest(
    Guid[]? AcceptedItemIds,
    string Forma,
    /// <summary>Pix, Dinheiro, Credito, Debito, Transferencia. Decide o que já entra efetivado.</summary>
    string Meio,
    DateOnly PrimeiroVencimento,
    int? Parcelas,
    decimal? Sinal,
    string? Observacoes);

public record PlanItemResponse(
    Guid Id,
    Guid ProcedureId,
    string ProcedureName,
    int Sessions,
    int? IntervalDays,
    int StartAfterDays,
    decimal UnitPrice,
    decimal Total,
    string Status,
    string? Notes);

public record TreatmentPlanResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid ProfessionalId,
    string ProfessionalName,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    List<PlanItemResponse> Items);
