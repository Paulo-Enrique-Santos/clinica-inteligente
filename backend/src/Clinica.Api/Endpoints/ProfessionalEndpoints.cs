using Clinica.Domain.Professionals;
using Clinica.Domain.Tenancy;
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

        group.MapGet("/", async (string? q, bool? incluirInativos, ClinicaDbContext db, CancellationToken ct) =>
        {
            var query = db.Professionals.AsQueryable();

            if (incluirInativos != true)
            {
                query = query.Where(p => p.Active);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => EF.Functions.ILike(p.DisplayName, $"%{q.Trim()}%"));
            }

            var profissionais = await query
                .OrderBy(p => p.DisplayName)
                .Select(p => ProfessionalResponse.From(p))
                .ToListAsync(ct);

            return Results.Ok(profissionais);
        })
        .WithName("ListarProfissionais");

        // Antes de /{id}, senão "me" seria interpretado como identificador.
        group.MapGet("/me", async (ClinicaDbContext db, IUserContext usuario, CancellationToken ct) =>
        {
            var eu = await db.Professionals
                .FirstOrDefaultAsync(p => p.KeycloakUserId == usuario.UserId, ct);

            // 404 aqui é informação útil, não falha: significa "seu login ainda não está
            // vinculado a uma profissional", e a tela sabe explicar isso.
            return eu is null ? Results.NotFound() : Results.Ok(ProfessionalResponse.From(eu));
        })
        .WithName("ObterMinhaFichaDeProfissional");

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
    bool Active,
    /// <summary>Vínculo com o login. Sem ele, a profissional não consegue ver a própria agenda.</summary>
    bool VinculadaAoLogin)
{
    public static ProfessionalResponse From(Professional p) =>
        new(p.Id, p.FullName, p.DisplayName, p.Specialty, p.Active,
            !string.IsNullOrWhiteSpace(p.KeycloakUserId));
}
