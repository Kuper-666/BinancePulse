using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Models;
using Microsoft.Extensions.Options;
using BinancePulse.Configuration;

namespace BinancePulse.Services
{
    public class TradingService
    {
        private readonly BinanceClient _client;
        private readonly WalletManager _wallet;
        private readonly EarnService _earn;
        private readonly BalanceRebalancerService _rebalancer;
        private readonly PositionManager _positionManager;
        private readonly TradingStrategy _strategy;
        private readonly PositionProtector _protector;
        private readonly TelegramNotifier _telegram;
        private readonly WebSocketPriceService _webSocket;
        private readonly TradingOptions _tradingOptions;
        private bool _isRunning;
        private List<string> _activePairs = new ();
        private readonly object _pairsLock = new ();
        private readonly Dictionary<string, DateTime> _lastBuyTime = new ();

        public event Action<string> OnLogGenerated;

        public TradingService(
            BinanceClient client,
            WalletManager wallet,
            EarnService earn,
            BalanceRebalancerService rebalancer,
            PositionManager positionManager,
            TradingStrategy strategy,
            PositionProtector protector,
            TelegramNotifier telegram,
            WebSocketPriceService webSocket,
            IOptions<TradingOptions> tradingOptions)
        {
            _client = client;
            _wallet = wallet;
            _earn = earn;
            _rebalancer = rebalancer;
            _positionManager = positionManager;
            _strategy = strategy;
            _protector = protector;
            _telegram = telegram;
            _webSocket = webSocket;
            _tradingOptions = tradingOptions.Value;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            Log ("🚀 TradingService запущен");
            _ = Task.Run (RunMainLoopAsync);
        }

        public void Stop()
        {
            _isRunning = false;
            Log ("⏹ TradingService остановлен");
            _webSocket?.Dispose ();
        }

        public async Task<decimal> GetUsdcBalanceAsync()
        {
            return await _client.GetAccountBalanceAsync ("USDC");
        }

        private async Task RunMainLoopAsync()
        {
            // Инициализация: синхронизация времени, загрузка пар, подписка WebSocket
            await _client.SyncTimeAsync ();
            await UpdatePairsAsync ();
            await LoadPositionsAsync ();

            while (_isRunning)
            {
                try
                {
                    // 1. Проверка и закрытие позиций по защитам
                    var toClose = await _protector.CheckAndProtectAsync (GetCurrentPrice);
                    foreach (var sym in toClose)
                        await ExecuteSellAsync (sym);

                    // 2. Получаем баланс USDC
                    decimal spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                    Log ($"💰 Баланс USDC: {spotBalance:F2}");
                    if (spotBalance < _tradingOptions.MinUsdcBalance)
                    {
                        Log ($"⚠️ Баланс USDC ({spotBalance:F2}) ниже минимального {_tradingOptions.MinUsdcBalance}, ждём...");
                        await Task.Delay (60000);
                        continue;
                    }

                    // 3. Обновляем список активных пар (раз в 5 минут)
                    if (DateTime.UtcNow.Minute % 5 == 0 && DateTime.UtcNow.Second < 10)
                        await UpdatePairsAsync ();

                    List<string> pairs;
                    lock (_pairsLock) { pairs = new List<string> (_activePairs); }
                    if (pairs.Count == 0)
                    {
                        await Task.Delay (5000);
                        continue;
                    }

                    // 4. Анализ каждой пары
                    foreach (var symbol in pairs)
                    {
                        if (!_isRunning) break;

                        var klines = await _client.GetKlinesAsync (symbol, "5m", 50);
                        if (klines == null || klines.Count < 30) continue;

                        var analysis = await _strategy.AnalyzeAsync (symbol, klines);
                        bool hasPosition = _positionManager.TryGet (symbol, out _);

                        // Обновление UI (если нужно – через событие, но для простоты пока логируем)
                        if (analysis.Indicators.ContainsKey ("price"))
                        {
                            Log ($"[{symbol}] Цена: {analysis.Indicators["price"]:F4}, Сигнал: {analysis.Action}");
                        }

                        // Покупка
                        if (analysis.Action == TradeAction.Buy && !hasPosition && _positionManager.Count < _tradingOptions.MaxConcurrentPositions)
                        {
                            await ExecuteBuyAsync (symbol, analysis.Indicators, spotBalance);
                            spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                        }
                        // Продажа
                        else if (analysis.Action == TradeAction.Sell && hasPosition)
                        {
                            await ExecuteSellAsync (symbol);
                            spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                        }
                    }

                    await Task.Delay (60000); // Пауза 1 минута между циклами
                }
                catch (Exception ex)
                {
                    Log ($"❌ Ошибка в торговом цикле: {ex.Message}");
                    await Task.Delay (10000);
                }
            }
        }

        private async Task UpdatePairsAsync()
        {
            try
            {
                var newPairs = await _client.GetTopVolumePairsAsync ("USDC", 10);
                newPairs = newPairs.Where (p => !p.Contains ("USD1") && !p.Contains ("UUSDC")).ToList ();

                if (newPairs.Count > 0)
                {
                    lock (_pairsLock) { _activePairs = newPairs; }
                    var newSymbols = newPairs.Except (_webSocket.GetSubscribedSymbols ()).ToArray ();
                    if (newSymbols.Any ())
                        await _webSocket.SubscribeToSymbolsAsync (newSymbols);
                    Log ($"📊 Обновлено {_activePairs.Count} торговых пар");
                }
            }
            catch (Exception ex) { Log ($"❌ Ошибка обновления пар: {ex.Message}"); }
        }

        private async Task LoadPositionsAsync()
        {
            // Здесь нужно восстановить позиции из файла, обновить SL/TP по текущей цене
            // Пока заглушка – в старом коде был метод PositionManager.LoadAsync, можно добавить позже
            Log ("📂 Загрузка сохранённых позиций (пока заглушка)");
        }

        private decimal GetCurrentPrice(string symbol) => _webSocket.GetCurrentPrice (symbol);

        private async Task ExecuteBuyAsync(string symbol, Dictionary<string, decimal> indicators, decimal currentBalance)
        {
            if (!indicators.ContainsKey ("price")) return;
            decimal price = indicators["price"];
            decimal rsi = indicators.ContainsKey ("rsi") ? indicators["rsi"] : 50;
            decimal fastSma = indicators.ContainsKey ("fastSma") ? indicators["fastSma"] : 0;
            decimal slowSma = indicators.ContainsKey ("slowSma") ? indicators["slowSma"] : 0;

            // Дополнительная проверка (можно расширить)
            bool shouldBuy = rsi < _tradingOptions.RsiBuyThreshold && fastSma > slowSma;
            if (!shouldBuy) return;

            // Размер позиции: фиксированная сумма 10 USDC (позже можно сделать динамическим)
            // Динамический расчёт размера позиции
            decimal qty = ( currentBalance * _tradingOptions.RiskPerTradePercent ) / price;
            // Ограничение максимальной суммой сделки
            decimal maxQty = _tradingOptions.MaxTradeAmount / price;
            qty = Math.Min (qty, maxQty);
            // Округление по шагу лота биржи
            decimal stepSize = await _client.GetStepSizeAsync (symbol);
            qty = Math.Floor (qty / stepSize) * stepSize;
            // Финальная проверка
            if (qty <= 0 || qty * price > currentBalance) return;

            // Защита от частых покупок одной пары
            if (_lastBuyTime.TryGetValue (symbol, out var lastTime) && DateTime.UtcNow - lastTime < TimeSpan.FromMinutes (2))
                return;
            _lastBuyTime[symbol] = DateTime.UtcNow;

            Log ($"💵 Покупка {qty} {symbol} по {price:F4} (сумма ~{qty * price:F2} USDC)");
            var order = await _client.PlaceOrder (symbol, "BUY", "MARKET", qty);
            if (order != null)
            {
                var pos = new OpenPosition
                {
                    Symbol = symbol,
                    Quantity = qty,
                    EntryPrice = price,
                    OpenTime = DateTime.UtcNow,
                    StopLossPrice = price * ( 1 - _tradingOptions.StopLossPercent ),
                    TakeProfitPrice = price * ( 1 + _tradingOptions.TakeProfitPercent ),
                    HighestPrice = price,
                    HighestPriceSinceOpen = price,
                    OcoOrderListId = 0
                };
                _positionManager.AddOrUpdate (symbol, pos);
                Log ($"✅ Куплено {qty} {symbol}");
                if (_telegram.IsEnabled)
                    await _telegram.SendTradeNotification (symbol, "BUY", price, qty);
            }
        }

        private async Task ExecuteSellAsync(string symbol)
        {
            if (!_positionManager.TryGet (symbol, out var pos)) return;
            string asset = symbol.Replace ("USDC", "");
            decimal spotBalance = await _client.GetAccountBalanceAsync (asset);
            decimal price = GetCurrentPrice (symbol);
            decimal qtyToSell = Math.Min (pos.Quantity, spotBalance);

            if (qtyToSell <= 0.000001m)
            {
                _positionManager.Remove (symbol);
                Log ($"⚠️ Позиция {symbol} удалена (баланс {asset} = {spotBalance})");
                return;
            }

            // Отмена OCO-ордера, если он был установлен
            if (pos.OcoOrderListId != 0)
                await _client.CancelOcoOrder (symbol, pos.OcoOrderListId);

            var order = await _client.PlaceOrder (symbol, "SELL", "MARKET", qtyToSell);
            if (order != null)
            {
                decimal pnl = ( price - pos.EntryPrice ) * qtyToSell;
                decimal pnlPct = ( price / pos.EntryPrice - 1 ) * 100;
                Log ($"🔒 Закрыта {symbol}: PnL {pnl:F2} ({pnlPct:F2}%)");

                var trade = new TradeLog
                {
                    Symbol = symbol,
                    EntryPrice = pos.EntryPrice,
                    ExitPrice = price,
                    Quantity = qtyToSell,
                    PnL = pnl,
                    PnLPercent = pnlPct,
                    OpenTime = pos.OpenTime,
                    CloseTime = DateTime.UtcNow,
                    Reason = "Signal Sell",
                    Duration = DateTime.UtcNow - pos.OpenTime,
                    Action = "SELL_CLOSE"
                };
                // Можно добавить сохранение в историю (пока просто лог)
                _positionManager.Remove (symbol);
                if (_telegram.IsEnabled)
                    await _telegram.SendTradeNotification (symbol, "SELL", price, qtyToSell, pnl);
            }
            else
            {
                Log ($"❌ Не удалось продать {symbol}: {_client.LastOrderError}");
            }
        }

        private void Log(string msg) => OnLogGenerated?.Invoke (msg);
    }
}