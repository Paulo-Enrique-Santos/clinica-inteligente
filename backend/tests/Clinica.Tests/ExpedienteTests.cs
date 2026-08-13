using System.Net;
using System.Net.Http.Json;
using Clinica.Api.Endpoints;
using Clinica.Tests.Infra;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Expediente da profissional e encaixe de atendimento.
///
/// A regra que estes testes protegem é a do procedimento longo: um atendimento de duas
/// horas não pode começar às 17h se o expediente fecha às 18h — nem invadir o almoço.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ExpedienteTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid BellaFace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    // Segunda-feira.
    private static readonly DateOnly Segunda = new(2027, 10, 4);

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
    public async Task Procedimento_longo_nao_cabe_perto_do_fim_do_expediente()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", "13:00", "14:00");
        var procedimento = await CriarProcedimento(120);
        var paciente = await CriarPaciente();

        // 17h + 2h = 19h, uma hora depois de a clínica fechar.
        var resposta = await Agendar(paciente, procedimento, profissional, Segunda, 17, 0);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Mesmo_procedimento_cabe_mais_cedo()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", "13:00", "14:00");
        var procedimento = await CriarProcedimento(120);
        var paciente = await CriarPaciente();

        // 15h + 2h = 17h: dentro do expediente e depois do almoço.
        var resposta = await Agendar(paciente, procedimento, profissional, Segunda, 15, 0);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Atendimento_nao_pode_invadir_o_almoco()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", "13:00", "14:00");
        var procedimento = await CriarProcedimento(60);
        var paciente = await CriarPaciente();

        // 12h30 + 1h = 13h30, entrando meia hora no almoço.
        var resposta = await Agendar(paciente, procedimento, profissional, Segunda, 12, 30);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Profissional_sem_nenhum_expediente_aceita_qualquer_horario()
    {
        var profissional = await CriarProfissional();
        var procedimento = await CriarProcedimento(60);
        var paciente = await CriarPaciente();

        // Nenhum expediente cadastrado. Recusar aqui tornaria o sistema inutilizável
        // no dia da implantação, quando a clínica ainda não preencheu o quadro.
        var resposta = await Agendar(paciente, procedimento, profissional, Segunda, 7, 0);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Dia_sem_expediente_nao_aceita_agendamento()
    {
        var profissional = await CriarProfissional();
        // Expediente só na segunda; o teste tenta no domingo.
        await DefinirExpediente(profissional, "09:00", "18:00", null, null);
        var procedimento = await CriarProcedimento(30);
        var paciente = await CriarPaciente();

        var domingo = Segunda.AddDays(-1);
        var resposta = await Agendar(paciente, procedimento, profissional, domingo, 10, 0);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Excecao_de_folga_fecha_o_dia()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", null, null);
        var procedimento = await CriarProcedimento(30);
        var paciente = await CriarPaciente();

        var excecao = await _client.PostAsJsonAsync(
            $"/professionals/{profissional}/schedule/exceptions",
            new { date = Segunda, closed = true, reason = "Congresso" });
        excecao.EnsureSuccessStatusCode();

        var resposta = await Agendar(paciente, procedimento, profissional, Segunda, 10, 0);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Horarios_oferecidos_respeitam_expediente_e_almoco()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", "13:00", "14:00");
        var procedimento = await CriarProcedimento(120);

        var livres = await _client.GetFromJsonAsync<string[]>(
            $"/professionals/{profissional}/slots?data={Segunda:yyyy-MM-dd}&procedimentoId={procedimento}");

        Assert.NotNull(livres);

        // Último começo possível é 16h (termina 18h). 16h15 já estouraria.
        Assert.Contains("16:00", livres);
        Assert.DoesNotContain("16:15", livres);

        // 12h30 invadiria o almoço; 11h termina exatamente às 13h e serve.
        Assert.DoesNotContain("12:30", livres);
        Assert.Contains("11:00", livres);
    }

    [Fact]
    public async Task Horario_ja_ocupado_some_da_lista_de_livres()
    {
        var profissional = await CriarProfissional();
        await DefinirExpediente(profissional, "09:00", "18:00", null, null);
        var procedimento = await CriarProcedimento(60);
        var paciente = await CriarPaciente();

        var criado = await Agendar(paciente, procedimento, profissional, Segunda, 10, 0);
        criado.EnsureSuccessStatusCode();

        var livres = await _client.GetFromJsonAsync<string[]>(
            $"/professionals/{profissional}/slots?data={Segunda:yyyy-MM-dd}&procedimentoId={procedimento}");

        Assert.DoesNotContain("10:00", livres!);
        // 09h30 tambem nao: terminaria as 10h30, em cima do atendimento existente.
        Assert.DoesNotContain("09:30", livres!);
        Assert.Contains("09:00", livres!);
    }

    // --- apoio ------------------------------------------------------------

    private Task<HttpResponseMessage> Agendar(
        Guid paciente, Guid procedimento, Guid profissional,
        DateOnly dia, int hora, int minuto) =>
        _client.PostAsJsonAsync("/appointments", new
        {
            patientId = paciente,
            procedureId = procedimento,
            professionalId = profissional,
            startsAt = new DateTimeOffset(dia.Year, dia.Month, dia.Day, hora, minuto, 0, Fuso),
        });

    private async Task DefinirExpediente(
        Guid profissional, string inicio, string fim, string? almocoInicio, string? almocoFim)
    {
        var resposta = await _client.PutAsJsonAsync($"/professionals/{profissional}/schedule", new
        {
            dias = new[]
            {
                new
                {
                    dayOfWeek = (int)DayOfWeek.Monday,
                    startsAt = inicio,
                    endsAt = fim,
                    breakStartsAt = almocoInicio,
                    breakEndsAt = almocoFim,
                },
            },
        });

        resposta.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CriarProfissional()
    {
        var nome = $"Dra. {Guid.NewGuid():N}"[..20];
        var r = await _client.PostAsJsonAsync("/professionals",
            new { fullName = nome, displayName = nome });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProfessionalResponse>())!.Id;
    }

    private async Task<Guid> CriarProcedimento(int minutos)
    {
        var r = await _client.PostAsJsonAsync("/procedures", new
        {
            name = $"Proc {Guid.NewGuid():N}"[..20],
            durationMinutes = minutos,
            price = 300m,
            suppliesCost = 50m,
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProcedureResponse>())!.Id;
    }

    private async Task<Guid> CriarPaciente()
    {
        var r = await _client.PostAsJsonAsync("/patients", new
        {
            fullName = $"Paciente {Guid.NewGuid():N}"[..20],
            phoneE164 = $"+5511{Random.Shared.Next(100000000, 999999999)}",
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<PatientResponse>())!.Id;
    }
}
