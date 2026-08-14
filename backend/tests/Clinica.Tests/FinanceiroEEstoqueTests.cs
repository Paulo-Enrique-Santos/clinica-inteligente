using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

[Collection(nameof(PostgresCollection))]
public class FinanceiroEEstoqueTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _client = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY", "FINANCE");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Saldo_do_estoque_e_a_soma_das_movimentacoes()
    {
        var item = await CriarItem("Toxina botulinica", "un", minimo: 5);

        await Movimentar(item, "Entrada", 10);
        await Movimentar(item, "Saida", 3);
        await Movimentar(item, "Ajuste", -1, motivo: "Contagem fisica encontrou uma a menos");

        var estoque = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");
        var encontrado = estoque!.Single(i => i.Id == item);

        // 10 - 3 - 1 = 6. Nenhuma coluna guarda esse número: ele é somado na consulta.
        Assert.Equal(6m, encontrado.Balance);
    }

    [Fact]
    public async Task Item_abaixo_do_minimo_e_sinalizado()
    {
        var item = await CriarItem($"Acido hialuronico {Guid.NewGuid():N}", "ml", minimo: 20);
        await Movimentar(item, "Entrada", 8);

        var estoque = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");
        var encontrado = estoque!.Single(i => i.Id == item);

        Assert.True(encontrado.BelowMinimum);
    }

    [Fact]
    public async Task Ajuste_sem_motivo_e_recusado()
    {
        var item = await CriarItem($"Luva {Guid.NewGuid():N}", "caixa", minimo: 1);

        var resposta = await _client.PostAsJsonAsync($"/stock/{item}/movements", new
        {
            type = "Ajuste",
            quantity = -2m,
        });

        // Ajuste sem motivo vira buraco inexplicável na auditoria.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Cobranca_com_vencimento_no_passado_aparece_como_vencida()
    {
        var atendimento = await CriarAtendimento();

        var criada = await _client.PostAsJsonAsync("/payments", new
        {
            appointmentId = atendimento,
            amount = 250m,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
        });
        criada.EnsureSuccessStatusCode();

        var cobrancas = await _client.GetFromJsonAsync<List<PaymentResponse>>("/payments?somenteVencidos=true");

        // "Vencido" não é status gravado: é conta feita na consulta. Por isso nenhuma
        // rotina diária precisa rodar para que esta cobrança apareça aqui.
        Assert.Contains(cobrancas!, c => c.AppointmentId == atendimento && c.Overdue);
    }

    [Fact]
    public async Task Baixa_marca_como_pago_e_registra_a_data()
    {
        var atendimento = await CriarAtendimento();

        var criada = await _client.PostAsJsonAsync("/payments", new
        {
            appointmentId = atendimento,
            amount = 180m,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        var id = await criada.Content.ReadFromJsonAsync<Guid>();

        var baixa = await _client.PostAsJsonAsync($"/payments/{id}/baixa", new { method = "Pix" });
        Assert.Equal(HttpStatusCode.NoContent, baixa.StatusCode);

        var cobrancas = await _client.GetFromJsonAsync<List<PaymentResponse>>("/payments?status=Pago");
        var paga = cobrancas!.Single(c => c.Id == id);

        Assert.Equal("Pix", paga.Method);
        Assert.NotNull(paga.PaidAt);
        Assert.False(paga.Overdue);
    }

    // --- apoio ------------------------------------------------------------

    private async Task<Guid> CriarItem(string nome, string unidade, decimal minimo)
    {
        var resposta = await _client.PostAsJsonAsync("/stock", new
        {
            name = $"{nome} {Guid.NewGuid():N}"[..40],
            unit = unidade,
            purchaseUnit = unidade,
            contentPerUnit = 1m,
            controlMode = "Informado",
            minimumQuantity = minimo,
        });
        resposta.EnsureSuccessStatusCode();

        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task Movimentar(Guid item, string tipo, decimal quantidade, string? motivo = null)
    {
        var resposta = await _client.PostAsJsonAsync($"/stock/{item}/movements", new
        {
            type = tipo,
            quantity = quantidade,
            reason = motivo,
        });
        resposta.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CriarAtendimento()
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
            durationMinutes = 30,
            price = 200m,
            suppliesCost = 20m,
        });
        var procedimentoCriado = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var profissional = await _client.PostAsJsonAsync("/professionals", new
        {
            fullName = $"Dra. {sufixo}",
            displayName = $"Dra. {sufixo}",
        });
        var profissionalCriado = await profissional.Content.ReadFromJsonAsync<ProfessionalResponse>();

        var atendimento = await _client.PostAsJsonAsync("/appointments", new
        {
            patientId = pacienteCriado!.Id,
            procedureId = procedimentoCriado!.Id,
            professionalId = profissionalCriado!.Id,
            startsAt = new DateTimeOffset(2027, 5, 10, 9, 0, 0, TimeSpan.Zero),
        });
        atendimento.EnsureSuccessStatusCode();

        return await atendimento.Content.ReadFromJsonAsync<Guid>();
    }
}
