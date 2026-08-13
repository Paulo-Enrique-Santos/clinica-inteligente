using Clinica.Infrastructure;
using Clinica.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Tests.Infra;

public class ClinicaApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Usuario de runtime: e o que faz o RLS incidir tambem no teste.
                ["ConnectionStrings:Clinica"] = connectionString,

                // Endereco proposital de Keycloak inexistente: se algum caminho tentar
                // validar token de verdade, o teste quebra alto em vez de silenciosamente
                // depender de um Keycloak que por acaso esteja rodando na maquina.
                ["Keycloak:Authority"] = "https://keycloak-inexistente.invalid/realms/clinica",
                ["Keycloak:Audience"] = "clinica-api",
            }));

        builder.ConfigureTestServices(services =>
        {
            // Trocar a connection string so pela configuracao NAO e confiavel com o modelo
            // de hosting minimo: o appsettings.json da API pode vencer o override, e ai o
            // teste conversa com o banco de DESENVOLVIMENTO em localhost:5432 sem avisar
            // ninguem. Foi exatamente o que aconteceu aqui — passou na maquina e quebrou na
            // CI, que nao tem nada na 5432.
            //
            // Remover e reregistrar o DbContext e deterministico: ConfigureTestServices roda
            // depois do ConfigureServices da aplicacao, entao esta versao e a que sobra.
            var registrosDoContexto = services
                .Where(d =>
                    d.ServiceType == typeof(ClinicaDbContext) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Contains(typeof(ClinicaDbContext))))
                .ToList();

            foreach (var registro in registrosDoContexto)
            {
                services.Remove(registro);
            }

            services.AddClinicaPersistence(connectionString);

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Cliente HTTP autenticado como um usuario da clinica informada.</summary>
    public HttpClient CreateClientFor(Guid tenantId, params string[] roles) =>
        CreateClientForUser(tenantId, usuario: null, roles);

    /// <summary>
    /// Igual ao anterior, mas identificando QUAL usuario — necessario para as regras que
    /// dependem do individuo, como a doutora ver apenas a propria agenda.
    /// </summary>
    public HttpClient CreateClientForUser(Guid tenantId, string? usuario, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantId.ToString());

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, usuario);
        }

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }
}
