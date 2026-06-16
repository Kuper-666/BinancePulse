using System;
using Serilog;
using Serilog.Core;

namespace BinancePulse.Services
{
    public static class LoggingService
    {
        private static ILogger _logger;

        public static void Initialize(string logPath = "Logs/binancepulse-.txt")
        {
            _logger = new LoggerConfiguration ()
                .MinimumLevel.Debug ()
                .WriteTo.File (logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .WriteTo.Console ()
                .CreateLogger ();

            Log.Logger = _logger;
        }

        public static void Info(string message) => _logger?.Information (message);
        public static void Error(string message, Exception ex = null) => _logger?.Error (ex, message);
        public static void Warning(string message) => _logger?.Warning (message);
        public static void Debug(string message) => _logger?.Debug (message);
        public static void Trade(string symbol, string action, decimal price, decimal quantity, decimal pnl = 0)
        {
            _logger?.Information ("[TRADE] {Symbol} {Action} Price={Price} Qty={Quantity} PnL={PnL}",
                symbol, action, price, quantity, pnl);
        }
    }
}