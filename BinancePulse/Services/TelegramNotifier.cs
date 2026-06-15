using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Options;
using BinancePulse.Configuration;

namespace BinancePulse.Services
{
    public class TelegramNotifier
    {
        private readonly ITelegramBotClient? _botClient;
        private readonly string _chatId;

        /// <summary>
        /// True если бот успешно инициализирован и готов отправлять сообщения.
        /// </summary>
        public bool IsEnabled { get; private set; }

        // Конструктор для DI через IOptions<TelegramOptions>
        public TelegramNotifier(IOptions<TelegramOptions> options)
            : this (options.Value.BotToken, options.Value.ChatId) { }

        // Конструктор для прямой передачи токена и chatId (используется в App.xaml.cs)
        public TelegramNotifier(string botToken, string chatId)
        {
            _chatId = chatId;

            if (string.IsNullOrWhiteSpace (botToken) || string.IsNullOrWhiteSpace (chatId))
            {
                IsEnabled = false;
                return;
            }

            try
            {
                _botClient = new TelegramBotClient (new TelegramBotClientOptions (botToken));
                IsEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"TelegramBot initialization error: {ex.Message}");
                IsEnabled = false;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsEnabled || _botClient is null) return;

            try
            {
                await _botClient.SendMessage (
                    chatId: _chatId,
                    text: message,
                    parseMode: ParseMode.Html,
                    cancellationToken: CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"Error sending Telegram message: {ex.Message}");
            }
        }

        public async Task SendTradeNotification(string symbol, string action, decimal price, decimal quantity, decimal pnl = 0)
        {
            string emoji = action.ToLower () == "buy" ? "🟢" : "🔴";
            string pnlText = pnl != 0 ? $"\n💰 <b>PnL:</b> {( pnl >= 0 ? "+" : "" )}{pnl:F2} USDC" : "";
            string message =
                $"{emoji} <b>{action.ToUpper ()}</b> {symbol}\n" +
                $"💵 <b>Цена:</b> {price:F4}\n" +
                $"📦 <b>Кол-во:</b> {quantity:F6}" +
                pnlText;

            await SendMessageAsync (message);
        }
    }
}