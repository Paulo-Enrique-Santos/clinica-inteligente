using System.Security.Cryptography;
using System.Text.Json;
using Clinica.Api.Tenancy;
using Clinica.Domain.Anamnesis;
using Clinica.Domain.Tenancy;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class AnamnesisEndpoints
{
    /// <summary>
    /// Uma semana. Curto o bastante para um link vazado envelhecer sozinho, longo o
    /// bastante para a paciente preencher quando puder.
    /// </summary>
    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapAnamnesisEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Interno: a recepção gera o link -------------------------------
        app.MapPost("/patients/{id:guid}/anamnese/link", async (
            Guid id,
            ClinicaDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (!await db.Patients.AnyAsync(p => p.Id == id, ct))
            {
                return Results.NotFound();
            }

            // 32 bytes aleatórios em Base64URL. É credencial: não pode ser adivinhável
            // nem derivado do id da paciente.
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            db.AnamnesisLinks.Add(new AnamnesisLink
            {
                TenantId = tenant.TenantId,
                PatientId = id,
                Token = token,
                ExpiresAt = DateTimeOffset.UtcNow.Add(Validade),
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { token });
        })
        .RequireAuthorization(p => p.RequireRole("OWNER", "SECRETARY", "DOCTOR"))
        .WithTags("Anamnese")
        .WithName("GerarLinkDeAnamnese");

        // --- Público: a paciente preenche ----------------------------------
        var publico = app.MapGroup("/public/anamnese").AllowAnonymous().WithTags("Anamnese");

        publico.MapGet("/{token}", async (
            string token,
            ClinicaDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var link = await ResolverAsync(token, db, tenant, ct);

            if (link is null)
            {
                return Results.NotFound();
            }

            var paciente = await db.Patients
                .Where(p => p.Id == link.PatientId)
                .Select(p => new { p.FullName })
                .FirstOrDefaultAsync(ct);

            // Devolve só o primeiro nome. Quem tiver o link não precisa — e não deve —
            // receber telefone, data de nascimento ou histórico da paciente.
            return paciente is null
                ? Results.NotFound()
                : Results.Ok(new { primeiroNome = paciente.FullName.Split(' ')[0] });
        })
        .WithName("AbrirFichaDeAnamnese");

        publico.MapPost("/{token}", async (
            string token,
            SubmitAnamnesisRequest request,
            ClinicaDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var link = await ResolverAsync(token, db, tenant, ct);

            if (link is null)
            {
                return Results.NotFound();
            }

            if (!request.DataConsent)
            {
                // Dado de saúde sem consentimento não deveria nem ser guardado.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.DataConsent)] =
                        ["E preciso concordar com o tratamento dos dados para enviar a ficha."],
                });
            }

            db.AnamnesisResponses.Add(new AnamnesisResponse
            {
                PatientId = link.PatientId,
                AnswersJson = JsonSerializer.Serialize(request.Answers ?? new()),
                ImageConsent = request.ImageConsent,
                DataConsent = true,
                SubmittedAt = DateTimeOffset.UtcNow,
            });

            // Uso único: o link morre ao ser enviado. Se a paciente precisar corrigir,
            // a recepção gera outro — melhor do que deixar um link vivo indefinidamente.
            link.UsedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("EnviarFichaDeAnamnese");

        return app;
    }

    /// <summary>
    /// Encontra o link e adota a clínica dele para o resto da requisição.
    ///
    /// É o único ponto do sistema em que a clínica não vem do token do usuário — a
    /// paciente não tem login. O token do link faz esse papel, e por isso a busca é feita
    /// numa tabela fora do filtro por tenant (senão não haveria tenant para filtrar).
    /// </summary>
    private static async Task<AnamnesisLink?> ResolverAsync(
        string token,
        ClinicaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var link = await db.AnamnesisLinks.FirstOrDefaultAsync(l => l.Token == token, ct);

        if (link is null || !link.Valido(DateTimeOffset.UtcNow))
        {
            return null;
        }

        if (tenant is HttpTenantContext http)
        {
            http.AssumirPorLinkValidado(link.TenantId);
        }

        return link;
    }
}

public record SubmitAnamnesisRequest(
    Dictionary<string, string>? Answers,
    bool ImageConsent,
    bool DataConsent);
