using System.Collections.Generic;
using System.Threading.Tasks;
using BinancePulse.Models;

namespace BinancePulse.Services
{
    public interface ITradingStrategy
    {
        string Name { get; }
        Task<(TradeAction Action, string Reason, Dictionary<string, decimal> Indicators)> AnalyzeAsync(
            string symbol, List<BinanceKline> klines);
    }
}