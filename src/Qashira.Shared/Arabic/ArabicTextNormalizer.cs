using System.Globalization;
using System.Text;

namespace Qashira.Shared.Arabic;

public static class ArabicTextNormalizer
{
    public static string NormalizeForSearch(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var replacement = character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                'ؤ' => 'و',
                'ئ' => 'ي',
                'ـ' => '\0',
                _ => character
            };

            if (replacement != '\0')
            {
                builder.Append(replacement);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLower(new CultureInfo("ar-EG"));
    }
}
