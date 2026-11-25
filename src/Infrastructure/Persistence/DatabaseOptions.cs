using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Infrastructure.Persistence;

/// <summary>
/// Database provider: postgres (main, e.g. via docker compose) or sqlite (no Docker, a file next to the Api).
/// </summary>
public static class DatabaseOptions
{
    public const string SectionProvider = "Database:Provider";
    public const string ConnectionName = "Default";

    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration[SectionProvider] ?? "postgres").Trim().ToLowerInvariant();
        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionName} is not configured");

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider)
            {
                case "postgres":
                case "npgsql":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown {SectionProvider}: '{provider}'. Use 'postgres' or 'sqlite'.");
            }
        });

        return services;
    }

    public static bool IsSqlite(this DbContext db) => db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Postgres: migrations. SQLite: EnsureCreated (migrations are generated for Npgsql), plus creating tables and
    /// columns that an existing file does not have yet, without losing data.
    /// </summary>
    public static async Task InitializeAsync(this AppDbContext db, CancellationToken ct = default)
    {
        if (!db.IsSqlite())
        {
            await db.Database.MigrateAsync(ct);
            return;
        }

        var created = await db.Database.EnsureCreatedAsync(ct);
        if (!created)
        {
            await CreateMissingSqliteTablesAsync(db, ct);
            await AddMissingSqliteColumnsAsync(db, ct);
        }
    }

    /// <summary>Columns added after a SQLite file was created: EnsureCreated does not add them.</summary>
    private static readonly (string Table, string Column, string Definition)[] SqliteAddedColumns =
    [
        ("users", "UiLanguage", "TEXT NOT NULL DEFAULT ''"),
        ("exam_tasks", "Image", "TEXT NULL"),
        ("ai_review_usages", "Kind", "TEXT NOT NULL DEFAULT 'Review'"),
    ];

    private static async Task AddMissingSqliteColumnsAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var (table, column, definition) in SqliteAddedColumns)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
            // Identifiers cannot be SQL parameters. Table, column and definition come only from the constant list above
            // (never from user input) and the identifiers are quoted, so building the statement is safe here.
#pragma warning disable EF1002
            if (!exists) await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}", ct);
#pragma warning restore EF1002
        }
    }

    private static async Task CreateMissingSqliteTablesAsync(AppDbContext db, CancellationToken ct)
    {
        var script = db.GetService<IRelationalDatabaseCreator>().GenerateCreateScript();
        var statements = script
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
            .Select(s => s
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase));

        foreach (var statement in statements)
            await db.Database.ExecuteSqlRawAsync(statement, ct);
    }
}
