using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Regressão: o front manda horário no fuso da clínica (-03:00), não em UTC.
///
/// Estes testes existem porque a suíte anterior usava <c>TimeSpan.Zero</c> em todo
/// horário — passava com folga enquanto agendar e listar estavam quebrados pelo
/// navegador, já que o Npgsql recusa offset diferente de zero.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgendaFusoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan FusoDaClinica = TimeSpan.FromHours(-3);

    private ClinicaApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _client = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Agendar_com_horario_no_fuso_da_clinica_funciona()
    {
        var (paciente, procedimento, profissional) = await MontarCenario();

        var resposta = await _client.PostAsJsonAsync("/appointments", new
        {
            patientId = paciente,
            procedureId = procedimento,
            professionalId = profissional,
            startsAt = new DateTimeOffset(2027, 8, 10, 9, 0, 0, FusoDaClinica),
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_agenda_com_janela_no_fuso_da_clinica_encontra_o_atendimento()
    {
        var (paciente, procedimento, profissional) = await MontarCenario();
        var inicio = new DateTimeOffset(2027, 8, 11, 14, 0, 0, FusoDaClinica);

        var criado = await _client.PostAsJsonAsync("/appointments", new
        {
            patientId = paciente,
            procedureId = procedimento,
            professionalId = profissional,
            startsAt = inicio,
        });
        criado.EnsureSuccessStatusCode();
        var id = await criado.Content.ReadFromJsonAsync<Guid>();

        var de = Uri.EscapeDataString("2027-08-11T00:00:00-03:00");
        var ate = Uri.EscapeDataString("2027-08-12T00:00:00-03:00");

        var agenda = await _client.GetFromJsonAsync<List<AppointmentResponse>>(
            $"/appointments?de={de}&ate={ate}");

        Assert.Contains(agenda!, a => a.Id == id);
    }

    [Fact]
    public async Task Atendimento_das_23h_nao_escorrega_para_o_dia_seguinte()
    {
        var (paciente, procedimento, profissional) = await MontarCenario();

        // 23h no fuso da clínica é 02h do dia seguinte em UTC. Se a janela do dia
        // fosse montada em UTC, este atendimento sumiria da agenda de quem marcou.
        var inicio = new DateTimeOffset(2027, 8, 12, 23, 0, 0, FusoDaClinica);

        var criado = await _client.PostAsJsonAsync("/appointments", new
        {
            patientId = paciente,
            procedureId = procedimento,
            professionalId = profissional,
            startsAt = inicio,
        });
        criado.EnsureSuccessStatusCode();
        var id = await criado.Content.ReadFromJsonAsync<Guid>();

        var de = Uri.EscapeDataString("2027-08-12T00:00:00-03:00");
        var ate = Uri.EscapeDataString("2027-08-13T00:00:00-03:00");

        var agenda = await _client.GetFromJsonAsync<List<AppointmentResponse>>(
            $"/appointments?de={de}&ate={ate}");

        Assert.Contains(agenda!, a => a.Id == id);
    }

    private async Task<(Guid Paciente, Guid Procedimento, Guid Profissional)> MontarCenario()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await _client.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        var pacienteCriado = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var procedimento = await _client.PostAsJsonAsync("/procedures", new
        {
            name = $"Procedimento {sufixo}",
            durationMinutes = 60,
            price = 250m,
            suppliesCost = 40m,
        });
        var procedimentoCriado = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var profissional = await _client.PostAsJsonAsync("/professionals", new
        {
            fullName = $"Dra. {sufixo}",
            displayName = $"Dra. {sufixo}",
        });
        var profissionalCriado = await profissional.Content.ReadFromJsonAsync<ProfessionalResponse>();

        return (pacienteCriado!.Id, procedimentoCriado!.Id, profissionalCriado!.Id);
    }
}
