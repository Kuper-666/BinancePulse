using BinancePulse.Configuration;
using BinancePulse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class SmaStrategy : ITradingStrategy
    {
        public string Name => "SMA Cross";
        private readonly int _fastSma;
        private readonly int _slowSma;
        private readonly int _rsiPeriod;
        private readonly int _rsiBuyThreshold;
        private readonly int _rsiSellThreshold;

        public SmaStrategy(TradingOptions options)
        {
            _fastSma = options.FastSmaPeriod;
            _slowSma = options.SlowSmaPeriod;
            _rsiPeriod = options.RsiPeriod;
            _rsiBuyThreshold = options.RsiBuyThreshold;
            _rsiSellThreshold = options.RsiSellThreshold;
        }

        public async Task<(TradeAction Action, string Reason, Dictionary<string, decimal> Indicators)> AnalyzeAsync(
            string symbol, List<BinanceKline> klines)
        {
            var result = (Action: TradeAction.Hold, Reason: "Нет сигнала", Indicators: new Dictionary<string, decimal> ());
            if (klines == null || klines.Count < _slowSma + 5) return result;

            var closes = klines.Select (k => k.Close).ToList ();
            decimal price = closes.Last ();

            decimal fastSma = closes.Skip (closes.Count - _fastSma).Average ();
            decimal slowSma = closes.Skip (closes.Count - _slowSma).Average ();
            var rsi = TechnicalAnalysis.RSI (closes, _rsiPeriod);
            decimal currentRsi = rsi.LastOrDefault () ?? 50;

            result.Indicators["price"] = price;
            result.Indicators["fastSma"] = fastSma;
            result.Indicators["slowSma"] = slowSma;
            result.Indicators["rsi"] = currentRsi;

            bool buySignal = fastSma > slowSma && currentRsi < _rsiBuyThreshold;
            bool sellSignal = fastSma < slowSma && currentRsi > _rsiSellThreshold;

            if (buySignal)
            {
                result.Action = TradeAction.Buy;
                result.Reason = $"SMA {_fastSma}/{_slowSma} пересечение ↑, RSI={currentRsi:F1}";
            }
            else if (sellSignal)
            {
                result.Action = TradeAction.Sell;
                result.Reason = $"SMA {_fastSma}/{_slowSma} пересечение ↓, RSI={currentRsi:F1}";
            }
            return result;
        }
    }
}