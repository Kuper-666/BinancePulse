using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinancePulse.Models
{
    public class PositionDisplayItem : INotifyPropertyChanged
    {
        private decimal _currentPrice;
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal CurrentPrice
        {
            get => _currentPrice;
            set { _currentPrice = value; OnPropertyChanged (); }
        }
        public decimal StopLossPrice { get; set; }
        public decimal TakeProfitPrice { get; set; }
        public DateTime OpenTime { get; set; }

        public decimal PnL => ( CurrentPrice - EntryPrice ) * Quantity;
        public decimal PnLPercent => EntryPrice != 0 ? ( CurrentPrice - EntryPrice ) / EntryPrice * 100 : 0;
        public TimeSpan Duration => DateTime.UtcNow - OpenTime;
        public string DurationDisplay => Duration.ToString (@"hh\:mm\:ss");
        public string PnLDisplay => PnL >= 0 ? $"+{PnL:F2}" : $"{PnL:F2}";
        public string PnLPercentDisplay => PnLPercent >= 0 ? $"+{PnLPercent:F2}%" : $"{PnLPercent:F2}%";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}