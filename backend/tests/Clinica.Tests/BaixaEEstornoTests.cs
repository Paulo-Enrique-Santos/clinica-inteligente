using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Quando a cobrança já nasce efetivada, e o que fazer quando o dinheiro volta.
///
/// A regra da clínica: dinheiro e cartão entram efetivados, porque o dinheiro já passou.
/// Só o PIX parcelado fica pendente — cada parcela depende de a paciente lembrar.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BaixaEEstornoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _doutora = null!;
    private HttpClient _recepcao = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _doutora = _factory.CreateClientFor(BellaFace, "DOCTOR");
        _recepcao = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY", "FINANCE");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Cartao_parcelado_entra_todo_efetivado()
    {
        var cobrancas = await Fechar("Parcelado", "Credito", parcelas: 3);

        Assert.Equal(3, cobrancas.Count);
        Assert.All(cobrancas, c => Assert.Equal("Pago", c.Status));
        // Ficar pendente obrigaria a recepção a dar baixa em algo que a maquininha já
        // aprovou.
        Assert.All(cobrancas, c => Assert.NotNull(c.PaidAt));
    }

    [Fact]
    public async Task Pix_parcelado_fica_pendente()
    {
        var cobrancas = await Fechar("Parcelado", "Pix", parcelas: 3);

        Assert.Equal(3, cobrancas.Count);
        Assert.All(cobrancas, c => Assert.Equal("Pendente", c.Status));
    }

    [Fact]
    public async Task Pix_com_sinal_efetiva_so_o_sinal()
    {
        var cobrancas = await Fechar("SinalMaisParcelas", "Pix", parcelas: 2, sinal: 300m);

        var sinal = cobrancas.Single(c => c.InstallmentNumber == 1);
        var parcelas = cobrancas.Where(c => c.InstallmentNumber > 1).ToList();

        // O sinal é pago na hora; o resto depende da paciente lembrar.
        Assert.Equal("Pago", sinal.Status);
        Assert.All(parcelas, c => Assert.Equal("Pendente", c.Status));
    }

    [Fact]
    public async Task Pix_a_vista_entra_efetivado()
    {
        var cobrancas = await Fechar("AVista", "Pix");

        Assert.Equal("Pago", Assert.Single(cobrancas).Status);
    }

    [Fact]
    public async Task Estorno_marca_como_estornada_e_preserva_a_data_do_pagamento()
    {
        var cobrancas = await Fechar("AVista", "Credito");
        var cobranca = cobrancas.Single();

        var estorno = await _recepcao.PostAsJsonAsync(
            $"/payments/{cobranca.Id}/estornar",
            new { motivo = "Cartao recusado pela operadora" });

        Assert.Equal(HttpStatusCode.NoContent, estorno.StatusCode);

        var depois = await Listar("Estornado");
        var estornada = depois.Single(c => c.Id == cobranca.Id);

        Assert.Equal("Estornado", estornada.Status);
        // Saber quando entrou continua importando depois de voltar: é o que permite
        // conciliar com a maquininha.
        Assert.NotNull(estornada.PaidAt);
    }

    [Fact]
    public async Task Nao_se_estorna_o_que_nunca_entrou()
    {
        var cobrancas = await Fechar("Parcelado", "Pix", parcelas: 2);

        var resposta = await _recepcao.PostAsJsonAsync(
            $"/payments/{cobrancas[0].Id}/estornar", new { motivo = "engano" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    // --- apoio ------------------------------------------------------------

    private async Task<List<PaymentResponse>> Listar(string status) =>
        await _recepcao.GetFromJsonAsync<List<PaymentResponse>>($"/payments?status={status}") ?? [];

    private async Task<List<PaymentResponse>> Fechar(
        string forma, string meio, int parcelas = 1, decimal sinal = 0)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await _recepcao.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        var p = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var profissional = await _recepcao.PostAsJsonAsync("/professionals",
            new { fullName = $"Dra. {sufixo}", displayName = $"Dra. {sufixo}" });
        var prof = await profissional.Content.ReadFromJsonAsync<ProfessionalResponse>();

        var procedimento = await _recepcao.PostAsJsonAsync("/procedures", new
        {
            name = $"Proc {sufixo}",
            durationMinutes = 60,
            price = 900m,
            suppliesCost = 100m,
        });
        var proc = await procedimento.Content.ReadFromJsonAsync<ProcedureResponse>();

        var criado = await _doutora.PostAsJsonAsync("/treatment-plans", new
        {
            patientId = p!.Id,
            professionalId = prof!.Id,
            items = new[] { new { procedureId = proc!.Id, sessions = 1, startAfterDays = 0 } },
        });
        criado.EnsureSuccessStatusCode();
        var protocolo = await criado.Content.ReadFromJsonAsync<Guid>();

        var protocolos = await _recepcao.GetFromJsonAsync<List<TreatmentPlanResponse>>("/treatment-plans");
        var itens = protocolos!.Single(x => x.Id == protocolo).Items;

        var orcamento = await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", new
        {
            acceptedItemIds = itens.Select(i => i.Id).ToArray(),
            forma,
            meio,
            primeiroVencimento = new DateOnly(2027, 4, 10),
            parcelas,
            sinal,
        });
        orcamento.EnsureSuccessStatusCode();

        var todas = new List<PaymentResponse>();
        foreach (var status in new[] { "Pendente", "Pago" })
        {
            todas.AddRange(await Listar(status));
        }

        return todas.Where(c => c.TreatmentPlanId == protocolo).ToList();
    }
}
