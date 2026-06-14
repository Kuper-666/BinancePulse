using System;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class WalletManager
    {
        private readonly BinanceClient _client;
        public event Action<string> OnLogGenerated;

        public WalletManager(BinanceClient client)
        {
            _client = client;
        }

        public async Task<decimal> GetUsdcBalanceAsync()
        {
            return await _client.GetAccountBalanceAsync ("USDC");
        }

        private void Log(string msg) => OnLogGenerated?.Invoke (msg);
    }
}