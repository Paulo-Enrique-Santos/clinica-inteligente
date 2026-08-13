using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinica.Infrastructure.Persistence.Migrations;

/// <summary>
/// Row Level Security por tenant — a terceira barreira da ADR 0001, a unica que continua
/// valendo se o codigo C# errar.
///
/// Toda migration que cria tabela derivada de <c>TenantEntity</c> deve chamar
/// <see cref="Enable"/>. Esquecer disso e o modo mais provavel de furar o isolamento,
/// entao existe um teste que varre o banco e falha se alguma tabela com coluna
/// <c>tenant_id</c> estiver sem policy.
/// </summary>
public static class TenantRls
{
    public const string PolicyName = "tenant_isolation";

    /// <summary>
    /// Variavel de sessao publicada pelo <c>TenantConnectionInterceptor</c> a cada
    /// abertura de conexao.
    /// </summary>
    public const string SettingName = "app.tenant_id";

    public static void Enable(MigrationBuilder migrationBuilder, string table)
    {
        // FORCE e o detalhe que quase todo mundo esquece: sem ele, o DONO da tabela
        // ignora a policy. Como as migrations rodam como dono, sem FORCE o RLS pareceria
        // ativo e nao protegeria nada em nenhum caminho que use aquele papel.
        //
        // current_setting(..., true) devolve NULL quando a variavel nao foi definida.
        // Comparar com NULL da falso, entao conexao sem tenant nao enxerga linha alguma:
        // o padrao e negar, nao permitir.
        //
        // O NULLIF nao e enfeite: depois de um RESET, a variavel vira string VAZIA em vez
        // de NULL, e ''::uuid levanta erro em vez de negar. Sem ele, um caso de borda que
        // deveria retornar "nenhuma linha" viraria erro 500.
        migrationBuilder.Sql($"""
            ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {table} FORCE ROW LEVEL SECURITY;

            CREATE POLICY {PolicyName} ON {table}
                USING (tenant_id = NULLIF(current_setting('{SettingName}', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('{SettingName}', true), '')::uuid);
            """);
    }

    public static void Disable(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.Sql($"""
            DROP POLICY IF EXISTS {PolicyName} ON {table};
            ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;
            """);
    }
}
