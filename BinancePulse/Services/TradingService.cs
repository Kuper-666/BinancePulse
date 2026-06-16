using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Configuration;
using BinancePulse.Models;
using Microsoft.Extensions.Options;

namespace BinancePulse.Services
{
    public class TradingService
    {
        private readonly BinanceClient _client;
        private readonly WalletManager _wallet;
        private readonly EarnService _earn;
        private readonly BalanceRebalancerService _rebalancer;
        private readonly PositionManager _positionManager;
        private readonly PositionProtector _protector;
        private readonly TelegramNotifier _telegram;
        private readonly WebSocketPriceService _webSocket;
        private readonly TradingOptions _tradingOptions;
        private readonly ITradingStrategy _strategy;
        private bool _isRunning;
        private bool _isPaused;
        private List<string> _activePairs = new ();
        private readonly object _pairsLock = new ();
        private readonly Dictionary<string, DateTime> _lastBuyTime = new ();
        private DailyStatistics _dailyStats = new ();
        private Timer _dailyReportTimer;

        public event Action<string> OnLogGenerated;
        public event Action<TradeLog> OnTradeClosed;
        public event Action<string, decimal, TradeAction> OnMarketUpdate;
        public event Action<DateTime, decimal> OnBalanceUpdate;
        public event Action OnPositionChanged;
        public event Action<bool> OnPauseStatusChanged;

        public TradingService(
            BinanceClient client,
            WalletManager wallet,
            EarnService earn,
            BalanceRebalancerService rebalancer,
            PositionManager positionManager,
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
            _protector = protector;
            _telegram = telegram;
            _webSocket = webSocket;
            _tradingOptions = tradingOptions.Value;

            // Выбор стратегии
            _strategy = _tradingOptions.SelectedStrategy == StrategyType.Sma
                ? new SmaStrategy (_tradingOptions)
                : new RsiBollingerStrategy (_tradingOptions);

            LoggingService.Info ($"Стратегия: {_strategy.Name}");

            _positionManager.PositionAdded += _ => OnPositionChanged?.Invoke ();
            _positionManager.PositionUpdated += _ => OnPositionChanged?.Invoke ();
            _positionManager.PositionRemoved += _ => OnPositionChanged?.Invoke ();

            if (_tradingOptions.EnableTelegramDailyReport && _telegram.IsEnabled)
            {
                var now = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays (1);
                var delay = nextMidnight - now;
                _dailyReportTimer = new Timer (async _ => await SendDailyReport (), null, delay, TimeSpan.FromDays (1));
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _isPaused = false;
            LoggingService.Info ("TradingService запущен");
            StartTelegramBot ();
            _ = Task.Run (RunMainLoopAsync);
        }

        public void Stop()
        {
            _isRunning = false;
            LoggingService.Info ("TradingService остановлен");
            _webSocket?.Dispose ();
        }

        public void PauseTrading()
        {
            _isPaused = true;
            OnPauseStatusChanged?.Invoke (true);
            LoggingService.Warning ("Торговля приостановлена");
        }

        public void ResumeTrading()
        {
            _isPaused = false;
            OnPauseStatusChanged?.Invoke (false);
            LoggingService.Info ("Торговля возобновлена");
        }

        public bool IsPaused => _isPaused;

        public async Task<decimal> GetUsdcBalanceAsync()
        {
            return await _client.GetAccountBalanceAsync ("USDC");
        }

        public List<PositionDisplayItem> GetPositionsWithCurrentPrice()
        {
            var result = new List<PositionDisplayItem> ();
            foreach (var pos in _positionManager.GetAllPositions ())
            {
                decimal currentPrice = GetCurrentPrice (pos.Symbol);
                if (currentPrice <= 0) currentPrice = pos.EntryPrice;
                result.Add (new PositionDisplayItem
                {
                    Symbol = pos.Symbol,
                    Quantity = pos.Quantity,
                    EntryPrice = pos.EntryPrice,
                    CurrentPrice = currentPrice,
                    StopLossPrice = pos.StopLossPrice,
                    TakeProfitPrice = pos.TakeProfitPrice,
                    OpenTime = pos.OpenTime
                });
            }
            return result;
        }

        public async Task SendManualReport()
        {
            await SendDailyReport ();
        }

        private async Task RunMainLoopAsync()
        {
            try
            {
                await _client.SyncTimeAsync ();
                await UpdatePairsAsync ();
                await LoadPositionsAsync ();
            }
            catch (Exception ex)
            {
                LoggingService.Error ("Ошибка инициализации торгового цикла", ex);
                return;
            }

            while (_isRunning)
            {
                try
                {
                    if (_isPaused)
                    {
                        await Task.Delay (5000);
                        continue;
                    }

                    decimal spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                    OnBalanceUpdate?.Invoke (DateTime.Now, spotBalance);
                    LoggingService.Info ($"Баланс USDC: {spotBalance:F2}");

                    var toClose = await _protector.CheckAndProtectAsync (GetCurrentPrice);
                    foreach (var sym in toClose)
                        await ExecuteSellAsync (sym);

                    if (spotBalance < _tradingOptions.MinUsdcBalance)
                    {
                        await Task.Delay (60000);
                        continue;
                    }

                    if (DateTime.UtcNow.Minute % 5 == 0 && DateTime.UtcNow.Second < 10)
                        await UpdatePairsAsync ();

                    List<string> pairs;
                    lock (_pairsLock) { pairs = new List<string> (_activePairs); }
                    if (pairs.Count == 0) { await Task.Delay (5000); continue; }

                    foreach (var symbol in pairs)
                    {
                        if (!_isRunning) break;
                        if (_isPaused) break;

                        try
                        {
                            var klines = await _client.GetKlinesAsync (symbol, "5m", 50);
                            if (klines == null || klines.Count < 30) continue;

                            var analysis = await _strategy.AnalyzeAsync (symbol, klines);
                            bool hasPosition = _positionManager.TryGet (symbol, out _);

                            if (analysis.Indicators.ContainsKey ("price"))
                            {
                                decimal price = analysis.Indicators["price"];
                                OnMarketUpdate?.Invoke (symbol, price, analysis.Action);
                            }

                            if (analysis.Action == TradeAction.Buy && !hasPosition && _positionManager.Count < _tradingOptions.MaxConcurrentPositions)
                            {
                                await ExecuteBuyAsync (symbol, analysis.Indicators, spotBalance);
                                spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                            }
                            else if (analysis.Action == TradeAction.Sell && hasPosition)
                            {
                                await ExecuteSellAsync (symbol);
                                spotBalance = await _client.GetAccountBalanceAsync ("USDC");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.Error ($"Ошибка анализа пары {symbol}", ex);
                        }
                    }

                    await Task.Delay (60000);
                }
                catch (Exception ex)
                {
                    LoggingService.Error ("Ошибка в торговом цикле", ex);
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
                    LoggingService.Info ($"Обновлено {_activePairs.Count} торговых пар");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error ("Ошибка обновления пар", ex);
            }
        }

        private async Task LoadPositionsAsync()
        {
            try
            {
                await _positionManager.LoadAndUpdateAsync (
                    getPrice: async symbol => ( await _client.GetKlinesAsync (symbol, "5m", 1) ).LastOrDefault ()?.Close ?? 0,
                    getStopLossPercent: price => _tradingOptions.StopLossPercent,
                    getTakeProfitPercent: price => _tradingOptions.TakeProfitPercent
                );
                LoggingService.Info ($"Загружено {_positionManager.Count} открытых позиций");
                foreach (var sym in _positionManager.GetSymbols ())
                    LoggingService.Info ($"   - {sym}");
                OnPositionChanged?.Invoke ();
            }
            catch (Exception ex)
            {
                LoggingService.Error ("Ошибка загрузки позиций", ex);
            }
        }

        private decimal GetCurrentPrice(string symbol) => _webSocket.GetCurrentPrice (symbol);

        private async Task ExecuteBuyAsync(string symbol, Dictionary<string, decimal> indicators, decimal currentBalance)
        {
            try
            {
                if (!indicators.ContainsKey ("price")) return;
                decimal price = indicators["price"];
                decimal rsi = indicators.ContainsKey ("rsi") ? indicators["rsi"] : 50;
                decimal fastSma = indicators.ContainsKey ("fastSma") ? indicators["fastSma"] : 0;
                decimal slowSma = indicators.ContainsKey ("slowSma") ? indicators["slowSma"] : 0;

                if (IsDailyLossLimitExceeded ())
                {
                    LoggingService.Warning ($"Дневной лимит убытка ({_tradingOptions.MaxDailyLoss} USDC) превышен. Сделка отменена.");
                    return;
                }

                // Для стратегии RSI+Bollinger могут быть другие условия, но мы доверяем анализу
                if (!( rsi < _tradingOptions.RsiBuyThreshold && fastSma > slowSma )) return;

                decimal qty = await CalculateDynamicQuantity (symbol, currentBalance, price);
                if (qty <= 0 || qty * price > currentBalance) return;

                if (_lastBuyTime.TryGetValue (symbol, out var lastTime) && DateTime.UtcNow - lastTime < TimeSpan.FromMinutes (2))
                    return;
                _lastBuyTime[symbol] = DateTime.UtcNow;

                var atr = await _client.GetATRAsync (symbol, _tradingOptions.ATRPeriod);
                if (atr <= 0) atr = price * 0.01m;
                decimal baseVolatility = price * 0.01m;
                decimal volatilityFactor = baseVolatility / atr;
                volatilityFactor = Math.Clamp (volatilityFactor, 0.5m, 2.0m);
                decimal riskUsdc = currentBalance * _tradingOptions.RiskPerTradePercent;
                LoggingService.Info ($"ATR для {symbol}: {atr:F4} (множитель волатильности: {volatilityFactor:F2})");
                LoggingService.Info ($"Размер позиции: {qty} {symbol} (сумма {qty * price:F2} USDC, риск {riskUsdc:F2} USDC)");

                LoggingService.Info ($"Покупка {qty} {symbol} по {price:F4}");
                var order = await _client.PlaceOrder (symbol, "BUY", "MARKET", qty);
                if (order != null)
                {
                    decimal stopPrice = price - atr * _tradingOptions.ATRMultiplierForStopLoss;
                    stopPrice = Math.Max (stopPrice, price * 0.9m);
                    decimal takePrice = price + atr * 2.5m;

                    var pos = new OpenPosition
                    {
                        Symbol = symbol,
                        Quantity = qty,
                        EntryPrice = price,
                        OpenTime = DateTime.UtcNow,
                        StopLossPrice = stopPrice,
                        TakeProfitPrice = takePrice,
                        HighestPrice = price,
                        HighestPriceSinceOpen = price,
                        OcoOrderListId = 0
                    };
                    _positionManager.AddOrUpdate (symbol, pos);
                    OnPositionChanged?.Invoke ();
                    LoggingService.Trade (symbol, "BUY", price, qty);
                    LoggingService.Info ($"Куплено {qty} {symbol}");

                    if (_telegram.IsEnabled)
                        await _telegram.SendTradeNotification (symbol, "BUY", price, qty);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error ($"Ошибка выполнения покупки {symbol}", ex);
            }
        }

        private async Task ExecuteSellAsync(string symbol)
        {
            try
            {
                if (!_positionManager.TryGet (symbol, out var pos)) return;
                string asset = symbol.Replace ("USDC", "");
                decimal spotBalance = await _client.GetAccountBalanceAsync (asset);
                decimal price = GetCurrentPrice (symbol);
                decimal qtyToSell = Math.Min (pos.Quantity, spotBalance);

                if (qtyToSell <= 0.000001m)
                {
                    _positionManager.Remove (symbol);
                    OnPositionChanged?.Invoke ();
                    LoggingService.Warning ($"Позиция {symbol} удалена (нет монет)");
                    return;
                }

                if (pos.OcoOrderListId != 0)
                    await _client.CancelOcoOrder (symbol, pos.OcoOrderListId);

                var order = await _client.PlaceOrder (symbol, "SELL", "MARKET", qtyToSell);
                if (order != null)
                {
                    decimal pnl = ( price - pos.EntryPrice ) * qtyToSell;
                    decimal pnlPct = ( price / pos.EntryPrice - 1 ) * 100;
                    LoggingService.Info ($"Закрыта {symbol}: PnL {pnl:F2} ({pnlPct:F2}%)");

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

                    _positionManager.Remove (symbol);
                    OnPositionChanged?.Invoke ();
                    OnTradeClosed?.Invoke (trade);
                    await SaveTradeToFile (trade);
                    UpdateDailyPnL (pnl);
                    LoggingService.Trade (symbol, "SELL", price, qtyToSell, pnl);

                    if (_telegram.IsEnabled)
                        await _telegram.SendTradeNotification (symbol, "SELL", price, qtyToSell, pnl);
                }
                else
                {
                    LoggingService.Error ($"Не удалось продать {symbol}: {_client.LastOrderError}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error ($"Ошибка выполнения продажи {symbol}", ex);
            }
        }

        private async Task<decimal> CalculateDynamicQuantity(string symbol, decimal currentBalance, decimal currentPrice)
        {
            try
            {
                var atr = await _client.GetATRAsync (symbol, _tradingOptions.ATRPeriod);
                if (atr <= 0.0001m) atr = currentPrice * 0.01m;

                decimal baseVolatility = currentPrice * 0.01m;
                decimal volatilityFactor = baseVolatility / atr;
                volatilityFactor = Math.Clamp (volatilityFactor, 0.5m, 2.0m);

                decimal riskUsdc = currentBalance * _tradingOptions.RiskPerTradePercent;
                decimal positionUsdc = riskUsdc * volatilityFactor;
                positionUsdc = Math.Clamp (positionUsdc, _tradingOptions.MinTradeAmount, _tradingOptions.MaxTradeAmount);

                decimal qty = positionUsdc / currentPrice;
                decimal stepSize = await _client.GetStepSizeAsync (symbol);
                qty = Math.Floor (qty / stepSize) * stepSize;
                return qty;
            }
            catch (Exception ex)
            {
                LoggingService.Error ($"Ошибка расчёта размера позиции {symbol}", ex);
                return 0;
            }
        }

        private decimal _dailyPnL = 0;
        private DateTime _lastResetDate = DateTime.UtcNow.Date;

        private void UpdateDailyPnL(decimal pnl)
        {
            if (DateTime.UtcNow.Date != _lastResetDate)
            {
                _dailyPnL = 0;
                _lastResetDate = DateTime.UtcNow.Date;
                LoggingService.Info ("Дневной счётчик PnL сброшен");
            }
            _dailyPnL += pnl;
        }

        private bool IsDailyLossLimitExceeded()
        {
            return _dailyPnL <= _tradingOptions.MaxDailyLoss;
        }

        private async Task SaveTradeToFile(TradeLog trade)
        {
            try
            {
                string filePath = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Data", "trades.json");
                List<TradeLog> trades = new ();
                if (File.Exists (filePath))
                {
                    var json = await File.ReadAllTextAsync (filePath);
                    trades = System.Text.Json.JsonSerializer.Deserialize<List<TradeLog>> (json) ?? new ();
                }
                trades.Insert (0, trade);
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync (filePath, System.Text.Json.JsonSerializer.Serialize (trades, options));
            }
            catch (Exception ex)
            {
                LoggingService.Error ($"Ошибка сохранения истории сделок: {ex.Message}");
            }
        }

        private void StartTelegramBot()
        {
            if (_telegram.IsEnabled)
            {
                _telegram.StartListening (OnTelegramCommand);
                LoggingService.Info ("Telegram бот запущен, слушаем команды...");
            }
        }

        private async Task OnTelegramCommand(string command, string chatId)
        {
            try
            {
                switch (command)
                {
                    case "/balance":
                        var bal = await _client.GetAccountBalanceAsync ("USDC");
                        await _telegram.SendMessageAsync ($"💰 Баланс USDC: {bal:F2}");
                        break;
                    case "/positions":
                        var positions = GetPositionsWithCurrentPrice ();
                        if (positions.Count == 0)
                            await _telegram.SendMessageAsync ("📭 Нет открытых позиций.");
                        else
                        {
                            string msg = "📋 <b>Открытые позиции:</b>\n";
                            foreach (var p in positions)
                            {
                                msg += $"{p.Symbol}: {p.Quantity:F6} по {p.EntryPrice:F4}, PnL {p.PnLDisplay}\n";
                            }
                            await _telegram.SendMessageAsync (msg);
                        }
                        break;
                    case "/stop":
                        PauseTrading ();
                        await _telegram.SendMessageAsync ("⏸ Торговля приостановлена.");
                        break;
                    case "/resume":
                        ResumeTrading ();
                        await _telegram.SendMessageAsync ("▶️ Торговля возобновлена.");
                        break;
                    case "/settings":
                        string settings = $"📊 <b>Текущие параметры:</b>\n" +
                                          $"Стратегия: {_strategy.Name}\n" +
                                          $"SMA: {_tradingOptions.FastSmaPeriod}/{_tradingOptions.SlowSmaPeriod}\n" +
                                          $"RSI пороги: {_tradingOptions.RsiBuyThreshold}/{_tradingOptions.RsiSellThreshold}\n" +
                                          $"SL/TP: {_tradingOptions.StopLossPercent:P0}/{_tradingOptions.TakeProfitPercent:P0}\n" +
                                          $"Риск: {_tradingOptions.RiskPerTradePercent:P0}\n" +
                                          $"Макс. позиций: {_tradingOptions.MaxConcurrentPositions}";
                        await _telegram.SendMessageAsync (settings);
                        break;
                    case "/report":
                        await SendManualReport ();
                        break;
                    case "/metrics":
                        await _telegram.SendMessageAsync (MetricsCollector.GetReport ());
                        break;
                    default:
                        await _telegram.SendMessageAsync ("❓ Неизвестная команда. Доступно: /balance, /positions, /stop, /resume, /settings, /report, /metrics");
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error ($"Ошибка обработки команды {command}", ex);
                await _telegram.SendMessageAsync ($"⚠️ Ошибка: {ex.Message}");
            }
        }

        private async Task SendDailyReport()
        {
            var stats = _dailyStats;
            if (stats.TotalTrades == 0 && stats.TotalPnL == 0) return;

            string message = $"📊 <b>Ежедневный отчёт ({stats.Date:yyyy-MM-dd})</b>\n" +
                             $"💰 PnL: {( stats.TotalPnL >= 0 ? "+" : "" )}{stats.TotalPnL:F2} USDC\n" +
                             $"📈 Сделок: {stats.TotalTrades} (✅{stats.WinningTrades} / ❌{stats.LosingTrades})\n" +
                             $"🎯 Win Rate: {stats.WinRate:F1}%\n" +
                             $"📉 Макс. просадка: {stats.MaxDrawdown:F2} USDC";
            await _telegram.SendMessageAsync (message);
            _dailyStats = new DailyStatistics { Date = DateTime.UtcNow.Date };
        }
    }
}