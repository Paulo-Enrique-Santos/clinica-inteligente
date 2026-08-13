using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Ficha de anamnese por link público.
///
/// É o único caminho do sistema em que a clínica não vem do token do usuário — a paciente
/// não tem login. Estes testes existem porque abrir essa porta é o tipo de coisa que
/// precisa continuar estreita conforme o sistema cresce.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AnamneseTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _recepcao = null!;
    private HttpClient _anonimo = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _recepcao = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY");
        _anonimo = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Paciente_abre_e_envia_a_ficha_sem_estar_logada()
    {
        var paciente = await CriarPaciente("Maria Aparecida Souza");
        var token = await GerarLink(paciente);

        var abertura = await _anonimo.GetFromJsonAsync<Abertura>($"/public/anamnese/{token}");
        // Só o primeiro nome: quem tem o link não deve receber telefone nem histórico.
        Assert.Equal("Maria", abertura!.PrimeiroNome);

        var envio = await _anonimo.PostAsJsonAsync($"/public/anamnese/{token}", new
        {
            answers = new Dictionary<string, string> { ["alergias"] = "Nenhuma" },
            imageConsent = true,
            dataConsent = true,
        });

        Assert.Equal(HttpStatusCode.NoContent, envio.StatusCode);

        var ficha = await _recepcao.GetFromJsonAsync<FichaDaPaciente>($"/patients/{paciente}/ficha");
        Assert.NotNull(ficha!.Anamnesis);
        Assert.True(ficha.Anamnesis!.ImageConsent);
    }

    [Fact]
    public async Task Link_so_serve_uma_vez()
    {
        var paciente = await CriarPaciente("Joana Lima");
        var token = await GerarLink(paciente);

        var corpo = new { answers = new Dictionary<string, string>(), imageConsent = false, dataConsent = true };

        (await _anonimo.PostAsJsonAsync($"/public/anamnese/{token}", corpo)).EnsureSuccessStatusCode();

        var segunda = await _anonimo.PostAsJsonAsync($"/public/anamnese/{token}", corpo);

        // Link vivo para sempre é link que vaza e continua servindo.
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
    }

    [Fact]
    public async Task Token_inventado_nao_abre_ficha_nenhuma()
    {
        var resposta = await _anonimo.GetAsync("/public/anamnese/token-que-nao-existe");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Sem_consentimento_de_dados_a_ficha_nao_e_guardada()
    {
        var paciente = await CriarPaciente("Rita Nunes");
        var token = await GerarLink(paciente);

        var resposta = await _anonimo.PostAsJsonAsync($"/public/anamnese/{token}", new
        {
            answers = new Dictionary<string, string> { ["alergias"] = "Dipirona" },
            imageConsent = false,
            dataConsent = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var ficha = await _recepcao.GetFromJsonAsync<FichaDaPaciente>($"/patients/{paciente}/ficha");
        Assert.Null(ficha!.Anamnesis);
    }

    [Fact]
    public async Task Ficha_da_paciente_esconde_financeiro_de_quem_nao_cuida_do_dinheiro()
    {
        var paciente = await CriarPaciente("Clara Dias");

        var doutora = _factory.CreateClientFor(BellaFace, "DOCTOR");
        var comoDoutora = await doutora.GetFromJsonAsync<FichaDaPaciente>($"/patients/{paciente}/ficha");
        var comoDona = await _recepcao.GetFromJsonAsync<FichaDaPaciente>($"/patients/{paciente}/ficha");

        // A doutora precisa do histórico clínico, não de quanto a paciente deve.
        Assert.False(comoDoutora!.ShowsFinance);
        Assert.True(comoDona!.ShowsFinance);
    }

    // --- apoio ------------------------------------------------------------

    private record Abertura(string PrimeiroNome);
    private record LinkGerado(string Token);

    private async Task<Guid> CriarPaciente(string nome)
    {
        var r = await _recepcao.PostAsJsonAsync("/patients", new
        {
            fullName = nome,
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<PatientResponse>())!.Id;
    }

    private async Task<string> GerarLink(Guid paciente)
    {
        var r = await _recepcao.PostAsJsonAsync($"/patients/{paciente}/anamnese/link", new { });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<LinkGerado>())!.Token;
    }
}
