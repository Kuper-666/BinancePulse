using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class PositionProtector
    {
        private readonly BinanceClient _client;
        private readonly PositionManager _positionManager;
        public event Action<string> OnLogGenerated;

        public PositionProtector(BinanceClient client, PositionManager positionManager)
        {
            _client = client;
            _positionManager = positionManager;
        }

        public async Task<List<string>> CheckAndProtectAsync(Func<string, decimal> getCurrentPrice)
        {
            var toClose = new List<string> ();
            foreach (var sym in _positionManager.GetSymbols ())
            {
                if (!_positionManager.TryGet (sym, out var pos)) continue;
                decimal price = getCurrentPrice (sym);
                if (price <= 0) continue;

                if (price <= pos.StopLossPrice || price >= pos.TakeProfitPrice)
                {
                    toClose.Add (sym);
                }
            }
            return toClose;
        }

        public void SetLogger(Action<string> logger) => OnLogGenerated += logger;
    }
}