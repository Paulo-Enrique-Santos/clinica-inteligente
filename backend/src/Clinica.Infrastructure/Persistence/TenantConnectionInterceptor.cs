using System.Data.Common;
using Clinica.Domain.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clinica.Infrastructure.Persistence;

/// <summary>
/// Publica o tenant da requisicao na sessao do Postgres, para que as policies de RLS
/// tenham o que comparar.
///
/// Roda na abertura da conexao. Isso funciona com pool porque o Npgsql executa
/// <c>DISCARD ALL</c> ao devolver a conexao, entao o valor nao vaza de uma requisicao
/// para a seguinte.
///
/// Sem tenant resolvido, nao define nada — e a policy usa <c>current_setting(..., true)</c>,
/// que devolve NULL nesse caso e nao casa com linha alguma. Negar por padrao.
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!tenant.IsResolved)
        {
            return;
        }

        using var command = CreateCommand(connection, tenant.TenantId);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsResolved)
        {
            return;
        }

        await using var command = CreateCommand(connection, tenant.TenantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, Guid tenantId)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantId.ToString();
        command.Parameters.Add(parameter);

        return command;
    }
}
