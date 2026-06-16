using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using BinancePulse.Configuration;
using BinancePulse.Services;
using BinancePulse.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace BinancePulse
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup (e);
            SetupGlobalExceptionHandling ();

            // Сначала загружаем конфигурацию вручную, чтобы зашифровать ключи при необходимости
            var configuration = new ConfigurationBuilder ()
                .SetBasePath (AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile ("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables ()
                .Build ();

            var binanceSection = configuration.GetSection ("Binance");
            var binanceOptions = binanceSection.Get<BinanceOptions> ();
            bool needEncrypt = binanceOptions != null && !binanceOptions.IsEncrypted &&
                               !string.IsNullOrEmpty (binanceOptions.ApiKey) &&
                               !string.IsNullOrEmpty (binanceOptions.ApiSecret);

            if (needEncrypt)
            {
                binanceOptions.ApiKey = EncryptionService.Encrypt (binanceOptions.ApiKey);
                binanceOptions.ApiSecret = EncryptionService.Encrypt (binanceOptions.ApiSecret);
                binanceOptions.IsEncrypted = true;

                var json = File.ReadAllText (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"));
                var jsonObj = JObject.Parse (json);
                jsonObj["Binance"]["ApiKey"] = binanceOptions.ApiKey;
                jsonObj["Binance"]["ApiSecret"] = binanceOptions.ApiSecret;
                jsonObj["Binance"]["IsEncrypted"] = true;
                File.WriteAllText (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), jsonObj.ToString ());

                Debug.WriteLine ("🔐 API-ключи зашифрованы и сохранены в appsettings.json");
            }

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

                    // Регистрация BinanceClient с расшифровкой ключей
                    services.AddSingleton<BinanceClient> (sp =>
                    {
                        var opts = sp.GetRequiredService<IOptions<BinanceOptions>> ().Value;
                        string apiKey = opts.IsEncrypted ? EncryptionService.Decrypt (opts.ApiKey) : opts.ApiKey;
                        string apiSecret = opts.IsEncrypted ? EncryptionService.Decrypt (opts.ApiSecret) : opts.ApiSecret;
                        return new BinanceClient (apiKey, apiSecret, opts.UseTestnet);
                    });

                    // Регистрация остальных сервисов
                    services.AddSingleton<WalletManager> ();
                    services.AddSingleton<EarnService> ();
                    services.AddSingleton<BalanceRebalancerService> ();
                    services.AddSingleton<PositionManager> ();
                    services.AddSingleton<TradingStrategy> ();

                    // PositionProtector требует BinanceClient, PositionManager и TradingOptions
                    services.AddSingleton<PositionProtector> (sp =>
                    {
                        var client = sp.GetRequiredService<BinanceClient> ();
                        var positionManager = sp.GetRequiredService<PositionManager> ();
                        var options = sp.GetRequiredService<IOptions<TradingOptions>> ().Value;
                        return new PositionProtector (client, positionManager, options);
                    });

                    // TelegramNotifier требует два строковых параметра
                    services.AddSingleton<TelegramNotifier> (sp =>
                    {
                        var telOpts = sp.GetRequiredService<IOptions<TelegramOptions>> ().Value;
                        return new TelegramNotifier (telOpts.BotToken, telOpts.ChatId);
                    });

                    services.AddSingleton<WebSocketPriceService> ();

                    // TradingService требует все зависимости – они уже зарегистрированы
                    services.AddSingleton<TradingService> ();

                    // UpdateManager требует Action<string> – передаём пустой делегат (можно заменить на реальный логгер)
                    services.AddSingleton<UpdateManager> (sp => new UpdateManager (msg => { }));

                    // MainWindowViewModel требует TradingService и UpdateManager
                    services.AddSingleton<MainWindowViewModel> ();
                })
                .Build ();

            await _host.StartAsync ();

            // Получаем ViewModel и показываем окно
            var vm = _host.Services.GetRequiredService<MainWindowViewModel> ();
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
                Directory.CreateDirectory (Path.GetDirectoryName (errorLogPath));
                File.AppendAllText (errorLogPath, logMessage + "\n\n");
            }
            catch { }
            Debug.WriteLine (logMessage);
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