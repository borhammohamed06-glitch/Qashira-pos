using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Qashira.App.Services;

internal static class MachineIdProvider
{
    public static string GetMachineCode(string productId)
    {
        var machineGuid = ReadMachineGuid();
        var rawValue = string.IsNullOrWhiteSpace(machineGuid)
            ? $"{Environment.MachineName}|{Environment.UserDomainName}"
            : machineGuid;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{productId}|{rawValue}"));
        var hex = Convert.ToHexString(bytes)[..24];
        return string.Join("-", Enumerable.Range(0, 6).Select(index => hex.Substring(index * 4, 4)));
    }

    private static string? ReadMachineGuid()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid") as string;
    }
}
