using BinancePulse.Configuration;
using BinancePulse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class RsiBollingerStrategy : ITradingStrategy
    {
        public string Name => "RSI + Bollinger Bands";
        private readonly int _rsiPeriod = 14;
        private readonly int _bbPeriod = 20;
        private readonly decimal _bbMultiplier = 2m;
        private readonly int _rsiBuyThreshold = 30;
        private readonly int _rsiSellThreshold = 70;

        public RsiBollingerStrategy(TradingOptions options)
        {
            _rsiPeriod = options.RsiPeriod;
            _rsiBuyThreshold = options.RsiBuyThreshold;
            _rsiSellThreshold = options.RsiSellThreshold;
        }

        public async Task<(TradeAction Action, string Reason, Dictionary<string, decimal> Indicators)> AnalyzeAsync(
            string symbol, List<BinanceKline> klines)
        {
            var result = (Action: TradeAction.Hold, Reason: "Нет сигнала", Indicators: new Dictionary<string, decimal> ());
            if (klines == null || klines.Count < _bbPeriod + 5) return result;

            var closes = klines.Select (k => k.Close).ToList ();
            decimal price = closes.Last ();

            var rsi = TechnicalAnalysis.RSI (closes, _rsiPeriod);
            var bb = TechnicalAnalysis.BollingerBands (closes, _bbPeriod, _bbMultiplier);

            decimal currentRsi = rsi.LastOrDefault () ?? 50;
            decimal? upper = bb.Upper.LastOrDefault ();
            decimal? lower = bb.Lower.LastOrDefault ();
            decimal? middle = bb.Middle.LastOrDefault ();

            if (!upper.HasValue || !lower.HasValue || !middle.HasValue) return result;

            result.Indicators["price"] = price;
            result.Indicators["rsi"] = currentRsi;
            result.Indicators["bbUpper"] = upper.Value;
            result.Indicators["bbLower"] = lower.Value;
            result.Indicators["bbMiddle"] = middle.Value;

            bool buySignal = currentRsi < _rsiBuyThreshold && price <= lower.Value;
            bool sellSignal = currentRsi > _rsiSellThreshold && price >= upper.Value;

            if (buySignal)
            {
                result.Action = TradeAction.Buy;
                result.Reason = $"RSI={currentRsi:F1} ниже {_rsiBuyThreshold}, цена у нижней полосы BB";
            }
            else if (sellSignal)
            {
                result.Action = TradeAction.Sell;
                result.Reason = $"RSI={currentRsi:F1} выше {_rsiSellThreshold}, цена у верхней полосы BB";
            }
            return result;
        }
    }
}