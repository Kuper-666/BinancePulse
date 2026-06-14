using System;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class BalanceRebalancerService
    {
        private readonly BinanceClient _client;
        public event Action<string> OnLogGenerated;

        public BalanceRebalancerService(BinanceClient client)
        {
            _client = client;
        }

        public async Task AutoConvertAssetsToUsdcAsync(decimal targetUsdc = 15m)
        {
            decimal current = await _client.GetAccountBalanceAsync ("USDC");
            if (current >= targetUsdc) return;
            decimal need = targetUsdc - current;
            if (need >= 0.5m)
            {
                Log ($"Пытаюсь сконвертировать пыль в USDC для покрытия {need:F2} USDC");
                await _client.ConvertDustToUsdcAsync ();
            }
        }

        private void Log(string msg) => OnLogGenerated?.Invoke (msg);
    }
}