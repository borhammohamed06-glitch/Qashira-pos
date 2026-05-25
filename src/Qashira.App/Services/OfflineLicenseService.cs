using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using Qashira.Shared.Constants;
using Serilog;

namespace Qashira.App.Services;

internal static class OfflineLicenseService
{
    private const string ProductId = "Qashira";
    private const string MarkerFileName = "license.required";

    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAtkVtdcEBOnjYvrpBn1S+
i2NIIu4mtVfneRDi1upb6Q6Gko0uJgmklee1bKP/oWWXS7+YrtH8qh1ZdpxHoHKv
og/G5ZOCd0BW6CGr1027MMoNtcxWyfPd9gxr6CUIjjKSzZk3209XpqNxAV4OuVRh
I7sxPKDIhWYqSQuz0yV+uFAiugfy3hw/9Hu0CeDmEJWL/q+/APD128w6+R2eCCqE
HHIWLK1AhY/Mp9dEdLugnMrd+rNHqOjtmn2vM3duVI1a43edSss+bse0ebRdXNcM
H/9SFTf2B2/i//AHuQm2qPATtzNIEQ6+L+4rb4yUmP3U8VRobtsrOR+cV5j5/JJx
azzGZxccUC0V/2mHPX85BVHaiJdJDU99RtaNntZ8yUBcVzlzjsr2jU3Aa98ys3ZU
t8tUtbuULqI4goYc5lAslySEFiJ6tSYRl2uXSnICgtrhvXqZffBGGCsK641BJySs
ME/Fw4oOiF5240TrxtmuDX+ionE4boEtDPt4fqgnW2nxAgMBAAE=
-----END PUBLIC KEY-----
""";

    public static LicenseValidationResult Validate()
    {
        if (!IsLicenseRequired())
        {
            return LicenseValidationResult.Success();
        }

        var machineCode = PrepareMachineCode();
        return ValidateLicenseFile(machineCode);
    }

    public static string PrepareMachineCode()
    {
        AppPaths.EnsureDataDirectories();
        var machineCode = MachineIdProvider.GetMachineCode(ProductId);
        File.WriteAllText(AppPaths.MachineCodePath, machineCode, Encoding.UTF8);
        WriteActivationRequest(machineCode);
        return machineCode;
    }

    public static LicenseActivationResult Activate(string activationCode)
    {
        var machineCode = PrepareMachineCode();

        try
        {
            if (string.IsNullOrWhiteSpace(activationCode))
            {
                return LicenseActivationResult.Failure("كود التفعيل غير صحيح");
            }

            var licenseJson = DecodeActivationCode(activationCode);
            var license = JsonSerializer.Deserialize<SignedLicense>(
                licenseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (license is null ||
                !string.Equals(license.ProductId, ProductId, StringComparison.Ordinal) ||
                !VerifySignature(license))
            {
                return LicenseActivationResult.Failure("كود التفعيل غير صحيح");
            }

            if (!string.Equals(license.MachineCode, machineCode, StringComparison.Ordinal))
            {
                return LicenseActivationResult.Failure("كود التفعيل لا يخص هذا الجهاز");
            }

            if (!DateTimeOffset.TryParse(license.ExpiresOn, out var expiresOn) ||
                expiresOn < DateTimeOffset.UtcNow)
            {
                return LicenseActivationResult.Failure("انتهت صلاحية الترخيص");
            }

            File.WriteAllText(
                AppPaths.LicenseFilePath,
                JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            return LicenseActivationResult.Success("تم تفعيل البرنامج بنجاح");
        }
        catch (FormatException)
        {
            return LicenseActivationResult.Failure("كود التفعيل غير صحيح");
        }
        catch (JsonException)
        {
            return LicenseActivationResult.Failure("كود التفعيل غير صحيح");
        }
        catch (CryptographicException)
        {
            return LicenseActivationResult.Failure("كود التفعيل غير صحيح");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Offline activation failed.");
            return LicenseActivationResult.Failure("حدث خطأ أثناء التفعيل");
        }
    }

    private static LicenseValidationResult ValidateLicenseFile(string machineCode)
    {
        var licensePath = AppPaths.LicenseFilePath;
        if (!File.Exists(licensePath))
        {
            return LicenseValidationResult.Failure(MissingLicenseMessage(machineCode));
        }

        try
        {
            var license = JsonSerializer.Deserialize<SignedLicense>(
                File.ReadAllText(licensePath, Encoding.UTF8),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (license is null)
            {
                return LicenseValidationResult.Failure(InvalidLicenseMessage(machineCode));
            }

            if (!string.Equals(license.ProductId, ProductId, StringComparison.Ordinal))
            {
                return LicenseValidationResult.Failure(InvalidLicenseMessage(machineCode));
            }

            if (!string.Equals(license.MachineCode, machineCode, StringComparison.Ordinal))
            {
                return LicenseValidationResult.Failure(WrongMachineMessage(machineCode));
            }

            if (!DateTimeOffset.TryParse(license.ExpiresOn, out var expiresOn) ||
                expiresOn < DateTimeOffset.UtcNow)
            {
                return LicenseValidationResult.Failure(ExpiredLicenseMessage(machineCode));
            }

            if (!VerifySignature(license))
            {
                return LicenseValidationResult.Failure(InvalidLicenseMessage(machineCode));
            }

            return LicenseValidationResult.Success();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "License validation failed.");
            return LicenseValidationResult.Failure(InvalidLicenseMessage(machineCode));
        }
    }

    private static bool IsLicenseRequired() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, MarkerFileName));

    private static bool VerifySignature(SignedLicense license)
    {
        var signature = Convert.FromBase64String(license.Signature);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(PublicKeyPem);
        return VerifyCanonicalPayload(rsa, BuildCanonicalPayload(license, includePlan: true), signature) ||
               VerifyCanonicalPayload(rsa, BuildCanonicalPayload(license, includePlan: false), signature);
    }

    private static bool VerifyCanonicalPayload(RSA rsa, string canonical, byte[] signature) =>
        rsa.VerifyData(
            Encoding.UTF8.GetBytes(canonical),
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

    private static string BuildCanonicalPayload(SignedLicense license, bool includePlan)
    {
        var values = new List<string>
        {
            license.ProductId,
            license.CustomerName,
            license.MachineCode,
            license.IssuedOn,
            license.ExpiresOn
        };

        if (includePlan)
        {
            values.Add(license.Plan ?? string.Empty);
        }

        return string.Join('\n', values);
    }

    private static string DecodeActivationCode(string activationCode)
    {
        var normalized = activationCode.Trim()
            .ReplaceLineEndings(string.Empty)
            .Replace(" ", string.Empty)
            .Replace('-', '+')
            .Replace('_', '/');

        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private static void WriteActivationRequest(string machineCode)
    {
        var request = new
        {
            ProductId,
            MachineCode = machineCode,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        File.WriteAllText(
            AppPaths.ActivationRequestPath,
            JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    private static string MissingLicenseMessage(string machineCode) =>
        $"هذا الجهاز غير مفعل.\n\nكود الجهاز:\n{machineCode}\n\nأرسل هذا الكود لصاحب النظام للحصول على ملف التفعيل، ثم ضع الملف هنا:\n{AppPaths.LicenseFilePath}";

    private static string InvalidLicenseMessage(string machineCode) =>
        $"ملف التفعيل غير صحيح.\n\nكود الجهاز:\n{machineCode}\n\nتأكد من وضع ملف تفعيل صحيح في:\n{AppPaths.LicenseFilePath}";

    private static string WrongMachineMessage(string machineCode) =>
        $"ملف التفعيل لا يخص هذا الجهاز.\n\nكود هذا الجهاز:\n{machineCode}";

    private static string ExpiredLicenseMessage(string machineCode) =>
        $"انتهت صلاحية التفعيل.\n\nكود الجهاز:\n{machineCode}";

    private sealed record SignedLicense(
        string ProductId,
        string CustomerName,
        string MachineCode,
        string IssuedOn,
        string ExpiresOn,
        string Signature,
        string? Plan = null);
}

internal sealed record LicenseValidationResult(bool IsValid, string Message)
{
    public static LicenseValidationResult Success() => new(true, string.Empty);

    public static LicenseValidationResult Failure(string message) => new(false, message);
}

internal sealed record LicenseActivationResult(bool IsValid, string Message)
{
    public static LicenseActivationResult Success(string message) => new(true, message);

    public static LicenseActivationResult Failure(string message) => new(false, message);
}
