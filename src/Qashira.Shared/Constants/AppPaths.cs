namespace Qashira.Shared.Constants;

public static class AppPaths
{
    public const string AppFolderName = "Qashira";
    public const string DatabaseFileName = "qashira.db";
    public const string LicenseFileName = "license.lic";
    public const string MachineCodeFileName = "machine-code.txt";
    public const string ActivationRequestFileName = "activation-request.json";

    public static string ProgramDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

    public static string DatabaseDirectory => Path.Combine(ProgramDataRoot, "database");
    public static string LogsPath => Path.Combine(ProgramDataRoot, "logs");
    public static string BackupsPath => Path.Combine(ProgramDataRoot, "backups");
    public static string SettingsPath => Path.Combine(ProgramDataRoot, "settings");
    public static string LicensePath => Path.Combine(ProgramDataRoot, "license");
    public static string LogExportsPath => Path.Combine(ProgramDataRoot, "log-exports");
    public static string DatabasePath => Path.Combine(DatabaseDirectory, DatabaseFileName);
    public static string LicenseFilePath => Path.Combine(LicensePath, LicenseFileName);
    public static string MachineCodePath => Path.Combine(LicensePath, MachineCodeFileName);
    public static string ActivationRequestPath => Path.Combine(LicensePath, ActivationRequestFileName);

    public static void EnsureDataDirectories()
    {
        Directory.CreateDirectory(ProgramDataRoot);
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(BackupsPath);
        Directory.CreateDirectory(SettingsPath);
        Directory.CreateDirectory(LicensePath);
        Directory.CreateDirectory(LogExportsPath);
    }
}
