using System;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class RiskCalculator
    {
        private readonly IBinanceClient _client;
        private readonly Action<string> _logger;

        public RiskCalculator(IBinanceClient client, Action<string> logger = null)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<decimal> CalculatePositionSizeAsync(string symbol, decimal riskCapital, decimal price)
        {
            decimal atr = await _client.GetATRAsync (symbol, 14);
            if (atr <= 0) atr = price * 0.02m;
            decimal qty = riskCapital / price;
            decimal step = await _client.GetStepSizeAsync (symbol);
            qty = Math.Floor (qty / step) * step;
            return qty;
        }

        public async Task<decimal> CalculateDynamicRiskAsync(decimal totalBalance, decimal baseRisk, decimal volatility)
        {
            volatility = Math.Clamp (volatility, 0.005m, 0.30m);
            decimal riskMultiplier = Math.Max (0.2m, 1 - ( volatility - 0.02m ) * 10);
            decimal adjustedRisk = Math.Clamp (baseRisk * riskMultiplier, 0.05m, 0.25m);
            return totalBalance * adjustedRisk;
        }
    }
}