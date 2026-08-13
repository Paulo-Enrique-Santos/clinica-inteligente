using Clinica.Tests.Infra;
using Npgsql;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O jeito mais provavel de furar o isolamento nao e escrever codigo errado: e criar uma
/// tabela nova e esquecer de habilitar RLS nela. Ninguem revisa migration procurando por
/// ausencia de coisa.
///
/// Este teste varre o banco depois das migrations e falha se alguma tabela com coluna
/// <c>tenant_id</c> ficou sem protecao — inclusive tabelas que ainda nem existem hoje.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RlsSchemaGuardTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Toda_tabela_com_tenant_id_tem_rls_ativo_forcado_e_com_policy()
    {
        const string sql = """
            SELECT c.relname,
                   c.relrowsecurity,
                   c.relforcerowsecurity,
                   (SELECT count(*)
                      FROM pg_policies p
                     WHERE p.schemaname = 'public'
                       AND p.tablename = c.relname) AS qtd_policies
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'public'
               AND c.relkind = 'r'
               AND EXISTS (SELECT 1
                             FROM information_schema.columns col
                            WHERE col.table_schema = 'public'
                              AND col.table_name = c.relname
                              AND col.column_name = 'tenant_id')
            """;

        await using var connection = new NpgsqlConnection(postgres.OwnerConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var problemas = new List<string>();
        var tabelasVerificadas = 0;

        while (await reader.ReadAsync())
        {
            var tabela = reader.GetString(0);
            var rlsAtivo = reader.GetBoolean(1);
            var rlsForcado = reader.GetBoolean(2);
            var policies = reader.GetInt64(3);

            tabelasVerificadas++;

            if (!rlsAtivo)
            {
                problemas.Add($"{tabela}: RLS desabilitado");
            }
            else if (!rlsForcado)
            {
                problemas.Add($"{tabela}: RLS sem FORCE — o dono da tabela ignoraria a policy");
            }

            if (policies == 0)
            {
                problemas.Add($"{tabela}: nenhuma policy definida");
            }
        }

        // Se este numero for zero, o teste passou por vacuidade — o que seria pior do que
        // falhar, porque pareceria cobertura.
        Assert.True(tabelasVerificadas > 0, "Nenhuma tabela com tenant_id encontrada; as migrations rodaram?");

        Assert.True(
            problemas.Count == 0,
            "Tabelas com tenant_id sem protecao de RLS:" + Environment.NewLine +
            string.Join(Environment.NewLine, problemas) + Environment.NewLine +
            "Use TenantRls.Enable(migrationBuilder, \"<tabela>\") na migration que criou a tabela.");
    }
}
