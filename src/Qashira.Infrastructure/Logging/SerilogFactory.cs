using Qashira.Shared.Constants;
using Serilog;
using Serilog.Events;

namespace Qashira.Infrastructure.Logging;

public static class SerilogFactory
{
    public static ILogger CreateLogger()
    {
        Directory.CreateDirectory(AppPaths.LogsPath);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsPath, "app-log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 45,
                fileSizeLimitBytes: 5_000_000,
                rollOnFileSizeLimit: true)
            .WriteTo.File(
                Path.Combine(AppPaths.LogsPath, "errors-.txt"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 60,
                fileSizeLimitBytes: 5_000_000,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }
}
