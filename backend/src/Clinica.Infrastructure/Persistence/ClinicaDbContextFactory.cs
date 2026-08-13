using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinica.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelas ferramentas do EF (<c>dotnet ef migrations</c>).
///
/// Conecta como <c>clinica_owner</c>, o dono das tabelas — nao como o usuario de runtime.
/// Essa separacao e o que faz o RLS valer de verdade em producao (ver ADR 0001): o dono
/// da tabela ignora as policies, entao ele so pode aparecer aqui, nunca na API.
/// </summary>
public class ClinicaDbContextFactory : IDesignTimeDbContextFactory<ClinicaDbContext>
{
    public ClinicaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CLINICA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=clinica;Username=clinica_owner;Password=owner_dev_only";

        var options = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ClinicaDbContext(options, new NullTenantContext());
    }
}
