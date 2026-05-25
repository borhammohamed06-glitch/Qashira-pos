namespace Qashira.Application.DTOs;

public sealed record BackupFileDto(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTimeOffset CreatedAt);

public sealed record BackupOperationDto(
    string BackupPath,
    string? SafetyBackupPath = null,
    string? ExportPath = null);

public sealed record LogExportOperationDto(
    string ExportPath,
    int FileCount);
