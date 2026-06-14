using System;

namespace BinancePulse.Models
{
    public class Position
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal EntryPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public DateTime OpenTime { get; set; }
        public bool IsLong { get; set; }
    }
}