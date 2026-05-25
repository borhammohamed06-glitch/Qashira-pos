using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qashira.Infrastructure.Database;
using Xunit;

namespace Qashira.Application.Tests;

public sealed class BackupValidationTests
{
    [Fact]
    public async Task ValidateBackupAsync_accepts_current_schema_with_printing_service_tables()
    {
        using var database = await MigratedDatabase.CreateAsync();
        var storage = new SqliteBackupStorage(database.DbContext);

        var isValid = await storage.ValidateBackupAsync(database.DatabasePath);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateBackupAsync_rejects_schema_missing_printing_service_tables()
    {
        using var database = await MigratedDatabase.CreateAsync();
        await database.DbContext.Database.ExecuteSqlRawAsync("DROP TABLE PrintingMaterialConsumptions;");
        var storage = new SqliteBackupStorage(database.DbContext);

        var isValid = await storage.ValidateBackupAsync(database.DatabasePath);

        Assert.False(isValid);
    }

    private sealed class MigratedDatabase : IDisposable
    {
        private readonly string _directoryPath;

        private MigratedDatabase(string directoryPath, string databasePath, QashiraDbContext dbContext)
        {
            _directoryPath = directoryPath;
            DatabasePath = databasePath;
            DbContext = dbContext;
        }

        public string DatabasePath { get; }

        public QashiraDbContext DbContext { get; }

        public static async Task<MigratedDatabase> CreateAsync()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"qashira-backup-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "qashira.db");

            var options = new DbContextOptionsBuilder<QashiraDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString())
                .Options;

            var dbContext = new QashiraDbContext(options);
            await dbContext.Database.MigrateAsync();

            return new MigratedDatabase(directoryPath, databasePath, dbContext);
        }

        public void Dispose()
        {
            DbContext.Dispose();
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, recursive: true);
            }
        }
    }
}
