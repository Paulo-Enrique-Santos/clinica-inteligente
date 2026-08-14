using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Compra-se em embalagem, consome-se em conteúdo.
///
/// O caso que motivou tudo: um frasco de 10ml entra no estoque, a doutora usa 7ml, e o
/// sistema precisa saber que ainda há 3ml aproveitáveis — em vez de considerar o frasco
/// inteiro consumido.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EstoquePorEmbalagemTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ClinicaApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ClinicaApiFactory(postgres.AppConnectionString);
        _client = _factory.CreateClientFor(BellaFace, "OWNER", "FINANCE");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Frasco_de_10ml_com_7ml_usados_deixa_3ml_aproveitaveis()
    {
        var item = await CriarItem("Preenchimento", "ml", "frasco", conteudo: 10, "Informado");

        // Entrada digitada como se compra: 1 frasco, não 10 ml.
        await Movimentar(item, "Entrada", 1, emEmbalagens: true);
        await Movimentar(item, "Saida", 7);

        var achado = await Buscar(item);

        Assert.Equal(3m, achado.Balance);
        Assert.Equal(0, achado.ClosedPackages);
        Assert.Equal(3m, achado.OpenRemainder);
    }

    [Fact]
    public async Task Saldo_se_reparte_entre_embalagens_fechadas_e_sobra_aberta()
    {
        var item = await CriarItem("Toxina", "ml", "frasco", conteudo: 10, "Informado");

        await Movimentar(item, "Entrada", 3, emEmbalagens: true); // 30 ml
        await Movimentar(item, "Saida", 7);                        // 23 ml

        var achado = await Buscar(item);

        Assert.Equal(23m, achado.Balance);
        Assert.Equal(2, achado.ClosedPackages);
        Assert.Equal(3m, achado.OpenRemainder);
    }

    [Fact]
    public async Task Abrir_embalagem_da_baixa_do_conteudo_inteiro()
    {
        var item = await CriarItem("Luva", "par", "caixa", conteudo: 50, "PorAbertura");

        await Movimentar(item, "Entrada", 2, emEmbalagens: true); // 100 pares

        var abrir = await _client.PostAsJsonAsync($"/stock/{item}/abrir", new { });
        abrir.EnsureSuccessStatusCode();

        var achado = await Buscar(item);

        // Sai a caixa inteira. Quantas luvas rasgaram ou sobraram, o sistema não
        // pretende saber — a clínica também não sabe.
        Assert.Equal(50m, achado.Balance);
        Assert.Equal(1, achado.ClosedPackages);
        Assert.Equal(0m, achado.OpenRemainder);
    }

    [Fact]
    public async Task Fechamento_do_atendimento_so_oferece_insumo_mensuravel()
    {
        var mensuravel = await CriarItem("Acido", "ml", "frasco", conteudo: 5, "Informado");
        var porAbertura = await CriarItem("Creme", "ml", "frasco", conteudo: 200, "PorAbertura");

        var informados = await _client.GetFromJsonAsync<List<StockItemResponse>>(
            "/stock?modo=Informado");

        // Pedir "quanto de creme você usou?" produziria número inventado.
        Assert.Contains(informados!, i => i.Id == mensuravel);
        Assert.DoesNotContain(informados!, i => i.Id == porAbertura);
    }

    [Fact]
    public async Task Conteudo_por_embalagem_zerado_e_recusado()
    {
        var resposta = await _client.PostAsJsonAsync("/stock", new
        {
            name = $"Invalido {Guid.NewGuid():N}"[..20],
            unit = "ml",
            purchaseUnit = "frasco",
            contentPerUnit = 0m,
            controlMode = "Informado",
            minimumQuantity = 0m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // --- apoio ------------------------------------------------------------

    private async Task<StockItemResponse> Buscar(Guid id)
    {
        var itens = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");
        return itens!.Single(i => i.Id == id);
    }

    private async Task<Guid> CriarItem(
        string nome, string unidade, string embalagem, decimal conteudo, string modo)
    {
        var nomeUnico = $"{nome} {Guid.NewGuid():N}"[..24];

        var r = await _client.PostAsJsonAsync("/stock", new
        {
            name = nomeUnico,
            unit = unidade,
            purchaseUnit = embalagem,
            contentPerUnit = conteudo,
            controlMode = modo,
            minimumQuantity = 0m,
        });
        r.EnsureSuccessStatusCode();

        var itens = await _client.GetFromJsonAsync<List<StockItemResponse>>("/stock");
        return itens!.Single(i => i.Name == nomeUnico).Id;
    }

    private async Task Movimentar(
        Guid item, string tipo, decimal quantidade, bool emEmbalagens = false)
    {
        var r = await _client.PostAsJsonAsync($"/stock/{item}/movements", new
        {
            type = tipo,
            quantity = quantidade,
            inPurchaseUnits = emEmbalagens,
        });
        r.EnsureSuccessStatusCode();
    }
}
