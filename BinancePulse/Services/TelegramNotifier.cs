using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BinancePulse.Services
{
    public class TelegramNotifier
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _chatId;
        private readonly bool _enabled;
        private Func<string, string, Task> _commandHandler;
        private CancellationTokenSource _cts;

        public bool IsEnabled => _enabled;

        public TelegramNotifier(string botToken, string chatId)
        {
            _chatId = chatId;
            _enabled = false;
            if (string.IsNullOrEmpty (botToken) || string.IsNullOrEmpty (chatId)) return;
            try
            {
                _botClient = new TelegramBotClient (botToken);
                var me = _botClient.GetMe (CancellationToken.None).GetAwaiter ().GetResult ();
                System.Diagnostics.Debug.WriteLine ($"Telegram бот @{me.Username} готов");
                _enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine ($"Telegram init error: {ex.Message}");
                _enabled = false;
            }
        }

        public async Task SendMessageAsync(string text)
        {
            if (!_enabled) return;
            try
            {
                await _botClient.SendMessage (
                    chatId: _chatId,
                    text: text,
                    parseMode: ParseMode.Html,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine ($"Telegram send error: {ex.Message}");
            }
        }

        public async Task SendTradeNotification(string symbol, string action, decimal price, decimal quantity, decimal pnl = 0)
        {
            if (!_enabled) return;
            string emoji = action == "BUY" ? "🟢" : "🔴";
            string pnlText = pnl != 0 ? $"\n💰 PnL: {( pnl >= 0 ? "+" : "" )}{pnl:F2} USDC" : "";
            string msg = $"{emoji} <b>{action}</b> {symbol}\n" +
                         $"💵 Цена: {price:F4}\n" +
                         $"📦 Кол-во: {quantity:F6}" +
                         pnlText;
            await SendMessageAsync (msg);
        }

        public void StartListening(Func<string, string, Task> onCommandReceived)
        {
            if (!_enabled || _commandHandler != null) return;
            _commandHandler = onCommandReceived;
            _cts = new CancellationTokenSource ();
            _ = Task.Run (() => ListenLoop (_cts.Token));
        }

        private async Task ListenLoop(CancellationToken token)
        {
            int offset = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var updates = await _botClient.GetUpdates (offset: offset, timeout: 30, cancellationToken: token);
                    foreach (var update in updates)
                    {
                        offset = update.Id + 1;
                        if (update.Message?.Text != null)
                        {
                            string text = update.Message.Text.Trim ();
                            string chatId = update.Message.Chat.Id.ToString ();
                            if (text.StartsWith ("/"))
                                await _commandHandler?.Invoke (text, chatId);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine ($"Telegram listen error: {ex.Message}");
                    await Task.Delay (5000, token);
                }
            }
        }
    }
}