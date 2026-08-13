using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Quem vê a agenda de quem.
///
/// A doutora enxerga apenas os próprios atendimentos; recepção e dona organizam a agenda
/// da clínica inteira. A restrição é do servidor — esconder o filtro na tela deixaria
/// qualquer pessoa ler a agenda alheia trocando a query string, e junto vão nome e
/// telefone das pacientes.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgendaPorPerfilTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    private ClinicaApiFactory _factory = null!;
    private HttpClient _recepcao = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _recepcao = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Doutora_ve_apenas_a_propria_agenda()
    {
        var loginDaCarla = $"user-carla-{Guid.NewGuid():N}";
        var carla = await CriarProfissional("Dra. Carla", loginDaCarla);
        var bruna = await CriarProfissional("Dra. Bruna", vinculo: null);

        var dia = new DateTimeOffset(2027, 9, 20, 0, 0, 0, Fuso);
        var daCarla = await Agendar(carla, dia.AddHours(9));
        var daBruna = await Agendar(bruna, dia.AddHours(10));

        var clienteDaCarla = _factory.CreateClientForUser(BellaFace, loginDaCarla, "DOCTOR");
        var agenda = await ListarAgenda(clienteDaCarla, dia);

        Assert.Contains(agenda, a => a.Id == daCarla);
        Assert.DoesNotContain(agenda, a => a.Id == daBruna);
    }

    [Fact]
    public async Task Doutora_nao_alcanca_agenda_alheia_forcando_a_query_string()
    {
        var loginDaCarla = $"user-carla-{Guid.NewGuid():N}";
        await CriarProfissional("Dra. Carla", loginDaCarla);
        var bruna = await CriarProfissional("Dra. Bruna", vinculo: null);

        var dia = new DateTimeOffset(2027, 9, 21, 0, 0, 0, Fuso);
        var daBruna = await Agendar(bruna, dia.AddHours(11));

        var clienteDaCarla = _factory.CreateClientForUser(BellaFace, loginDaCarla, "DOCTOR");

        // Pede explicitamente a agenda da outra profissional.
        var agenda = await ListarAgenda(clienteDaCarla, dia, bruna);

        Assert.DoesNotContain(agenda, a => a.Id == daBruna);
    }

    [Fact]
    public async Task Recepcao_ve_a_agenda_de_todas()
    {
        var carla = await CriarProfissional("Dra. Carla", $"user-{Guid.NewGuid():N}");
        var bruna = await CriarProfissional("Dra. Bruna", vinculo: null);

        var dia = new DateTimeOffset(2027, 9, 22, 0, 0, 0, Fuso);
        var daCarla = await Agendar(carla, dia.AddHours(9));
        var daBruna = await Agendar(bruna, dia.AddHours(9));

        var agenda = await ListarAgenda(_recepcao, dia);

        Assert.Contains(agenda, a => a.Id == daCarla);
        Assert.Contains(agenda, a => a.Id == daBruna);
    }

    [Fact]
    public async Task Doutora_sem_vinculo_com_login_ve_agenda_vazia()
    {
        var bruna = await CriarProfissional("Dra. Bruna", vinculo: null);
        var dia = new DateTimeOffset(2027, 9, 23, 0, 0, 0, Fuso);
        await Agendar(bruna, dia.AddHours(9));

        var semVinculo = _factory.CreateClientForUser(BellaFace, "ninguem", "DOCTOR");
        var agenda = await ListarAgenda(semVinculo, dia);

        // Nega por padrão: sem vínculo, nenhuma agenda — e não a agenda de todo mundo.
        Assert.Empty(agenda);
    }

    [Fact]
    public async Task Dona_que_tambem_atende_continua_vendo_tudo()
    {
        var login = $"user-dona-{Guid.NewGuid():N}";
        var dona = await CriarProfissional("Dra. Ana", login);
        var outra = await CriarProfissional("Dra. Bruna", vinculo: null);

        var dia = new DateTimeOffset(2027, 9, 24, 0, 0, 0, Fuso);
        await Agendar(dona, dia.AddHours(9));
        var daOutra = await Agendar(outra, dia.AddHours(9));

        // Em clínica pequena a dona também atende. Ganhar o papel de quem atende não
        // pode custar a visão do próprio negócio.
        var cliente = _factory.CreateClientForUser(BellaFace, login, "OWNER", "DOCTOR");
        var agenda = await ListarAgenda(cliente, dia);

        Assert.Contains(agenda, a => a.Id == daOutra);
    }

    // --- apoio ------------------------------------------------------------

    private async Task<List<AppointmentResponse>> ListarAgenda(
        HttpClient client,
        DateTimeOffset dia,
        Guid? profissional = null)
    {
        var de = Uri.EscapeDataString(dia.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var ate = Uri.EscapeDataString(dia.AddDays(1).ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var filtro = profissional is { } p ? $"&profissionalId={p}" : "";

        return await client.GetFromJsonAsync<List<AppointmentResponse>>(
            $"/appointments?de={de}&ate={ate}{filtro}") ?? [];
    }

    private async Task<Guid> CriarProfissional(string nome, string? vinculo)
    {
        var resposta = await _recepcao.PostAsJsonAsync("/professionals", new
        {
            fullName = $"{nome} {Guid.NewGuid():N}"[..30],
            displayName = nome,
            keycloakUserId = vinculo,
        });
        resposta.EnsureSuccessStatusCode();

        var criada = await resposta.Content.ReadFromJsonAsync<ProfessionalResponse>();
        return criada!.Id;
    }

    private async Task<Guid> Agendar(Guid profissional, DateTimeOffset inicio)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await _recepcao.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        var pacienteCriado = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var procedimento = await _recepcao.PostAsJsonAsync("/procedures", new
        {
            name = $"Procedimento {sufixo}",
            durationMinutes = 30,
            price = 100m,
            suppliesCost = 10m,
        });
        var procedimentoCriado = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var atendimento = await _recepcao.PostAsJsonAsync("/appointments", new
        {
            patientId = pacienteCriado!.Id,
            procedureId = procedimentoCriado!.Id,
            professionalId = profissional,
            startsAt = inicio,
        });
        atendimento.EnsureSuccessStatusCode();

        return await atendimento.Content.ReadFromJsonAsync<Guid>();
    }
}
