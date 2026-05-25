using System.Globalization;

namespace Qashira.Shared.Formatting;

public static class EgyptianFormat
{
    private static readonly CultureInfo Culture = new("ar-EG");

    public static string Currency(decimal value) => string.Create(Culture, $"{value:0.00} ج.م");

    public static string DateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy - hh:mm tt", Culture);
}
