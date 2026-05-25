namespace Qashira.Application.Abstractions;

public interface IAutomaticBackupService
{
    Task RunStartupBackupAsync(CancellationToken cancellationToken = default);
}
