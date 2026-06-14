using System;

namespace BinancePulse.Models
{
    public class TradeLog
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string Action { get; set; } = string.Empty;
    }
}