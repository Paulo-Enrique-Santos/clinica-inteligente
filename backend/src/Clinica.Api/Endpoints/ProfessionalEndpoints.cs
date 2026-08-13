using Clinica.Domain.Professionals;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class ProfessionalEndpoints
{
    public static IEndpointRouteBuilder MapProfessionalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/professionals")
            .RequireAuthorization()
            .WithTags("Profissionais");

        group.MapGet("/", async (bool? incluirInativos, ClinicaDbContext db, CancellationToken ct) =>
        {
            var query = db.Professionals.AsQueryable();

            if (incluirInativos != true)
            {
                query = query.Where(p => p.Active);
            }

            var profissionais = await query
                .OrderBy(p => p.DisplayName)
                .Select(p => ProfessionalResponse.From(p))
                .ToListAsync(ct);

            return Results.Ok(profissionais);
        })
        .WithName("ListarProfissionais");

        group.MapPost("/", async (SaveProfessionalRequest request, ClinicaDbContext db, CancellationToken ct) =>
        {
            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            var profissional = new Professional
            {
                FullName = request.FullName.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Specialty = request.Specialty?.Trim(),
                KeycloakUserId = request.KeycloakUserId?.Trim(),
                Active = request.Active ?? true,
            };

            db.Professionals.Add(profissional);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/professionals/{profissional.Id}", ProfessionalResponse.From(profissional));
        })
        .RequireAuthorization(p => p.RequireRole("OWNER"))
        .WithName("CriarProfissional");

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveProfessionalRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            if (request.Validate() is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            var profissional = await db.Professionals.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (profissional is null)
            {
                return Results.NotFound();
            }

            profissional.FullName = request.FullName.Trim();
            profissional.DisplayName = request.DisplayName.Trim();
            profissional.Specialty = request.Specialty?.Trim();
            profissional.KeycloakUserId = request.KeycloakUserId?.Trim();
            profissional.Active = request.Active ?? profissional.Active;

            await db.SaveChangesAsync(ct);

            return Results.Ok(ProfessionalResponse.From(profissional));
        })
        .RequireAuthorization(p => p.RequireRole("OWNER"))
        .WithName("AtualizarProfissional");

        return app;
    }
}

public record SaveProfessionalRequest(
    string FullName,
    string DisplayName,
    string? Specialty,
    string? KeycloakUserId,
    bool? Active)
{
    public Dictionary<string, string[]>? Validate()
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(FullName))
        {
            erros[nameof(FullName)] = ["Nome completo e obrigatorio."];
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            erros[nameof(DisplayName)] = ["Nome de exibicao e obrigatorio."];
        }

        return erros.Count == 0 ? null : erros;
    }
}

public record ProfessionalResponse(
    Guid Id,
    string FullName,
    string DisplayName,
    string? Specialty,
    bool Active)
{
    public static ProfessionalResponse From(Professional p) =>
        new(p.Id, p.FullName, p.DisplayName, p.Specialty, p.Active);
}
