namespace BinancePulse.Configuration
{
    public class BinanceOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public bool UseTestnet { get; set; } = false;
    }
}