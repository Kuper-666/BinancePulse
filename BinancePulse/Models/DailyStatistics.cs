using System;

namespace BinancePulse.Models
{
    public class DailyStatistics
    {
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;
        public int TotalTrades { get; set; } = 0;
        public int WinningTrades { get; set; } = 0;
        public int LosingTrades { get; set; } = 0;
        public decimal TotalPnL { get; set; } = 0;
        public decimal PeakBalance { get; set; } = 0;
        public decimal MaxDrawdown { get; set; } = 0;

        public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades * 100 : 0;
    }
}