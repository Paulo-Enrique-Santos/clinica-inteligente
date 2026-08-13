using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Cliente HTTP autenticado como um usuario da clinica informada.</summary>
    public HttpClient CreateClientFor(Guid tenantId, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantId.ToString());

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }
}
