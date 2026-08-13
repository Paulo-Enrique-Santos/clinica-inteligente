using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Agenda: conflito de horário e isolamento entre clínicas.
///
/// A regra de sobreposição vive numa constraint do Postgres, não numa consulta prévia.
/// Estes testes verificam que ela chega à API traduzida em 409 — e não em 500.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AppointmentTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid NovaEstetica = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _bella = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _bella = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Horario_livre_e_aceito()
    {
        var cenario = await MontarCenario(_bella);

        var resposta = await Agendar(_bella, cenario, Horario(9));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Mesma_profissional_no_mesmo_horario_e_recusada_com_409()
    {
        var cenario = await MontarCenario(_bella);
        var inicio = Horario(11);

        var primeira = await Agendar(_bella, cenario, inicio);
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);

        // Procedimento de 60 min: começar 30 min depois invade o horário anterior.
        var segunda = await Agendar(_bella, cenario, inicio.AddMinutes(30));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Profissionais_diferentes_podem_atender_no_mesmo_horario()
    {
        var cenario = await MontarCenario(_bella);
        var outraProfissional = await CriarProfissional(_bella, $"Dra. Bruna {Guid.NewGuid():N}");
        var inicio = Horario(14);

        var primeira = await Agendar(_bella, cenario, inicio);
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);

        var segunda = await Agendar(_bella, cenario with { ProfissionalId = outraProfissional }, inicio);

        // Duas salas, duas profissionais: o horário não colide.
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
    }

    [Fact]
    public async Task Horario_encostado_no_anterior_e_aceito()
    {
        var cenario = await MontarCenario(_bella);
        var inicio = Horario(16);

        await Agendar(_bella, cenario, inicio);

        // O intervalo é fechado no início e aberto no fim: 17h não conflita com
        // um atendimento que termina às 17h.
        var seguinte = await Agendar(_bella, cenario, inicio.AddMinutes(60));

        Assert.Equal(HttpStatusCode.Created, seguinte.StatusCode);
    }

    [Fact]
    public async Task Nao_e_possivel_agendar_paciente_de_outra_clinica()
    {
        var cenarioBella = await MontarCenario(_bella);

        var nova = _factory.CreateClientFor(NovaEstetica, "OWNER", "SECRETARY");
        var cenarioNova = await MontarCenario(nova);

        // Agenda da Bella, com paciente da Nova Estética.
        var resposta = await Agendar(
            _bella,
            cenarioBella with { PacienteId = cenarioNova.PacienteId },
            Horario(19));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // --- apoio ------------------------------------------------------------

    private record Cenario(Guid PacienteId, Guid ProcedimentoId, Guid ProfissionalId);

    /// <summary>Cria paciente, procedimento de 60 min e profissional para a clínica do cliente.</summary>
    private static async Task<Cenario> MontarCenario(HttpClient client)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await client.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        paciente.EnsureSuccessStatusCode();
        var pacienteCriado = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var procedimento = await client.PostAsJsonAsync("/procedures", new
        {
            name = $"Limpeza de pele {sufixo}",
            durationMinutes = 60,
            price = 250m,
            suppliesCost = 40m,
        });
        procedimento.EnsureSuccessStatusCode();
        var procedimentoCriado = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var profissional = await CriarProfissional(client, $"Dra. Carla {sufixo}");

        Assert.NotNull(pacienteCriado);
        Assert.NotNull(procedimentoCriado);

        return new Cenario(pacienteCriado.Id, procedimentoCriado.Id, profissional);
    }

    private static async Task<Guid> CriarProfissional(HttpClient client, string nome)
    {
        var resposta = await client.PostAsJsonAsync("/professionals", new
        {
            fullName = nome,
            displayName = nome,
        });
        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<ProfessionalResponse>();
        Assert.NotNull(criado);

        return criado.Id;
    }

    private static Task<HttpResponseMessage> Agendar(
        HttpClient client,
        Cenario cenario,
        DateTimeOffset inicio) =>
        client.PostAsJsonAsync("/appointments", new
        {
            patientId = cenario.PacienteId,
            procedureId = cenario.ProcedimentoId,
            professionalId = cenario.ProfissionalId,
            startsAt = inicio,
        });

    // Data fixa no futuro, com hora variando por teste: os testes compartilham o
    // mesmo banco, e horários distintos evitam colisão entre eles.
    private static DateTimeOffset Horario(int hora) =>
        new(2027, 3, 15, hora, 0, 0, TimeSpan.Zero);
}
