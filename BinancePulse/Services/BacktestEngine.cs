using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Models;

namespace BinancePulse.Services
{
    public class BacktestEngine
    {
        private readonly BinanceClient _client;

        public BacktestEngine(BinanceClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Запуск бэктеста на исторических данных для одной пары
        /// </summary>
        public async Task<BacktestResult> RunAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            int fastSma,
            int slowSma,
            int rsiPeriod,
            int rsiBuyThreshold,
            int rsiSellThreshold,
            decimal stopLossPercent,
            decimal takeProfitPercent,
            decimal initialCapital = 1000m,
            decimal commissionPercent = 0.001m) // 0.1% комиссия
        {
            // Загружаем исторические свечи (интервал 5 минут)
            // Для бэктеста загружаем максимум свечей (Binance отдаёт до 1000)
            var klines = await _client.GetKlinesAsync (symbol, "5m", 1000);
            if (klines == null || klines.Count < slowSma + 10)
            {
                return new BacktestResult { TotalTrades = 0, EquityCurve = new List<decimal> { initialCapital } };
            }

            // Фильтруем по дате
            klines = klines.Where (k => k.OpenTime >= startDate && k.OpenTime <= endDate).ToList ();
            if (klines.Count < 100) return new BacktestResult { TotalTrades = 0, EquityCurve = new List<decimal> { initialCapital } };

            var closes = klines.Select (k => k.Close).ToList ();
            var highs = klines.Select (k => k.High).ToList ();
            var lows = klines.Select (k => k.Low).ToList ();
            var volumes = klines.Select (k => k.Volume).ToList ();

            decimal capital = initialCapital;
            decimal position = 0m;
            decimal entryPrice = 0m;
            decimal peakCapital = initialCapital;
            decimal maxDrawdown = 0m;

            List<BacktestTrade> trades = new ();
            List<decimal> equityCurve = new () { initialCapital };

            // Предрасчёт индикаторов
            var fastSmaValues = CalculateSma (closes, fastSma);
            var slowSmaValues = CalculateSma (closes, slowSma);
            var rsiValues = CalculateRsi (closes, rsiPeriod);

            for (int i = slowSma + 5; i < closes.Count; i++)
            {
                decimal price = closes[i];
                decimal fastSmaVal = fastSmaValues[i];
                decimal slowSmaVal = slowSmaValues[i];
                decimal rsi = rsiValues[i];

                // Генерация сигнала
                bool buySignal = false;
                bool sellSignal = false;

                if (i > 1)
                {
                    bool fastAboveSlow = fastSmaVal > slowSmaVal;
                    bool prevFastAboveSlow = fastSmaValues[i - 1] > slowSmaValues[i - 1];

                    // Золотой крест (быстрая SMA пересекает медленную снизу вверх) + RSI < порог
                    if (!prevFastAboveSlow && fastAboveSlow && rsi < rsiBuyThreshold)
                    {
                        buySignal = true;
                    }

                    // Смертельный крест (быстрая SMA пересекает медленную сверху вниз) + RSI > порог
                    if (prevFastAboveSlow && !fastAboveSlow && rsi > rsiSellThreshold)
                    {
                        sellSignal = true;
                    }
                }

                // Логика открытия/закрытия
                if (position == 0 && buySignal)
                {
                    // Покупка
                    decimal qty = capital / price;
                    qty = Math.Round (qty, 6);
                    if (qty > 0)
                    {
                        // Вычитаем комиссию
                        decimal commission = qty * price * commissionPercent;
                        capital -= commission;
                        position = qty;
                        entryPrice = price;
                    }
                }
                else if (position > 0)
                {
                    // Проверка стоп-лосса и тейк-профита
                    decimal profitPercent = ( price - entryPrice ) / entryPrice;
                    decimal stopPrice = entryPrice * ( 1 - stopLossPercent );
                    decimal takePrice = entryPrice * ( 1 + takeProfitPercent );

                    bool stopHit = price <= stopPrice;
                    bool takeHit = price >= takePrice;

                    if (sellSignal || stopHit || takeHit)
                    {
                        // Закрытие позиции
                        decimal tradePnL = ( price - entryPrice ) * position;
                        decimal tradePnLPercent = ( price - entryPrice ) / entryPrice * 100;

                        // Вычитаем комиссию при продаже
                        decimal commission = price * position * commissionPercent;
                        capital += position * price - commission;
                        position = 0;

                        trades.Add (new BacktestTrade
                        {
                            EntryTime = klines[i - 1].OpenTime,
                            ExitTime = klines[i].OpenTime,
                            EntryPrice = entryPrice,
                            ExitPrice = price,
                            Quantity = position,
                            PnL = tradePnL,
                            PnLPercent = tradePnLPercent,
                            Reason = stopHit ? "Stop Loss" : takeHit ? "Take Profit" : "Signal Sell"
                        });

                        // Обновление максимальной просадки
                        if (capital > peakCapital) peakCapital = capital;
                        decimal drawdown = ( peakCapital - capital ) / peakCapital * 100;
                        if (drawdown > maxDrawdown) maxDrawdown = drawdown;
                    }
                }

                equityCurve.Add (position > 0 ? position * price : capital);
            }

            // Закрытие последней позиции, если осталась
            if (position > 0)
            {
                decimal lastPrice = closes.Last ();
                decimal tradePnL = ( lastPrice - entryPrice ) * position;
                decimal tradePnLPercent = ( lastPrice - entryPrice ) / entryPrice * 100;
                decimal commission = lastPrice * position * commissionPercent;
                capital += position * lastPrice - commission;

                trades.Add (new BacktestTrade
                {
                    EntryTime = klines[^2].OpenTime,
                    ExitTime = klines.Last ().OpenTime,
                    EntryPrice = entryPrice,
                    ExitPrice = lastPrice,
                    Quantity = position,
                    PnL = tradePnL,
                    PnLPercent = tradePnLPercent,
                    Reason = "Close at end"
                });
                equityCurve.Add (capital);
                position = 0;
            }

            // Расчёт метрик
            decimal totalReturn = ( capital - initialCapital ) / initialCapital * 100;
            int totalTrades = trades.Count;
            int winningTrades = trades.Count (t => t.PnL > 0);
            int losingTrades = totalTrades - winningTrades;
            decimal winRate = totalTrades > 0 ? (decimal)winningTrades / totalTrades * 100 : 0;
            decimal profitFactor = 0;
            if (losingTrades > 0 && winningTrades > 0)
            {
                decimal grossProfit = trades.Where (t => t.PnL > 0).Sum (t => t.PnL);
                decimal grossLoss = Math.Abs (trades.Where (t => t.PnL < 0).Sum (t => t.PnL));
                profitFactor = grossLoss > 0 ? grossProfit / grossLoss : 0;
            }

            // Sharpe Ratio (упрощённо)
            decimal sharpeRatio = 0;
            if (equityCurve.Count > 1)
            {
                var returns = new List<decimal> ();
                for (int i = 1; i < equityCurve.Count; i++)
                {
                    returns.Add (( equityCurve[i] - equityCurve[i - 1] ) / equityCurve[i - 1]);
                }
                decimal avgReturn = returns.Average ();
                decimal stdDev = CalculateStdDev (returns);
                sharpeRatio = stdDev > 0 ? avgReturn / stdDev * (decimal)Math.Sqrt (252) : 0;
            }

            return new BacktestResult
            {
                TotalReturnPercent = totalReturn,
                WinRate = winRate,
                TotalTrades = totalTrades,
                WinningTrades = winningTrades,
                LosingTrades = losingTrades,
                MaxDrawdown = maxDrawdown,
                SharpeRatio = sharpeRatio,
                ProfitFactor = profitFactor,
                AverageWin = winningTrades > 0 ? trades.Where (t => t.PnL > 0).Average (t => t.PnL) : 0,
                AverageLoss = losingTrades > 0 ? trades.Where (t => t.PnL < 0).Average (t => t.PnL) : 0,
                EquityCurve = equityCurve,
                Trades = trades
            };
        }

        // Вспомогательные методы индикаторов (можно вынести в отдельный статический класс)
        private List<decimal> CalculateSma(List<decimal> data, int period)
        {
            var result = new List<decimal> ();
            for (int i = 0; i < data.Count; i++)
            {
                if (i < period - 1)
                    result.Add (0);
                else
                    result.Add (data.Skip (i - period + 1).Take (period).Average ());
            }
            return result;
        }

        private List<decimal> CalculateRsi(List<decimal> closes, int period)
        {
            // Простая реализация RSI (можно взять из TechnicalAnalysis)
            var rsiValues = new List<decimal> ();
            if (closes.Count <= period) return rsiValues;

            decimal avgGain = 0, avgLoss = 0;
            for (int i = 1; i <= period; i++)
            {
                decimal diff = closes[i] - closes[i - 1];
                if (diff > 0) avgGain += diff;
                else avgLoss += Math.Abs (diff);
            }
            avgGain /= period;
            avgLoss /= period;
            rsiValues.Add (avgLoss == 0 ? 100 : 100 - 100 / ( 1 + avgGain / avgLoss ));

            for (int i = period + 1; i < closes.Count; i++)
            {
                decimal diff = closes[i] - closes[i - 1];
                decimal gain = diff > 0 ? diff : 0;
                decimal loss = diff < 0 ? Math.Abs (diff) : 0;
                avgGain = ( avgGain * ( period - 1 ) + gain ) / period;
                avgLoss = ( avgLoss * ( period - 1 ) + loss ) / period;
                rsiValues.Add (avgLoss == 0 ? 100 : 100 - 100 / ( 1 + avgGain / avgLoss ));
            }
            return rsiValues;
        }

        private decimal CalculateStdDev(List<decimal> values)
        {
            if (values.Count == 0) return 0;
            decimal avg = values.Average ();
            decimal sumSq = values.Select (v => ( v - avg ) * ( v - avg )).Sum ();
            return (decimal)Math.Sqrt ((double)( sumSq / values.Count ));
        }
    }
}