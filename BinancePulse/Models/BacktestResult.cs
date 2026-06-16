using System;
using System.Collections.Generic;

namespace BinancePulse.Models
{
    public class BacktestResult
    {
        public decimal TotalReturnPercent { get; set; }      // Общая доходность %
        public decimal WinRate { get; set; }                 // Win Rate %
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal MaxDrawdown { get; set; }             // Максимальная просадка %
        public decimal SharpeRatio { get; set; }             // Коэффициент Шарпа (годовой)
        public decimal ProfitFactor { get; set; }            // Отношение прибыли к убыткам
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public List<decimal> EquityCurve { get; set; }       // Кривая капитала
        public List<BacktestTrade> Trades { get; set; }      // Список сделок
    }

    public class BacktestTrade
    {
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
        public string Reason { get; set; }
    }
}