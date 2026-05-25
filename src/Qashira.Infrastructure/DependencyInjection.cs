using Qashira.Application.Abstractions;
using Qashira.Infrastructure.Database;
using Qashira.Infrastructure.Logging;
using Qashira.Infrastructure.Security;
using Qashira.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Qashira.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        AppPaths.EnsureDataDirectories();

        services.AddDbContext<QashiraDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<QashiraDbContext>());
        services.AddScoped<IBackupStorage, SqliteBackupStorage>();
        services.AddScoped<ILogArchiveStorage, AppLogArchiveStorage>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
