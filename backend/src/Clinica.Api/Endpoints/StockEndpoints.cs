using Clinica.Domain.Stock;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/stock")
            .RequireAuthorization()
            .WithTags("Estoque");

        group.MapGet("/", async (
            string? q,
            bool? incluirInativos,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var itens = db.StockItems.AsQueryable();

            if (incluirInativos != true)
            {
                itens = itens.Where(i => i.Active);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                itens = itens.Where(i => EF.Functions.ILike(i.Name, $"%{q.Trim()}%"));
            }

            // O saldo é somado das movimentações, não lido de uma coluna. Ver o comentário
            // em StockItem: coluna de saldo é rápida de ler e mente no dia em que alguém
            // gravar movimentação por um caminho que esqueceu de atualizá-la.
            var resultado = await itens
                .OrderBy(i => i.Name)
                .Select(i => new StockItemResponse(
                    i.Id,
                    i.Name,
                    i.Unit,
                    db.StockMovements
                        .Where(m => m.StockItemId == i.Id)
                        .Sum(m => m.Type == StockMovementType.Entrada
                            ? m.Quantity
                            : m.Type == StockMovementType.Saida
                                ? -m.Quantity
                                : m.Quantity),
                    i.MinimumQuantity,
                    i.Active))
                .ToListAsync(ct);

            return Results.Ok(resultado);
        })
        .WithName("ListarEstoque");

        group.MapPost("/", async (SaveStockItemRequest request, ClinicaDbContext db, CancellationToken ct) =>
        {
            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            var item = new StockItem
            {
                Name = request.Name.Trim(),
                Unit = request.Unit.Trim(),
                MinimumQuantity = request.MinimumQuantity,
                Active = request.Active ?? true,
            };

            db.StockItems.Add(item);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/stock/{item.Id}", item.Id);
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "FINANCE"))
        .WithName("CriarItemDeEstoque");

        group.MapGet("/{id:guid}/movements", async (Guid id, ClinicaDbContext db, CancellationToken ct) =>
        {
            var movimentacoes = await db.StockMovements
                .Where(m => m.StockItemId == id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new StockMovementResponse(
                    m.Id,
                    m.Type.ToString(),
                    m.Quantity,
                    m.AppointmentId,
                    m.Reason,
                    m.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(movimentacoes);
        })
        .WithName("ListarMovimentacoesDoItem");

        group.MapPost("/{id:guid}/movements", async (
            Guid id,
            CreateStockMovementRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<StockMovementType>(request.Type, ignoreCase: true, out var tipo))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Type)] =
                        [$"Tipo invalido. Use um de: {string.Join(", ", Enum.GetNames<StockMovementType>())}."],
                });
            }

            if (tipo != StockMovementType.Ajuste && request.Quantity <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Quantity)] = ["Entrada e saida exigem quantidade positiva."],
                });
            }

            if (tipo == StockMovementType.Ajuste && request.Quantity == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Quantity)] = ["Ajuste de zero nao ajusta nada."],
                });
            }

            if (tipo == StockMovementType.Ajuste && string.IsNullOrWhiteSpace(request.Reason))
            {
                // Ajuste sem motivo vira buraco inexplicável na auditoria de estoque.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Reason)] = ["Ajuste exige motivo."],
                });
            }

            if (!await db.StockItems.AnyAsync(i => i.Id == id, ct))
            {
                return Results.NotFound();
            }

            var movimentacao = new StockMovement
            {
                StockItemId = id,
                Type = tipo,
                Quantity = request.Quantity,
                AppointmentId = request.AppointmentId,
                Reason = request.Reason?.Trim(),
            };

            db.StockMovements.Add(movimentacao);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/stock/{id}/movements/{movimentacao.Id}", movimentacao.Id);
        })
        .WithName("RegistrarMovimentacao");

        return app;
    }
}

public record SaveStockItemRequest(string Name, string Unit, decimal MinimumQuantity, bool? Active)
{
    public Dictionary<string, string[]>? Validate()
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            erros[nameof(Name)] = ["Nome e obrigatorio."];
        }

        if (string.IsNullOrWhiteSpace(Unit))
        {
            erros[nameof(Unit)] = ["Unidade e obrigatoria (ml, un, g, caixa)."];
        }

        if (MinimumQuantity < 0)
        {
            erros[nameof(MinimumQuantity)] = ["Quantidade minima nao pode ser negativa."];
        }

        return erros.Count == 0 ? null : erros;
    }
}

public record CreateStockMovementRequest(
    string Type,
    decimal Quantity,
    Guid? AppointmentId,
    string? Reason);

public record StockItemResponse(
    Guid Id,
    string Name,
    string Unit,
    decimal Balance,
    decimal MinimumQuantity,
    bool Active)
{
    public bool BelowMinimum => Balance < MinimumQuantity;
}

public record StockMovementResponse(
    Guid Id,
    string Type,
    decimal Quantity,
    Guid? AppointmentId,
    string? Reason,
    DateTimeOffset CreatedAt);
