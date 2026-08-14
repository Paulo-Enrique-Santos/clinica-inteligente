using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Fluxo da clínica: a doutora prescreve, a recepção fecha o valor.
///
/// A separação de papéis aqui não é burocracia — é o desenho do negócio. Quem cuida do
/// clínico propõe; quem cuida do dinheiro negocia.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ProtocoloTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _doutora = null!;
    private HttpClient _recepcao = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _doutora = _factory.CreateClientFor(BellaFace, "DOCTOR");
        _recepcao = _factory.CreateClientFor(BellaFace, "SECRETARY", "FINANCE");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Recepcao_nao_prescreve_protocolo()
    {
        var c = await Cenario();

        var resposta = await _recepcao.PostAsJsonAsync("/treatment-plans", new
        {
            patientId = c.Paciente,
            professionalId = c.Profissional,
            items = new[] { new { procedureId = c.Limpeza, sessions = 1, startAfterDays = 0 } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Doutora_nao_fecha_orcamento()
    {
        var c = await Cenario();
        var protocolo = await Prescrever(c);

        var resposta = await _doutora.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", new
        {
            acceptedItemIds = Array.Empty<Guid>(),
            forma = "AVista",
            meio = "Pix",
            primeiroVencimento = new DateOnly(2027, 6, 1),
        });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Item_recusado_fica_registrado_e_nao_entra_na_conta()
    {
        var c = await Cenario();
        var protocolo = await Prescrever(c);
        var itens = await Itens(protocolo);

        var limpeza = itens.Single(i => i.ProcedureName.StartsWith("Limpeza"));

        // A paciente quer só a limpeza; o botox fica para depois.
        var resposta = await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", new
        {
            acceptedItemIds = new[] { limpeza.Id },
            forma = "AVista",
            meio = "Pix",
            primeiroVencimento = new DateOnly(2027, 6, 1),
        });
        resposta.EnsureSuccessStatusCode();

        var depois = await Itens(protocolo);

        Assert.Equal("Aceito", depois.Single(i => i.Id == limpeza.Id).Status);
        // O botox continua no protocolo, marcado como recusado — a prescrição da
        // doutora não some porque a paciente adiou.
        Assert.Contains(depois, i => i.Status == "Recusado");

        // Sem filtro de status: desde a Fase L, parte das cobrancas ja nasce efetivada
        // dependendo da forma e do meio de pagamento.
        var cobrancas = await _recepcao.GetFromJsonAsync<List<PaymentResponse>>("/payments");
        // Filtra pelo protocolo: os testes compartilham o banco, e somar tudo que é
        // "Protocolo" pegaria as cobranças geradas pelos outros.
        var geradas = cobrancas!.Where(p => p.TreatmentPlanId == protocolo).ToList();

        // 2 sessões de limpeza a 200 = 400. O botox (1500) ficou de fora.
        Assert.Equal(400m, geradas.Sum(p => p.Amount));
    }

    [Fact]
    public async Task Sinal_mais_parcelas_gera_as_cobrancas_certas()
    {
        var c = await Cenario();
        var protocolo = await Prescrever(c);
        var itens = await Itens(protocolo);

        var resposta = await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", new
        {
            acceptedItemIds = itens.Select(i => i.Id).ToArray(),
            forma = "SinalMaisParcelas",
            meio = "Pix",
            primeiroVencimento = new DateOnly(2027, 7, 5),
            parcelas = 2,
            sinal = 500m,
        });
        resposta.EnsureSuccessStatusCode();

        // Sem filtro de status: desde a Fase L, parte das cobrancas ja nasce efetivada
        // dependendo da forma e do meio de pagamento.
        var cobrancas = await _recepcao.GetFromJsonAsync<List<PaymentResponse>>("/payments");
        // Filtra pelo protocolo: os testes compartilham o banco, e somar tudo que é
        // "Protocolo" pegaria as cobranças geradas pelos outros.
        var geradas = cobrancas!.Where(p => p.TreatmentPlanId == protocolo).ToList();

        // 400 (limpeza) + 1500 (botox) = 1900: sinal de 500 e duas parcelas de 700.
        Assert.Equal(3, geradas.Count);
        Assert.Equal(1900m, geradas.Sum(p => p.Amount));
    }

    [Fact]
    public async Task Protocolo_nao_e_aprovado_duas_vezes()
    {
        var c = await Cenario();
        var protocolo = await Prescrever(c);
        var itens = await Itens(protocolo);

        var corpo = new
        {
            acceptedItemIds = itens.Select(i => i.Id).ToArray(),
            forma = "AVista",
            meio = "Pix",
            primeiroVencimento = new DateOnly(2027, 8, 1),
        };

        (await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", corpo))
            .EnsureSuccessStatusCode();

        var segunda = await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", corpo);

        // Cobrar duas vezes o mesmo protocolo seria o pior erro possível aqui.
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Paciente_que_recusa_tudo_deixa_o_protocolo_recusado_e_sem_cobranca()
    {
        var c = await Cenario();
        var protocolo = await Prescrever(c);

        var resposta = await _recepcao.PostAsJsonAsync($"/treatment-plans/{protocolo}/orcamento", new
        {
            acceptedItemIds = Array.Empty<Guid>(),
            forma = "AVista",
            meio = "Pix",
            primeiroVencimento = new DateOnly(2027, 9, 1),
        });
        resposta.EnsureSuccessStatusCode();

        var protocolos = await _recepcao.GetFromJsonAsync<List<TreatmentPlanResponse>>("/treatment-plans");
        Assert.Equal("Recusado", protocolos!.Single(p => p.Id == protocolo).Status);
    }

    // --- apoio ------------------------------------------------------------

    private record Dados(Guid Paciente, Guid Profissional, Guid Limpeza, Guid Botox);

    private async Task<List<PlanItemResponse>> Itens(Guid protocolo)
    {
        var protocolos = await _recepcao.GetFromJsonAsync<List<TreatmentPlanResponse>>("/treatment-plans");
        return protocolos!.Single(p => p.Id == protocolo).Items;
    }

    private async Task<Guid> Prescrever(Dados c)
    {
        var resposta = await _doutora.PostAsJsonAsync("/treatment-plans", new
        {
            patientId = c.Paciente,
            professionalId = c.Profissional,
            notes = "Comecar pela limpeza; botox depois de 15 dias.",
            items = new[]
            {
                new { procedureId = c.Limpeza, sessions = 2, intervalDays = (int?)15, startAfterDays = 0 },
                new { procedureId = c.Botox, sessions = 1, intervalDays = (int?)null, startAfterDays = 15 },
            },
        });

        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Dados> Cenario()
    {
        var admin = _factory.CreateClientFor(BellaFace, "OWNER", "SECRETARY");
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var paciente = await admin.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {sufixo}",
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        var p = await paciente.Content.ReadFromJsonAsync<PatientResponse>();

        var profissional = await admin.PostAsJsonAsync("/professionals",
            new { fullName = $"Dra. {sufixo}", displayName = $"Dra. {sufixo}" });
        var prof = await profissional.Content.ReadFromJsonAsync<ProfessionalResponse>();

        var limpeza = await admin.PostAsJsonAsync("/procedures", new
        {
            name = $"Limpeza {sufixo}",
            durationMinutes = 60,
            price = 200m,
            suppliesCost = 30m,
        });
        var l = await limpeza.Content.ReadFromJsonAsync<ProcedureResponse>();

        var botox = await admin.PostAsJsonAsync("/procedures", new
        {
            name = $"Botox {sufixo}",
            durationMinutes = 45,
            price = 1500m,
            suppliesCost = 400m,
        });
        var b = await botox.Content.ReadFromJsonAsync<ProcedureResponse>();

        return new Dados(p!.Id, prof!.Id, l!.Id, b!.Id);
    }
}
