using Clinica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Clinica.Tests.Infra;

/// <summary>
/// Postgres real em container para os testes de integracao.
///
/// Detalhe que faz toda a diferenca aqui: o teste NAO se conecta como superusuario.
/// Superusuario ignora RLS mesmo com FORCE, entao um teste que usasse o usuario padrao
/// do Testcontainers passaria mesmo que a policy estivesse errada — estaria medindo
/// apenas o filtro do EF Core e nos daria uma falsa sensacao de seguranca.
///
/// Por isso o fixture reproduz a separacao de papeis da producao: migra como
/// <c>clinica_owner</c> e roda a aplicacao como <c>clinica_app</c>.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string OwnerPassword = "owner_test";
    private const string AppPassword = "app_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("clinica")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    public string OwnerConnectionString { get; private set; } = string.Empty;

    /// <summary>Usado pela API. Sem privilegio de dono — e sobre ele que o RLS incide.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5432);

        OwnerConnectionString = $"Host={host};Port={port};Database=clinica;Username=clinica_owner;Password={OwnerPassword}";
        AppConnectionString = $"Host={host};Port={port};Database=clinica;Username=clinica_app;Password={AppPassword}";

        await _container.ExecScriptAsync($"""
            CREATE ROLE clinica_owner LOGIN PASSWORD '{OwnerPassword}';
            CREATE ROLE clinica_app   LOGIN PASSWORD '{AppPassword}';

            GRANT ALL ON SCHEMA public TO clinica_owner;
            GRANT USAGE ON SCHEMA public TO clinica_app;

            -- Extensoes sao provisionamento de banco, nao migration: exigem privilegio
            -- que o usuario de migration nao tem. Aqui reproduzimos o que o
            -- infra/postgres/init faz em desenvolvimento e producao.
            CREATE EXTENSION IF NOT EXISTS btree_gist;
            """);

        // Migrations rodam como dono, exatamente como em producao.
        var options = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new ClinicaDbContext(options, new NullTenantContext());
        await db.Database.MigrateAsync();

        await _container.ExecScriptAsync("""
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO clinica_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO clinica_app;
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
