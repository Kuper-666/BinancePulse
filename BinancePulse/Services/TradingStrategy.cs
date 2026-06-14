using BinancePulse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace BinancePulse.Services
{
    public class TradingStrategy
    {
        public event Action<string> OnLogGenerated;

        public async Task<(TradeAction Action, string Reason, Dictionary<string, decimal> Indicators)> AnalyzeAsync(string symbol, List<BinanceKline> klines)
        {
            var result = (Action: TradeAction.Hold, Reason: "Нет сигнала", Indicators: new Dictionary<string, decimal> ());
            if (klines == null || klines.Count < 20) return result;

            var closes = klines.Select (k => k.Close).ToList ();
            decimal price = closes.Last ();
            decimal fastSma = closes.Skip (closes.Count - 9).Average ();
            decimal slowSma = closes.Skip (closes.Count - 21).Average ();

            result.Indicators["price"] = price;
            result.Indicators["fastSma"] = fastSma;
            result.Indicators["slowSma"] = slowSma;

            if (fastSma > slowSma && fastSma / slowSma > 1.002m)
            {
                result.Action = TradeAction.Buy;
                result.Reason = $"Золотой крест SMA (9/21)";
            }
            else if (fastSma < slowSma && slowSma / fastSma > 1.002m)
            {
                result.Action = TradeAction.Sell;
                result.Reason = $"Смертельный крест SMA (9/21)";
            }
            return result;
        }

        public void SetLogger(Action<string> logger) => OnLogGenerated += logger;
    }
}