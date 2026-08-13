using Clinica.Domain.Patients;
using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Api.Endpoints;

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/patients")
            .RequireAuthorization()
            .WithTags("Pacientes");

        // Nenhuma destas consultas menciona tenant. Nao e esquecimento: o filtro global
        // do EF Core ja restringe, e o RLS do Postgres restringe de novo. Se um dia
        // alguem "consertar" isso adicionando .Where(p => p.TenantId == ...), o codigo
        // fica redundante, nao mais seguro.
        group.MapGet("/", async (string? q, ClinicaDbContext db, CancellationToken ct) =>
        {
            var query = db.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = q.Trim();
                // Só dígitos? É busca por telefone. A recepção digita "98765" tanto
                // quanto "Maria", e obrigar a escolher o campo seria burocracia.
                var somenteDigitos = termo.All(char.IsDigit);

                query = somenteDigitos
                    ? query.Where(p => p.PhoneE164.Contains(termo))
                    : query.Where(p => EF.Functions.ILike(p.FullName, $"%{termo}%"));
            }

            var patients = await query
                .OrderBy(p => p.FullName)
                .Take(30)
                .Select(p => PatientResponse.From(p))
                .ToListAsync(ct);

            return Results.Ok(patients);
        })
        .WithName("ListarPacientes");

        group.MapGet("/{id:guid}", async (Guid id, ClinicaDbContext db, CancellationToken ct) =>
        {
            var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

            // 404, nao 403, quando o paciente e de outra clinica. Responder 403 confirmaria
            // que o registro existe — vazamento de informacao por codigo de status.
            return patient is null
                ? Results.NotFound()
                : Results.Ok(PatientResponse.From(patient));
        })
        .WithName("ObterPaciente");

        group.MapPost("/", async (
            CreatePatientRequest request,
            ClinicaDbContext db,
            CancellationToken ct) =>
        {
            var validation = request.Validate();
            if (validation is not null)
            {
                return Results.ValidationProblem(validation);
            }

            var patient = new Patient
            {
                FullName = request.FullName.Trim(),
                PhoneE164 = request.PhoneE164.Trim(),
                BirthDate = request.BirthDate,
                Notes = request.Notes?.Trim(),
                // TenantId nao e atribuido aqui de proposito: quem carimba e o DbContext,
                // a partir do token. O request nem tem esse campo.
            };

            db.Patients.Add(patient);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/patients/{patient.Id}", PatientResponse.From(patient));
        })
        .RequireAuthorization(policy => policy.RequireRole("OWNER", "SECRETARY"))
        .WithName("CriarPaciente");

        return app;
    }
}

public record CreatePatientRequest(
    string FullName,
    string PhoneE164,
    DateOnly? BirthDate,
    string? Notes)
{
    public Dictionary<string, string[]>? Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(FullName))
        {
            errors[nameof(FullName)] = ["Nome e obrigatorio."];
        }

        if (string.IsNullOrWhiteSpace(PhoneE164))
        {
            errors[nameof(PhoneE164)] = ["Telefone e obrigatorio."];
        }
        else if (!PhoneE164.StartsWith('+') || PhoneE164.Length is < 8 or > 20)
        {
            errors[nameof(PhoneE164)] = ["Telefone deve estar em E.164, ex.: +5511987654321."];
        }

        return errors.Count == 0 ? null : errors;
    }
}

public record PatientResponse(
    Guid Id,
    string FullName,
    string PhoneE164,
    DateOnly? BirthDate,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    public static PatientResponse From(Patient p) =>
        new(p.Id, p.FullName, p.PhoneE164, p.BirthDate, p.Notes, p.CreatedAt);
}
