namespace BinancePulse.Configuration
{
    public enum StrategyType { Sma, RsiBollinger }

    public class TradingOptions
    {
        public decimal MinUsdcBalance { get; set; } = 5.50m;
        public int FastSmaPeriod { get; set; } = 9;
        public int SlowSmaPeriod { get; set; } = 21;
        public int RsiPeriod { get; set; } = 14;
        public int RsiBuyThreshold { get; set; } = 30;
        public int RsiSellThreshold { get; set; } = 70;
        public decimal StopLossPercent { get; set; } = 0.02m;
        public decimal TakeProfitPercent { get; set; } = 0.04m;
        public decimal TrailingStopPercent { get; set; } = 0.02m;
        public int MaxConcurrentPositions { get; set; } = 3;
        public decimal MinTradeAmount { get; set; } = 10m;
        public decimal MaxTradeAmount { get; set; } = 50m;
        public decimal RiskPerTradePercent { get; set; } = 0.02m;
        public decimal ATRMultiplierForStopLoss { get; set; } = 1.5m;
        public decimal ATRMultiplierForPosition { get; set; } = 0.02m;
        public int ATRPeriod { get; set; } = 14;
        public decimal MaxDailyLoss { get; set; } = -20m;
        public bool EnableTelegramDailyReport { get; set; } = true;
        public StrategyType SelectedStrategy { get; set; } = StrategyType.Sma;
    }
}