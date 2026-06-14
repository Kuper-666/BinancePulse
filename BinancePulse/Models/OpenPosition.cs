namespace BinancePulse.Models
{
    public class OpenPosition
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime OpenTime { get; set; }
        public decimal StopLossPrice { get; set; }
        public decimal TakeProfitPrice { get; set; }
        public decimal HighestPrice { get; set; }
        public long OcoOrderListId { get; set; }
        public decimal HighestPriceSinceOpen { get; set; }
        public bool IsBreakevenSet { get; set; } = false;
    }
}