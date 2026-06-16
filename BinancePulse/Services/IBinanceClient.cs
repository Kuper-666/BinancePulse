using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using BinancePulse.Models;

namespace BinancePulse.Services
{
    public interface IBinanceClient : IDisposable
    {
        event Action<string> OnLogGenerated;
        string LastOrderError { get; }
        bool IsTestnet { get; }

        Task SyncTimeAsync();
        Task<JObject> PlaceOrder(string symbol, string side, string type, decimal quantity);
        Task<JObject> PlaceOcoOrder(string symbol, decimal quantity, decimal stopPrice, decimal limitPrice);
        Task<bool> CancelOcoOrder(string symbol, long orderListId);
        Task<JObject> GetAccountInfoAsync();
        Task<decimal> GetAccountBalanceAsync(string asset);
        Task<List<BinanceKline>> GetKlinesAsync(string symbol, string interval, int limit = 500);
        Task<decimal> GetStepSizeAsync(string symbol);
        Task<decimal> GetTickSizeAsync(string symbol);
        Task<decimal> GetATRAsync(string symbol, int period = 14);
        Task<List<string>> GetTopVolumePairsAsync(string quoteAsset = "USDC", int topCount = 20);
        Task<string> GetServerInfo();
        Task<List<JObject>> GetAllOrdersAsync(string symbol, long startTime = 0, long endTime = 0, int limit = 500);
        Task<JArray> GetFlexibleEarnBalanceAsync();
        Task<bool> RedeemFlexibleEarnWithWaitAsync(string asset, decimal amount, int maxWaitSeconds = 60);
        Task<bool> ConvertDustToUsdcAsync(List<string> assetIds = null);
    }
}