using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Specialized;
using Qashira.Application.DTOs;
using Qashira.App.ViewModels;

namespace Qashira.App;

public partial class MainWindow : Window
{
    private bool _updatingPasswordBoxes;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        viewModel.CartLines.CollectionChanged += CartLines_OnCollectionChanged;
        PreviewKeyDown += MainWindow_OnPreviewKeyDown;
        AddHandler(TextBlock.MouseRightButtonUpEvent, new MouseButtonEventHandler(TextBlock_OnMouseRightButtonUp), true);
        AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(SuppressButtonBringIntoView), true);
        Loaded += (_, _) => FocusFirstLogicalInput();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingPasswordBoxes)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingPasswordBoxes)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.NewPassword = passwordBox.Password;
        }
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingPasswordBoxes)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.ConfirmPassword = passwordBox.Password;
        }
    }

    private void PosSearchBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter &&
            DataContext is MainWindowViewModel viewModel &&
            viewModel.SearchOrAddProductCommand.CanExecute(null))
        {
            viewModel.SearchOrAddProductCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ProductResultsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is System.Windows.Controls.ListBox listBox)
        {
            viewModel.SetSelectedProducts(listBox.SelectedItems.OfType<ProductLookupDto>());
        }
    }

    private void ProductResultsListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not System.Windows.Controls.ListBox listBox ||
            listBox.SelectedItem is not ProductLookupDto product)
        {
            return;
        }

        viewModel.AddProductFromDoubleClick(product);
        FocusCashierSearchSoon();
        e.Handled = true;
    }

    private void ProductResultsListBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            DataContext is not MainWindowViewModel viewModel ||
            !viewModel.AddSelectedProductCommand.CanExecute(null))
        {
            return;
        }

        viewModel.AddSelectedProductCommand.Execute(null);
        FocusCashierSearchSoon();
        e.Handled = true;
    }

    private void SuspendedInvoicesGrid_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter && viewModel.ResumeSuspendedInvoiceCommand.CanExecute(null))
        {
            viewModel.ResumeSuspendedInvoiceCommand.Execute(null);
            FocusCashierSearchSoon();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && viewModel.CancelSuspendedInvoiceCommand.CanExecute(null))
        {
            viewModel.CancelSuspendedInvoiceCommand.Execute(null);
            FocusCashierSearchSoon();
            e.Handled = true;
        }
    }

    private void QuickNavigationResultsListBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            DataContext is MainWindowViewModel viewModel &&
            viewModel.NavigateToSelectedQuickNavigationCommand.CanExecute(null))
        {
            viewModel.NavigateToSelectedQuickNavigationCommand.Execute(null);
            FocusFirstLogicalInputSoon();
            e.Handled = true;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
        {
            return;
        }

        _updatingPasswordBoxes = true;
        try
        {
            if (e.PropertyName == nameof(MainWindowViewModel.Password) &&
                string.IsNullOrEmpty(viewModel.Password) &&
                PasswordBox.Password.Length > 0)
            {
                PasswordBox.Clear();
            }

            if (e.PropertyName == nameof(MainWindowViewModel.NewPassword) &&
                string.IsNullOrEmpty(viewModel.NewPassword) &&
                NewPasswordBox.Password.Length > 0)
            {
                NewPasswordBox.Clear();
            }

            if (e.PropertyName == nameof(MainWindowViewModel.ConfirmPassword) &&
                string.IsNullOrEmpty(viewModel.ConfirmPassword) &&
                ConfirmPasswordBox.Password.Length > 0)
            {
                ConfirmPasswordBox.Clear();
            }
        }
        finally
        {
            _updatingPasswordBoxes = false;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.ActiveWorkspace) or nameof(MainWindowViewModel.PosVisibility))
        {
            FocusFirstLogicalInputSoon();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsQuickNavigationOpen) && viewModel.IsQuickNavigationOpen)
        {
            Dispatcher.BeginInvoke(() =>
            {
                QuickNavigationSearchBox.Focus();
                QuickNavigationSearchBox.SelectAll();
            });
        }
    }

    private void CartLines_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        FocusCashierSearchSoon();
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (HandleGlobalShortcut(viewModel, e))
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (viewModel.IsQuickNavigationOpen)
            {
                viewModel.CloseQuickNavigationCommand.Execute(null);
                FocusFirstLogicalInputSoon();
                e.Handled = true;
                return;
            }

            if (viewModel.IsNotificationsPopupOpen)
            {
                viewModel.ToggleNotificationsCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Delete &&
            viewModel.ActiveWorkspace == "POS" &&
            !IsTextEditingElement(Keyboard.FocusedElement as DependencyObject) &&
            viewModel.RemoveSelectedCartLineCommand.CanExecute(null))
        {
            viewModel.RemoveSelectedCartLineCommand.Execute(null);
            FocusCashierSearchSoon();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down &&
            ReferenceEquals(Keyboard.FocusedElement, PosSearchBox) &&
            ProductResultsListBox.Items.Count > 0)
        {
            ProductResultsListBox.Focus();
            if (ProductResultsListBox.SelectedIndex < 0)
            {
                ProductResultsListBox.SelectedIndex = 0;
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down &&
            ReferenceEquals(Keyboard.FocusedElement, QuickNavigationSearchBox) &&
            QuickNavigationResultsListBox.Items.Count > 0)
        {
            QuickNavigationResultsListBox.Focus();
            if (QuickNavigationResultsListBox.SelectedIndex < 0)
            {
                QuickNavigationResultsListBox.SelectedIndex = 0;
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            HandleEnterKey(viewModel, e);
        }
    }

    private bool HandleGlobalShortcut(MainWindowViewModel viewModel, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            Execute(viewModel.OpenQuickNavigationCommand);
            e.Handled = true;
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)
        {
            Execute(viewModel.ShowBackupCommand);
            e.Handled = true;
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R)
        {
            if (viewModel.ActiveWorkspace == "Backup")
            {
                Execute(viewModel.RestoreBackupCommand);
            }
            else
            {
                Execute(viewModel.ShowBackupCommand);
            }

            e.Handled = true;
            return true;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.F1:
                Execute(viewModel.ShowPosCommand);
                break;
            case Key.F2:
                Execute(viewModel.ShowProductsCommand);
                break;
            case Key.F3:
                Execute(viewModel.ShowReturnsCommand);
                break;
            case Key.F4:
                Execute(viewModel.ShowPrintingServicesCommand);
                break;
            case Key.F5:
                Execute(viewModel.RefreshCurrentWorkspaceCommand);
                break;
            case Key.F8:
                if (viewModel.PosVisibility == Visibility.Visible)
                {
                    Execute(viewModel.HoldInvoiceCommand);
                    FocusCashierSearchSoon();
                }
                break;
            case Key.F9:
                if (viewModel.PosVisibility == Visibility.Visible)
                {
                    Execute(viewModel.CompleteSaleAndPrintCommand);
                    FocusCashierSearchSoon();
                }
                break;
            case Key.F10:
                if (viewModel.PosVisibility == Visibility.Visible)
                {
                    Execute(viewModel.CompleteSaleCommand);
                    FocusCashierSearchSoon();
                }
                break;
            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    private void HandleEnterKey(MainWindowViewModel viewModel, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        var focused = Keyboard.FocusedElement;

        if (viewModel.IsQuickNavigationOpen)
        {
            Execute(viewModel.NavigateToSelectedQuickNavigationCommand);
            FocusFirstLogicalInputSoon();
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(focused, PosSearchBox))
        {
            Execute(viewModel.SearchOrAddProductCommand);
            FocusCashierSearchSoon();
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(focused, ProductResultsListBox))
        {
            Execute(viewModel.AddSelectedProductCommand);
            FocusCashierSearchSoon();
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(focused, PasswordBox))
        {
            Execute(viewModel.LoginCommand);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(focused, ConfirmPasswordBox))
        {
            Execute(viewModel.ChangeRequiredPasswordCommand);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(focused, NewPasswordBox))
        {
            MoveFocusToNextElement();
            e.Handled = true;
            return;
        }

        if (viewModel.OpenShiftVisibility == Visibility.Visible)
        {
            Execute(viewModel.OpenShiftCommand);
            e.Handled = true;
            return;
        }

        if (focused is TextBox textBox && !textBox.AcceptsReturn)
        {
            MoveFocusToNextElement();
            e.Handled = true;
        }
    }

    private void FocusFirstLogicalInput()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.LoginVisibility == Visibility.Visible)
        {
            UsernameTextBox.Focus();
            UsernameTextBox.SelectAll();
            return;
        }

        if (viewModel.ChangePasswordVisibility == Visibility.Visible)
        {
            NewPasswordBox.Focus();
            return;
        }

        if (viewModel.OpenShiftVisibility == Visibility.Visible)
        {
            OpeningCashTextBox.Focus();
            OpeningCashTextBox.SelectAll();
            return;
        }

        if (viewModel.PosVisibility == Visibility.Visible)
        {
            PosSearchBox.Focus();
            PosSearchBox.SelectAll();
        }
    }

    private void FocusFirstLogicalInputSoon()
    {
        Dispatcher.BeginInvoke(FocusFirstLogicalInput);
    }

    private void FocusCashierSearchSoon()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainWindowViewModel viewModel &&
                viewModel.PosVisibility == Visibility.Visible)
            {
                PosSearchBox.Focus();
                PosSearchBox.SelectAll();
            }
        });
    }

    private static void Execute(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private static void MoveFocusToNextElement()
    {
        if (Keyboard.FocusedElement is UIElement element)
        {
            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private static bool IsTextEditingElement(DependencyObject? source) =>
        source is TextBoxBase || source is PasswordBox || FindAncestor<TextBoxBase>(source) is not null || FindAncestor<PasswordBox>(source) is not null;

    private void TextBlock_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBlock textBlock &&
            !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            Clipboard.SetText(textBlock.Text);
            e.Handled = true;
        }
    }

    private void SuppressButtonBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is not null)
        {
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is DependencyObject posSource &&
            IsInside(posSource, PosWorkspaceRoot) &&
            (FindAncestor<TextBoxBase>(posSource) is not null ||
             FindAncestor<ComboBox>(posSource) is not null ||
             FindAncestor<CheckBox>(posSource) is not null))
        {
            e.Handled = true;
        }
    }

    private static bool IsInside(DependencyObject? current, DependencyObject ancestor)
    {
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
