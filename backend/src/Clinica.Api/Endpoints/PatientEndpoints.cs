using Clinica.Domain.Patients;
using Clinica.Domain.Tenancy;
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

        group.MapGet("/{id:guid}/ficha", async (
            Guid id,
            string? aba,
            int? pagina,
            ClinicaDbContext db,
            IUserContext usuario,
            CancellationToken ct) =>
        {
            const int PorPagina = 20;

            var paciente = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

            if (paciente is null)
            {
                return Results.NotFound();
            }

            // Financeiro só para quem cuida do dinheiro: a doutora precisa do histórico
            // clínico, não de quanto a paciente deve.
            var podeVerFinanceiro = usuario.HasRole("OWNER") || usuario.HasRole("FINANCE");

            var cobrancasDaPaciente = db.Payments
                .Where(p => (p.Appointment != null && p.Appointment.PatientId == id)
                            || db.TreatmentPlans.Any(t => t.Id == p.TreatmentPlanId
                                                          && t.PatientId == id));

            // Os totais vêm sempre: são eles que rotulam as abas, e uma paciente com
            // 500 sessões não pode obrigar a carregar as 500 para saber que são 500.
            var totais = new FichaTotais(
                await db.Appointments.CountAsync(a => a.PatientId == id, ct),
                await db.TreatmentPlans.CountAsync(p => p.PatientId == id, ct),
                podeVerFinanceiro ? await cobrancasDaPaciente.CountAsync(ct) : 0);

            var pular = Math.Max(0, (pagina ?? 1) - 1) * PorPagina;

            // Só a aba pedida é carregada. Antes a ficha trazia tudo de uma vez, o que
            // funcionava com dez atendimentos e ficaria impraticável com quinhentos.
            var atendimentos = aba is null or "sessoes"
                ? await db.Appointments
                    .Where(a => a.PatientId == id)
                    .OrderByDescending(a => a.StartsAt)
                    .Skip(pular).Take(PorPagina)
                    .Select(a => new FichaAtendimento(
                        a.Id,
                        a.StartsAt,
                        a.Procedure!.Name,
                        a.Professional!.DisplayName,
                        a.Status.ToString(),
                        a.Price,
                        a.ExecutionNotes))
                    .ToListAsync(ct)
                : [];

            var protocolos = aba == "protocolos"
                ? await db.TreatmentPlans
                    .Where(p => p.PatientId == id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(pular).Take(PorPagina)
                    .Select(p => new FichaProtocolo(
                        p.Id,
                        p.Status.ToString(),
                        p.CreatedAt,
                        p.Professional!.DisplayName,
                        p.Items
                            .Where(i => i.Status != Clinica.Domain.Treatments.PlanItemStatus.Recusado)
                            .Select(i => new FichaItem(i.Procedure!.Name, i.Sessions, i.UnitPrice * i.Sessions))
                            .ToList()))
                    .ToListAsync(ct)
                : [];

            var cobrancas = aba == "pagamentos" && podeVerFinanceiro
                ? await cobrancasDaPaciente
                    .OrderByDescending(p => p.DueDate)
                    .Skip(pular).Take(PorPagina)
                    .Select(p => new FichaCobranca(
                        p.Id,
                        p.Amount,
                        p.DueDate,
                        p.Status.ToString(),
                        p.Method == null ? null : p.Method.ToString(),
                        p.InstallmentNumber,
                        p.InstallmentCount))
                    .ToListAsync(ct)
                : [];

            var anamnese = aba == "anamnese"
                ? await db.AnamnesisResponses
                    .Where(a => a.PatientId == id)
                    .OrderByDescending(a => a.SubmittedAt)
                    .Select(a => new FichaAnamnese(a.SubmittedAt, a.ImageConsent, a.AnswersJson))
                    .FirstOrDefaultAsync(ct)
                : null;

            return Results.Ok(new FichaDaPaciente(
                PatientResponse.From(paciente),
                totais,
                atendimentos,
                protocolos,
                cobrancas,
                anamnese,
                podeVerFinanceiro,
                PorPagina));
        })
        .WithName("ObterFichaDaPaciente");

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

public record FichaAtendimento(
    Guid Id,
    DateTimeOffset StartsAt,
    string ProcedureName,
    string ProfessionalName,
    string Status,
    decimal Price,
    string? ExecutionNotes);

public record FichaItem(string ProcedureName, int Sessions, decimal Total);

public record FichaProtocolo(
    Guid Id,
    string Status,
    DateTimeOffset CreatedAt,
    string ProfessionalName,
    List<FichaItem> Items);

public record FichaCobranca(
    Guid Id,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    string? Method,
    int? InstallmentNumber,
    int? InstallmentCount);

public record FichaAnamnese(DateTimeOffset SubmittedAt, bool ImageConsent, string AnswersJson);

public record FichaTotais(int Appointments, int Plans, int Payments);

public record FichaDaPaciente(
    PatientResponse Patient,
    FichaTotais Totals,
    List<FichaAtendimento> Appointments,
    List<FichaProtocolo> Plans,
    List<FichaCobranca> Payments,
    FichaAnamnese? Anamnesis,
    bool ShowsFinance,
    int PageSize);

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
