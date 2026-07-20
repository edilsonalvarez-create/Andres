using Microsoft.Data.SqlClient;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Validation;

/// <summary>
/// Compara esquemas de dos bases SQL Server: tablas, columnas, índices,
/// llaves foráneas, procedimientos almacenados, triggers y conteo de registros.
/// </summary>
public class SqlServerSchemaValidator : IDatabaseSchemaValidator
{
    private readonly ISqlHostGuard _sqlHostGuard;

    public SqlServerSchemaValidator(ISqlHostGuard sqlHostGuard)
    {
        _sqlHostGuard = sqlHostGuard;
    }

    public async Task<IReadOnlyList<SchemaDifference>> CompareAsync(
        string sourceConnectionString, string targetConnectionString, CancellationToken ct = default)
    {
        // Sprint 18-A (B2): defensa en profundidad — validar destino ANTES de abrir SqlConnection,
        // aunque el Upsert ya lo haya validado (cubre entornos persistidos antes del guard y config).
        EnsureAllowedDestination(sourceConnectionString, "origen");
        EnsureAllowedDestination(targetConnectionString, "destino");

        var source = await SnapshotAsync(sourceConnectionString, ct);
        var target = await SnapshotAsync(targetConnectionString, ct);
        var differences = new List<SchemaDifference>();

        CompareSets(differences, "Tablas", source.Tables, target.Tables);
        CompareSets(differences, "Columnas", source.Columns, target.Columns);
        CompareSets(differences, "Índices", source.Indexes, target.Indexes);
        CompareSets(differences, "Llaves foráneas", source.ForeignKeys, target.ForeignKeys);
        CompareSets(differences, "Procedimientos", source.Procedures, target.Procedures);
        CompareSets(differences, "Triggers", source.Triggers, target.Triggers);
        CompareSets(differences, "Migraciones", source.Migrations, target.Migrations);

        foreach (var (table, sourceCount) in source.RowCounts)
        {
            if (target.RowCounts.TryGetValue(table, out var targetCount) && sourceCount != targetCount)
                differences.Add(new SchemaDifference("Registros", table,
                    $"Conteo difiere: origen={sourceCount}, destino={targetCount}"));
        }

        return differences;
    }

    /// <summary>
    /// Lanza InvalidOperationException con mensaje legible (sin credenciales) si el destino
    /// no está permitido. El job Hangfire la captura y marca la corrida como fallida.
    /// </summary>
    private void EnsureAllowedDestination(string connectionString, string role)
    {
        var result = _sqlHostGuard.ValidateConnectionString(connectionString);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Entorno {role} rechazado: {result.Error}");
    }

    private static void CompareSets(List<SchemaDifference> differences, string category,
        HashSet<string> source, HashSet<string> target)
    {
        foreach (var item in source.Except(target))
            differences.Add(new SchemaDifference(category, item, "Existe en origen pero no en destino"));
        foreach (var item in target.Except(source))
            differences.Add(new SchemaDifference(category, item, "Existe en destino pero no en origen"));
    }

    private record SchemaSnapshot(
        HashSet<string> Tables, HashSet<string> Columns, HashSet<string> Indexes,
        HashSet<string> ForeignKeys, HashSet<string> Procedures, HashSet<string> Triggers,
        HashSet<string> Migrations, Dictionary<string, long> RowCounts);

    private static async Task<SchemaSnapshot> SnapshotAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var tables = await QuerySetAsync(connection,
            "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", ct);
        var columns = await QuerySetAsync(connection,
            "SELECT TABLE_SCHEMA + '.' + TABLE_NAME + '.' + COLUMN_NAME + ' (' + DATA_TYPE + ')' FROM INFORMATION_SCHEMA.COLUMNS", ct);
        var indexes = await QuerySetAsync(connection,
            @"SELECT s.name + '.' + t.name + '.' + i.name
              FROM sys.indexes i
              JOIN sys.tables t ON i.object_id = t.object_id
              JOIN sys.schemas s ON t.schema_id = s.schema_id
              WHERE i.name IS NOT NULL", ct);
        var foreignKeys = await QuerySetAsync(connection,
            "SELECT CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_TYPE = 'FOREIGN KEY'", ct);
        var procedures = await QuerySetAsync(connection,
            "SELECT ROUTINE_SCHEMA + '.' + ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE'", ct);
        var triggers = await QuerySetAsync(connection,
            @"SELECT s.name + '.' + tr.name
              FROM sys.triggers tr
              JOIN sys.tables t ON tr.parent_id = t.object_id
              JOIN sys.schemas s ON t.schema_id = s.schema_id", ct);

        // Historial de migraciones EF Core (si el ambiente lo usa).
        var migrations = await QuerySetAsync(connection,
            @"IF OBJECT_ID('dbo.__EFMigrationsHistory') IS NOT NULL
                  SELECT MigrationId FROM dbo.__EFMigrationsHistory", ct);

        var rowCounts = new Dictionary<string, long>();
        await using (var command = new SqlCommand(
            @"SELECT s.name + '.' + t.name, SUM(p.rows)
              FROM sys.tables t
              JOIN sys.schemas s ON t.schema_id = s.schema_id
              JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
              GROUP BY s.name, t.name", connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                rowCounts[reader.GetString(0)] = reader.GetInt64(1);
        }

        return new SchemaSnapshot(tables, columns, indexes, foreignKeys, procedures, triggers, migrations, rowCounts);
    }

    private static async Task<HashSet<string>> QuerySetAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            set.Add(reader.GetString(0));
        return set;
    }
}
