using BinancePulse.Configuration;
using BinancePulse.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class PositionProtector
    {
        private readonly BinanceClient _client;
        private readonly PositionManager _positionManager;
        private readonly TradingOptions _tradingOptions;
        public event Action<string> OnLogGenerated;

        public PositionProtector(BinanceClient client, PositionManager positionManager, TradingOptions tradingOptions)
        {
            _client = client;
            _positionManager = positionManager;
            _tradingOptions = tradingOptions;
        }

        public async Task<List<string>> CheckAndProtectAsync(Func<string, decimal> getCurrentPrice)
        {
            var toClose = new List<string> ();
            foreach (var sym in _positionManager.GetSymbols ())
            {
                if (!_positionManager.TryGet (sym, out var pos)) continue;
                decimal price = getCurrentPrice (sym);
                if (price <= 0) continue;

                // Получаем ATR для адаптивного трейлинга
                var atr = await _client.GetATRAsync (sym, _tradingOptions.ATRPeriod);
                if (atr <= 0) atr = price * 0.01m; // fallback

                // Адаптивный трейлинг-стоп (активация при росте на 2×ATR)
                if (price > pos.HighestPriceSinceOpen)
                {
                    pos.HighestPriceSinceOpen = price;
                    if (price - pos.EntryPrice >= 2 * atr)
                    {
                        decimal newStop = price - _tradingOptions.ATRMultiplierForStopLoss * atr;
                        if (newStop > pos.StopLossPrice)
                        {
                            pos.StopLossPrice = newStop;
                            Log ($"📈 Адаптивный трейлинг-стоп {sym}: SL повышен до {newStop:F4}");
                            await UpdateOcoOrder (sym, pos);
                        }
                    }
                }

                // Проверка стандартных условий
                if (price <= pos.StopLossPrice || price >= pos.TakeProfitPrice)
                {
                    toClose.Add (sym);
                }
            }
            return toClose;
        }

        private async Task UpdateOcoOrder(string symbol, OpenPosition pos)
        {
            try
            {
                if (pos.OcoOrderListId != 0)
                    await _client.CancelOcoOrder (symbol, pos.OcoOrderListId);
                var newOco = await _client.PlaceOcoOrder (symbol, pos.Quantity, pos.StopLossPrice, pos.TakeProfitPrice);
                if (newOco != null)
                    pos.OcoOrderListId = (long)newOco["orderListId"];
            }
            catch (Exception ex)
            {
                Log ($"⚠️ Ошибка обновления OCO {symbol}: {ex.Message}");
            }
        }

        private void Log(string msg) => OnLogGenerated?.Invoke (msg);
    }
}