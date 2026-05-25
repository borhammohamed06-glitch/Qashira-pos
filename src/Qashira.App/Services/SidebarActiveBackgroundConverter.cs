using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Qashira.App.Services;

public sealed class SidebarActiveBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(14, 124, 102));
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(34, 67, 95));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var activeWorkspace = value as string;
        var targetWorkspace = parameter as string;
        return string.Equals(activeWorkspace, targetWorkspace, StringComparison.Ordinal)
            ? ActiveBrush
            : InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
