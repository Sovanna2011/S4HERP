using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace S4HERP.BuildingBlocks.Infrastructure;

/// <summary>
/// Stamps the tenant onto the SQL Server session context every time a connection
/// opens, which is what SQL Server row-level security reads.
///
/// This is the second half of ADR-03. The EF global query filter covers ordinary
/// LINQ; this covers everything else — raw SQL, the table browser, reporting
/// views — because those are precisely the paths the ORM filter cannot reach.
/// Connections are pooled, so the value must be set on every open, not once.
/// </summary>
public class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string Sql = """
        EXEC sys.sp_set_session_context @key = N'TenantId',      @value = @tenantId,   @read_only = 0;
        EXEC sys.sp_set_session_context @key = N'IsCrossTenant', @value = @crossTenant, @read_only = 0;
        """;

    public override void ConnectionOpened(
        DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void Apply(DbConnection connection)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;

        var tenant = command.CreateParameter();
        tenant.ParameterName = "@tenantId";
        tenant.Value = tenantContext.TenantId;
        command.Parameters.Add(tenant);

        var crossTenant = command.CreateParameter();
        crossTenant.ParameterName = "@crossTenant";
        crossTenant.Value = tenantContext.IsCrossTenant ? 1 : 0;
        command.Parameters.Add(crossTenant);

        return command;
    }
}
