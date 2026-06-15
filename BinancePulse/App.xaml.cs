using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BinancePulse.Configuration;
using BinancePulse.Services;
using BinancePulse.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BinancePulse
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup (e);
            SetupGlobalExceptionHandling ();

            _host = Host.CreateDefaultBuilder ()
                .ConfigureAppConfiguration ((context, config) =>
                {
                    config.SetBasePath (AppDomain.CurrentDomain.BaseDirectory);
                    config.AddJsonFile ("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables ();
                })
                .ConfigureServices ((context, services) =>
                {
                    services.Configure<BinanceOptions> (context.Configuration.GetSection ("Binance"));
                    services.Configure<TelegramOptions> (context.Configuration.GetSection ("Telegram"));
                    services.Configure<TradingOptions> (context.Configuration.GetSection ("Trading"));

                    services.AddSingleton<BinanceClient> (sp =>
                    {
                        var opts = sp.GetRequiredService<IOptions<BinanceOptions>> ().Value;
                        return new BinanceClient (opts.ApiKey, opts.ApiSecret, opts.UseTestnet);
                    });

                    services.AddSingleton<WalletManager> ();
                    services.AddSingleton<EarnService> ();
                    services.AddSingleton<BalanceRebalancerService> ();
                    services.AddSingleton<PositionManager> ();
                    services.AddSingleton<TradingStrategy> ();

                    services.AddSingleton<PositionProtector> (sp =>
                    {
                        var client = sp.GetRequiredService<BinanceClient> ();
                        var positionManager = sp.GetRequiredService<PositionManager> ();
                        var options = sp.GetRequiredService<IOptions<TradingOptions>> ().Value;
                        return new PositionProtector (client, positionManager, options);
                    });

                    services.AddSingleton<TelegramNotifier> (sp =>
                    {
                        var telOpts = sp.GetRequiredService<IOptions<TelegramOptions>> ().Value;
                        return new TelegramNotifier (telOpts.BotToken, telOpts.ChatId);
                    });

                    services.AddSingleton<WebSocketPriceService> ();
                    services.AddSingleton<TradingService> ();

                    // ✅ Исправленная регистрация UpdateManager
                    services.AddSingleton<UpdateManager> (sp => new UpdateManager (msg => { }));

                    services.AddSingleton<MainWindowViewModel> ();
                })
                .Build ();

            await _host.StartAsync ();

            var vm = _host.Services.GetRequiredService<MainWindowViewModel> ();

            // Теперь подключаем реальный лог UI к UpdateManager
            // UpdateManager логирует через переданный Action — он уже задан выше.
            // Если нужно перенаправить в UI, можно сделать так:
            // vm.AddLog будет установлен в MainWindow.xaml.cs

            var mainWindow = new MainWindow
            {
                DataContext = vm
            };
            mainWindow.Show ();
        }

        private void SetupGlobalExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                LogCriticalException ("AppDomain UnhandledException", ex);
                if (args.IsTerminating)
                    MessageBox.Show ($"Критическая ошибка: {ex.Message}\n\nПриложение будет закрыто.",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogCriticalException ("TaskScheduler UnobservedTaskException", args.Exception);
                args.SetObserved ();
            };

            Dispatcher.UnhandledException += (sender, args) =>
            {
                LogCriticalException ("Dispatcher UnhandledException", args.Exception);
                MessageBox.Show ($"Ошибка UI: {args.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true;
            };
        }

        private void LogCriticalException(string source, Exception ex)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex.Message}\n{ex.StackTrace}";
            string errorLogPath = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Logs", "critical_errors.log");
            try
            {
                Directory.CreateDirectory (Path.GetDirectoryName (errorLogPath)!);
                File.AppendAllText (errorLogPath, logMessage + "\n\n");
            }
            catch { }
            System.Diagnostics.Debug.WriteLine (logMessage);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync ();
                _host.Dispose ();
            }
            base.OnExit (e);
        }
    }
}