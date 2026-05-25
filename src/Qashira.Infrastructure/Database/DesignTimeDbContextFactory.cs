using Qashira.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qashira.Infrastructure.Database;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QashiraDbContext>
{
    public QashiraDbContext CreateDbContext(string[] args)
    {
        AppPaths.EnsureDataDirectories();
        var options = new DbContextOptionsBuilder<QashiraDbContext>()
            .UseSqlite($"Data Source={AppPaths.DatabasePath}")
            .Options;

        return new QashiraDbContext(options);
    }
}
