using System.Windows;
using System.Windows.Input;
using Qashira.App.Services;

namespace Qashira.App;

public partial class ActivationWindow : Window
{
    public ActivationWindow()
    {
        InitializeComponent();
        MachineCodeTextBox.Text = OfflineLicenseService.PrepareMachineCode();
        PreviewKeyDown += ActivationWindow_OnPreviewKeyDown;
        ActivationCodeTextBox.Focus();
    }

    private void CopyMachineCode_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MachineCodeTextBox.Text);
        MessageTextBlock.Foreground = System.Windows.Media.Brushes.DarkGreen;
        MessageTextBlock.Text = "تم نسخ كود الجهاز";
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        MessageTextBlock.Foreground = System.Windows.Media.Brushes.Firebrick;
        var result = OfflineLicenseService.Activate(ActivationCodeTextBox.Text);
        MessageTextBlock.Text = result.Message;

        if (!result.IsValid)
        {
            return;
        }

        MessageBox.Show(result.Message, "تفعيل كاشيرا", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ActivationWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            Activate_Click(sender, e);
            e.Handled = true;
        }
    }
}
