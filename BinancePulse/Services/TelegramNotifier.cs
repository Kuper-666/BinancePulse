using System;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class TelegramNotifier
    {
        private readonly bool _enabled = false;

        public bool IsEnabled => _enabled;

        public TelegramNotifier(string botToken, string chatId)
        {
            // Заглушка, ничего не делаем
        }

        public async Task SendMessageAsync(string text)
        {
            await Task.CompletedTask;
        }

        public async Task SendTradeNotification(string symbol, string action, decimal price, decimal quantity, decimal pnl = 0)
        {
            await Task.CompletedTask;
        }
    }
}