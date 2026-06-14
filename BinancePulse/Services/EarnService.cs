using System;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class EarnService
    {
        private readonly BinanceClient _client;
        public event Action<string> OnLogGenerated;

        public EarnService(BinanceClient client)
        {
            _client = client;
        }

        public async Task<bool> EnsureLiquidBalanceAsync(string asset, decimal requiredAmount)
        {
            decimal current = await _client.GetAccountBalanceAsync (asset);
            if (current >= requiredAmount) return true;
            decimal need = requiredAmount - current;
            Log ($"Выкупаю {need} {asset} из Earn...");
            bool result = await _client.RedeemFlexibleEarnWithWaitAsync (asset, need);
            if (result) Log ($"✅ Выкуп {asset} подтверждён.");
            return result;
        }

        private void Log(string msg) => OnLogGenerated?.Invoke (msg);
    }
}