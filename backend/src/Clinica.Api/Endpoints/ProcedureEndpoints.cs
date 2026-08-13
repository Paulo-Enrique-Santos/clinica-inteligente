using Clinica.Domain.Procedures;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class ProcedureEndpoints
{
    public static IEndpointRouteBuilder MapProcedureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/procedures")
            .RequireAuthorization()
            .WithTags("Procedimentos");

        group.MapGet("/", async (bool? incluirInativos, ClinicaDbContext db, CancellationToken ct) =>
        {
            var query = db.Procedures.AsQueryable();

            if (incluirInativos != true)
            {
                query = query.Where(p => p.Active);
            }

            var procedimentos = await query
                .OrderBy(p => p.Name)
                .Select(p => ProcedureResponse.From(p))
                .ToListAsync(ct);

            return Results.Ok(procedimentos);
        })
        .WithName("ListarProcedimentos");

        group.MapGet("/{id:guid}", async (Guid id, ClinicaDbContext db, CancellationToken ct) =>
        {
            var procedimento = await db.Procedures.FirstOrDefaultAsync(p => p.Id == id, ct);
            return procedimento is null
                ? Results.NotFound()
                : Results.Ok(ProcedureResponse.From(procedimento));
        })
        .WithName("ObterProcedimento");

        group.MapPost("/", async (SaveProcedureRequest request, ClinicaDbContext db, CancellationToken ct) =>
        {
            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            var procedimento = new Procedure
            {
                Name = request.Name.Trim(),
                DurationMinutes = request.DurationMinutes,
                Price = request.Price,
                SuppliesCost = request.SuppliesCost,
                Description = request.Description?.Trim(),
                Active = request.Active ?? true,
            };

            db.Procedures.Add(procedimento);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/procedures/{procedimento.Id}", ProcedureResponse.From(procedimento));
        })
        .RequireAuthorization(p => p.RequireRole("OWNER"))
        .WithName("CriarProcedimento");

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveProcedureRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            var procedimento = await db.Procedures.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (procedimento is null)
            {
                return Results.NotFound();
            }

            procedimento.Name = request.Name.Trim();
            procedimento.DurationMinutes = request.DurationMinutes;
            procedimento.Price = request.Price;
            procedimento.SuppliesCost = request.SuppliesCost;
            procedimento.Description = request.Description?.Trim();
            procedimento.Active = request.Active ?? procedimento.Active;

            await db.SaveChangesAsync(ct);

            return Results.Ok(ProcedureResponse.From(procedimento));
        })
        .RequireAuthorization(p => p.RequireRole("OWNER"))
        .WithName("AtualizarProcedimento");

        return app;
    }
}

public record SaveProcedureRequest(
    string Name,
    int DurationMinutes,
    decimal Price,
    decimal SuppliesCost,
    string? Description,
    bool? Active)
{
    public Dictionary<string, string[]>? Validate()
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            erros[nameof(Name)] = ["Nome e obrigatorio."];
        }

        // Duração alimenta a agenda: zero deixaria o atendimento sem fim, e a
        // constraint do banco recusaria com uma mensagem que a recepção não entende.
        if (DurationMinutes is < 5 or > 480)
        {
            erros[nameof(DurationMinutes)] = ["Duracao deve estar entre 5 e 480 minutos."];
        }

        if (Price < 0)
        {
            erros[nameof(Price)] = ["Preco nao pode ser negativo."];
        }

        if (SuppliesCost < 0)
        {
            erros[nameof(SuppliesCost)] = ["Custo de insumos nao pode ser negativo."];
        }

        return erros.Count == 0 ? null : erros;
    }
}

public record ProcedureResponse(
    Guid Id,
    string Name,
    int DurationMinutes,
    decimal Price,
    decimal SuppliesCost,
    decimal Margin,
    string? Description,
    bool Active)
{
    public static ProcedureResponse From(Procedure p) =>
        new(p.Id, p.Name, p.DurationMinutes, p.Price, p.SuppliesCost,
            p.Price - p.SuppliesCost, p.Description, p.Active);
}
