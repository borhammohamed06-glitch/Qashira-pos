using System.Windows;
using Qashira.Application;
using Qashira.Application.Abstractions;
using Qashira.Infrastructure;
using Qashira.Infrastructure.Database;
using Qashira.Infrastructure.Logging;
using Qashira.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Qashira.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _mainWindowScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Log.Logger = SerilogFactory.CreateLogger();

        var license = OfflineLicenseService.Validate();
        if (!license.IsValid)
        {
            if (!TryActivateOffline())
            {
                Shutdown(3);
                return;
            }

            license = OfflineLicenseService.Validate();
        }

        if (!license.IsValid && !TryActivateOffline())
        {
            MessageBox.Show(license.Message, "تفعيل النظام", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(3);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled WPF exception");
            MessageBox.Show("حدث خطأ غير متوقع. تم تسجيل التفاصيل في ملف السجل.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddApplication();
                services.AddInfrastructure();
                services.AddSingleton<IReceiptPrinter, WpfReceiptPrinter>();
                services.AddTransient<MainWindow>();
                services.AddTransient<ViewModels.MainWindowViewModel>();
            })
            .Build();

        await _host.StartAsync();
        using (var startupScope = _host.Services.CreateScope())
        {
            await startupScope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            await startupScope.ServiceProvider.GetRequiredService<IAutomaticBackupService>().RunStartupBackupAsync();
        }

        _mainWindowScope = _host.Services.CreateScope();
        var window = _mainWindowScope.ServiceProvider.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    private static bool TryActivateOffline()
    {
        var activationWindow = new ActivationWindow();
        return activationWindow.ShowDialog() == true && OfflineLicenseService.Validate().IsValid;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _mainWindowScope?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
