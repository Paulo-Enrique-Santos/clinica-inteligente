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
            string? modo,
            bool? incluirInativos,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var query = db.StockItems.AsQueryable();

            if (incluirInativos != true)
            {
                query = query.Where(i => i.Active);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(i => EF.Functions.ILike(i.Name, $"%{q.Trim()}%"));
            }

            // O fechamento do atendimento só oferece o que a profissional consegue medir.
            if (!string.IsNullOrWhiteSpace(modo)
                && Enum.TryParse<StockControlMode>(modo, true, out var filtro))
            {
                query = query.Where(i => i.ControlMode == filtro);
            }

            var itens = await query.OrderBy(i => i.Name).ToListAsync(ct);

            var saldos = await db.StockMovements
                .Where(m => itens.Select(i => i.Id).Contains(m.StockItemId))
                .GroupBy(m => m.StockItemId)
                .Select(g => new
                {
                    Id = g.Key,
                    Saldo = g.Sum(m => m.Type == StockMovementType.Entrada
                        ? m.Quantity
                        : m.Type == StockMovementType.Saida
                            ? -m.Quantity
                            : m.Quantity),
                })
                .ToDictionaryAsync(x => x.Id, x => x.Saldo, ct);

            return Results.Ok(itens.Select(i =>
            {
                var saldo = saldos.GetValueOrDefault(i.Id);
                var (fechadas, aberto) = i.Repartir(saldo);

                return new StockItemResponse(
                    i.Id,
                    i.Name,
                    i.Unit,
                    i.PurchaseUnit,
                    i.ContentPerUnit,
                    i.ControlMode.ToString(),
                    saldo,
                    fechadas,
                    aberto,
                    i.MinimumQuantity,
                    i.Active);
            }).ToList());
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
                PurchaseUnit = request.PurchaseUnit.Trim(),
                ContentPerUnit = request.ContentPerUnit,
                ControlMode = Enum.Parse<StockControlMode>(request.ControlMode, true),
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
                    m.Id, m.Type.ToString(), m.Quantity, m.AppointmentId, m.Reason, m.CreatedAt))
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

            var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            // Entrada é digitada como se compra — "2 frascos" — e convertida aqui.
            // Deixar a conta para a tela abriria espaço para cada lugar multiplicar de
            // um jeito, e para o saldo divergir por caminho de entrada.
            var quantidade = request.InPurchaseUnits
                ? request.Quantity * item.ContentPerUnit
                : request.Quantity;

            if (tipo != StockMovementType.Ajuste && quantidade <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Quantity)] = ["Entrada e saida exigem quantidade positiva."],
                });
            }

            if (tipo == StockMovementType.Ajuste && quantidade == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Quantity)] = ["Ajuste de zero nao ajusta nada."],
                });
            }

            if (tipo == StockMovementType.Ajuste && string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Reason)] = ["Ajuste exige motivo."],
                });
            }

            db.StockMovements.Add(new StockMovement
            {
                StockItemId = id,
                Type = tipo,
                Quantity = quantidade,
                AppointmentId = request.AppointmentId,
                Reason = request.Reason?.Trim(),
            });

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("RegistrarMovimentacao");

        group.MapPost("/{id:guid}/abrir", async (Guid id, ClinicaDbContext db, CancellationToken ct) =>
        {
            var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == id, ct);

            if (item is null)
            {
                return Results.NotFound();
            }

            // Abrir dá baixa da embalagem inteira. É o único registro que existe para
            // esse tipo de insumo: o que acontece depois — quanto de creme se passa,
            // quantas luvas rasgam — a clínica não mede, e inventar número seria pior.
            db.StockMovements.Add(new StockMovement
            {
                StockItemId = id,
                Type = StockMovementType.Saida,
                Quantity = item.ContentPerUnit,
                Reason = $"Abertura de {item.PurchaseUnit}",
            });

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("AbrirEmbalagem");

        return app;
    }
}

public record SaveStockItemRequest(
    string Name,
    string Unit,
    string PurchaseUnit,
    decimal ContentPerUnit,
    string ControlMode,
    decimal MinimumQuantity,
    bool? Active)
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
            erros[nameof(Unit)] = ["Unidade de consumo e obrigatoria (ml, par, un)."];
        }

        if (string.IsNullOrWhiteSpace(PurchaseUnit))
        {
            erros[nameof(PurchaseUnit)] = ["Unidade de compra e obrigatoria (frasco, caixa)."];
        }

        if (ContentPerUnit <= 0)
        {
            erros[nameof(ContentPerUnit)] = ["Conteudo por embalagem deve ser maior que zero."];
        }

        if (!Enum.TryParse<StockControlMode>(ControlMode, true, out _))
        {
            erros[nameof(ControlMode)] =
                [$"Modo invalido. Use: {string.Join(", ", Enum.GetNames<StockControlMode>())}."];
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
    string? Reason,
    /// <summary>Quantidade veio em embalagens (2 frascos) em vez de conteúdo (20 ml).</summary>
    bool InPurchaseUnits = false);

public record StockItemResponse(
    Guid Id,
    string Name,
    string Unit,
    string PurchaseUnit,
    decimal ContentPerUnit,
    string ControlMode,
    decimal Balance,
    int ClosedPackages,
    decimal OpenRemainder,
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
