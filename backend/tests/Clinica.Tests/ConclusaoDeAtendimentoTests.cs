using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Conclusão do atendimento: baixa de insumo, observação clínica e agendamento do
/// contato de pós-procedimento.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConclusaoDeAtendimentoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    private ClinicaApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _client = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY", "FINANCE", "DOCTOR");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Concluir_da_baixa_no_estoque_com_a_quantidade_real()
    {
        var atendimento = await Agendar(9);
        var item = await CriarItem("Toxina", "un");
        await Entrada(item, 10);

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir", new
        {
            notes = "Paciente tolerou bem.",
            supplies = new[] { new { stockItemId = item, quantity = 1.5m } },
            followUpInHours = 24,
        });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var estoque = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");

        // 10 de entrada menos o 1,5 realmente usado — e não a quantidade que a tabela
        // do procedimento previa.
        Assert.Equal(8.5m, estoque!.Single(i => i.Id == item).Balance);
    }

    [Fact]
    public async Task Concluir_marca_o_atendimento_como_realizado()
    {
        var atendimento = await Agendar(10);

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir",
            new { notes = "Sem intercorrencias." });
        resposta.EnsureSuccessStatusCode();

        var dia = new DateTimeOffset(2027, 11, 8, 0, 0, 0, Fuso);
        var agenda = await ListarAgenda(dia);

        Assert.Equal("Realizado", agenda.Single(a => a.Id == atendimento).Status);
    }

    [Fact]
    public async Task Estoque_pode_ficar_negativo_para_nao_travar_a_doutora()
    {
        var atendimento = await Agendar(11);
        var item = await CriarItem("Creme", "ml");
        // Nenhuma entrada: a clínica usou produto que nunca foi registrado.

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir", new
        {
            supplies = new[] { new { stockItemId = item, quantity = 3m } },
        });

        // Travar aqui impediria fechar o atendimento com a paciente na frente, por causa
        // de escrituração. O saldo negativo aparece na tela e se corrige com ajuste.
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var estoque = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");
        Assert.Equal(-3m, estoque!.Single(i => i.Id == item).Balance);
    }

    [Fact]
    public async Task Insumo_de_outra_clinica_e_recusado()
    {
        var atendimento = await Agendar(12);

        var nova = _factory.CreateClientFor(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "OWNER", "FINANCE");

        var criado = await nova.PostAsJsonAsync("/stock", new
        {
            name = $"Item da outra {Guid.NewGuid():N}"[..30],
            unit = "un",
            minimumQuantity = 0,
        });
        var itemDaOutra = await criado.Content.ReadFromJsonAsync<Guid>();

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir", new
        {
            supplies = new[] { new { stockItemId = itemDaOutra, quantity = 1m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Atendimento_cancelado_nao_pode_ser_concluido()
    {
        var atendimento = await Agendar(13);

        var cancelar = await _client.PostAsJsonAsync($"/appointments/{atendimento}/status",
            new { status = "Cancelado", reason = "Paciente desistiu" });
        cancelar.EnsureSuccessStatusCode();

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir",
            new { notes = "nao deveria passar" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Quantidade_zerada_e_recusada()
    {
        var atendimento = await Agendar(14);
        var item = await CriarItem("Agulha", "un");

        var resposta = await _client.PostAsJsonAsync($"/appointments/{atendimento}/concluir", new
        {
            supplies = new[] { new { stockItemId = item, quantity = 0m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // --- apoio ------------------------------------------------------------

    private async Task<List<AppointmentResponse>> ListarAgenda(DateTimeOffset dia)
    {
        var de = Uri.EscapeDataString(dia.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var ate = Uri.EscapeDataString(dia.AddDays(1).ToString("yyyy-MM-ddTHH:mm:sszzz"));

        return await _client.GetFromJsonAsync<List<AppointmentResponse>>(
            $"/appointments?de={de}&ate={ate}") ?? [];
    }

    private async Task<Guid> CriarItem(string nome, string unidade)
    {
        var r = await _client.PostAsJsonAsync("/stock", new
        {
            name = $"{nome} {Guid.NewGuid():N}"[..30],
            unit = unidade,
            minimumQuantity = 0,
        });
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task Entrada(Guid item, decimal quantidade)
    {
        var r = await _client.PostAsJsonAsync($"/stock/{item}/movements",
            new { type = "Entrada", quantity = quantidade });
        r.EnsureSuccessStatusCode();
    }

    private async Task<Guid> Agendar(int hora)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await _client.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        var p = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var procedimento = await _client.PostAsJsonAsync("/procedures", new
        {
            name = $"Proc {sufixo}",
            durationMinutes = 30,
            price = 150m,
            suppliesCost = 20m,
        });
        var proc = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var profissional = await _client.PostAsJsonAsync("/professionals", new
        {
            fullName = $"Dra. {sufixo}",
            displayName = $"Dra. {sufixo}",
        });
        var prof = await profissional.Content.ReadFromJsonAsync<ProfessionalResponse>();

        var atendimento = await _client.PostAsJsonAsync("/appointments", new
        {
            patientId = p!.Id,
            procedureId = proc!.Id,
            professionalId = prof!.Id,
            startsAt = new DateTimeOffset(2027, 11, 8, hora, 0, 0, Fuso),
        });
        atendimento.EnsureSuccessStatusCode();

        return await atendimento.Content.ReadFromJsonAsync<Guid>();
    }
}
