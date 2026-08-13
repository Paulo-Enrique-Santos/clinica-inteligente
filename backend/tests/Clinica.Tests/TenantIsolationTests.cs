using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O guardiao da ADR 0001.
///
/// Se algum destes testes falhar, o sistema esta expondo dado de saude de uma clinica
/// para outra. Nao ha "corrige depois": e o unico conjunto de testes desta fase que
/// justifica travar o merge sozinho.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class TenantIsolationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid NovaEstetica = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private ClinicaApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Paciente_de_outra_clinica_retorna_404_e_nao_403()
    {
        var bella = _factory.CreateClientFor(BellaFace, "SECRETARY");
        var nova = _factory.CreateClientFor(NovaEstetica, "SECRETARY");

        var criado = await CriarPaciente(bella, "Paciente da Bella");

        var comoDona = await bella.GetAsync($"/patients/{criado.Id}");
        Assert.Equal(HttpStatusCode.OK, comoDona.StatusCode);

        var comoIntrusa = await nova.GetAsync($"/patients/{criado.Id}");

        // 404 e nao 403: responder "proibido" confirmaria que o registro existe, o que ja
        // e vazamento de informacao. Para a outra clinica, o paciente simplesmente nao ha.
        Assert.Equal(HttpStatusCode.NotFound, comoIntrusa.StatusCode);
    }

    [Fact]
    public async Task Listagem_nao_mistura_pacientes_de_clinicas_diferentes()
    {
        var bella = _factory.CreateClientFor(BellaFace, "SECRETARY");
        var nova = _factory.CreateClientFor(NovaEstetica, "SECRETARY");

        var daBella = await CriarPaciente(bella, "Exclusiva da Bella");
        var daNova = await CriarPaciente(nova, "Exclusiva da Nova");

        var listaBella = await bella.GetFromJsonAsync<List<PatientResponse>>("/patients");
        var listaNova = await nova.GetFromJsonAsync<List<PatientResponse>>("/patients");

        Assert.NotNull(listaBella);
        Assert.NotNull(listaNova);

        Assert.Contains(listaBella, p => p.Id == daBella.Id);
        Assert.DoesNotContain(listaBella, p => p.Id == daNova.Id);

        Assert.Contains(listaNova, p => p.Id == daNova.Id);
        Assert.DoesNotContain(listaNova, p => p.Id == daBella.Id);
    }

    [Fact]
    public async Task Paciente_criado_recebe_o_tenant_do_token()
    {
        var bella = _factory.CreateClientFor(BellaFace, "SECRETARY");
        var nova = _factory.CreateClientFor(NovaEstetica, "SECRETARY");

        var criado = await CriarPaciente(bella, "Carimbo pelo token");

        // O request nem tem campo de tenant. A prova de que o carimbo veio do token e que
        // a outra clinica nao alcanca o registro.
        var comoIntrusa = await nova.GetAsync($"/patients/{criado.Id}");
        Assert.Equal(HttpStatusCode.NotFound, comoIntrusa.StatusCode);
    }

    [Fact]
    public async Task Requisicao_sem_autenticacao_e_recusada()
    {
        var anonimo = _factory.CreateClient();

        var resposta = await anonimo.GetAsync("/patients");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Papel_sem_permissao_nao_cria_paciente()
    {
        // Financeiro nao cadastra paciente. Tenancy resolvida nao implica autorizacao.
        var financeiro = _factory.CreateClientFor(BellaFace, "FINANCE");

        var resposta = await financeiro.PostAsJsonAsync("/patients", new
        {
            fullName = "Nao deveria existir",
            phoneE164 = TelefoneAleatorio(),
        });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    private static async Task<PatientResponse> CriarPaciente(HttpClient client, string nome)
    {
        var resposta = await client.PostAsJsonAsync("/patients", new
        {
            fullName = nome,
            phoneE164 = TelefoneAleatorio(),
        });

        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<PatientResponse>();
        Assert.NotNull(criado);

        return criado;
    }

    // O indice unico e (tenant_id, telefone); telefone aleatorio evita colisao entre
    // testes que compartilham o mesmo banco.
    private static string TelefoneAleatorio() =>
        $"+5511{Random.Shared.Next(100000000, 999999999)}";
}
