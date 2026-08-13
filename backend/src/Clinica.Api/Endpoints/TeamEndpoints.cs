using Clinica.Domain.Professionals;
using Clinica.Domain.Tenancy;
using Clinica.Infrastructure.Identity;
using Clinica.Infrastructure.Persistence;

namespace Clinica.Api.Endpoints;

public static class TeamEndpoints
{
    /// <summary>
    /// Papéis que a dona pode conceder. OWNER fica de fora de propósito: quem paga pelo
    /// produto recebe esse acesso no onboarding da clínica, e permitir criar outra dona
    /// pela tela abriria caminho para alguém se promover sem passar por ninguém.
    /// </summary>
    private static readonly string[] PapeisConcediveis = ["DOCTOR", "SECRETARY", "FINANCE"];

    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/team")
            .RequireAuthorization(p => p.RequireRole("OWNER"))
            .WithTags("Equipe");

        group.MapGet("/", async (
            KeycloakAdminClient keycloak,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var contas = await keycloak.ListarDaClinicaAsync(tenant.TenantId, ct);
            return Results.Ok(contas);
        })
        .WithName("ListarEquipe");

        group.MapPost("/", async (
            CreateTeamMemberRequest request,
            KeycloakAdminClient keycloak,
            ClinicaDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (request.Validate(PapeisConcediveis) is { } erros)
            {
                return Results.ValidationProblem(erros);
            }

            string usuarioId;

            try
            {
                usuarioId = await keycloak.CriarAsync(
                    // O tenant vem do token da dona. O request nem tem esse campo.
                    tenant.TenantId,
                    new NovaConta(
                        request.Username.Trim().ToLowerInvariant(),
                        request.Email?.Trim(),
                        request.FirstName.Trim(),
                        request.LastName.Trim(),
                        request.Password,
                        request.Role),
                    ct);
            }
            catch (ContaJaExisteException e)
            {
                return Results.Problem(
                    title: "Usuario ja existe",
                    detail: e.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Doutora precisa de ficha de profissional para aparecer na agenda — e o
            // vínculo com o login é o que permite a ela ver a própria agenda. Criar as
            // duas coisas juntas evita o passo manual que hoje deixa a agenda vazia.
            if (request.Role == "DOCTOR")
            {
                var nome = $"{request.FirstName.Trim()} {request.LastName.Trim()}";

                db.Professionals.Add(new Professional
                {
                    FullName = nome,
                    DisplayName = request.DisplayName?.Trim() is { Length: > 0 } d ? d : nome,
                    Specialty = request.Specialty?.Trim(),
                    KeycloakUserId = usuarioId,
                });

                await db.SaveChangesAsync(ct);
            }

            return Results.Created($"/team/{usuarioId}", new { id = usuarioId });
        })
        .WithName("CriarMembroDaEquipe");

        return app;
    }
}

public record CreateTeamMemberRequest(
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string Password,
    string Role,
    string? DisplayName,
    string? Specialty)
{
    public Dictionary<string, string[]>? Validate(string[] papeisPermitidos)
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length < 3)
        {
            erros[nameof(Username)] = ["Usuario deve ter ao menos 3 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(FirstName))
        {
            erros[nameof(FirstName)] = ["Nome e obrigatorio."];
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            erros[nameof(LastName)] = ["Sobrenome e obrigatorio."];
        }

        // Oito é o mínimo defensável para senha entregue em mãos e usada por tempo
        // indeterminado, já que ainda não existe tela de troca.
        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
        {
            erros[nameof(Password)] = ["Senha deve ter ao menos 8 caracteres."];
        }

        if (!papeisPermitidos.Contains(Role))
        {
            erros[nameof(Role)] = [$"Papel deve ser um de: {string.Join(", ", papeisPermitidos)}."];
        }

        return erros.Count == 0 ? null : erros;
    }
}
