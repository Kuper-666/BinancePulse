using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BinancePulse.Services
{
    public class WebSocketPriceService : IDisposable
    {
        private readonly ConcurrentDictionary<string, decimal> _currentPrices = new ();
        private readonly ConcurrentDictionary<string, ClientWebSocket> _sockets = new ();
        private bool _disposed;

        public WebSocketPriceService() { }

        public async Task SubscribeToSymbolsAsync(string[] symbols)
        {
            foreach (var sym in symbols)
            {
                if (_sockets.ContainsKey (sym)) continue;
                _ = Task.Run (() => ConnectAndListen (sym));
                await Task.Delay (100);
            }
        }

        private async Task ConnectAndListen(string symbol)
        {
            string url = $"wss://stream.binance.com:9443/ws/{symbol.ToLowerInvariant ()}@ticker";
            using var ws = new ClientWebSocket ();
            _sockets[symbol] = ws;
            try
            {
                await ws.ConnectAsync (new Uri (url), CancellationToken.None);
                var buffer = new byte[4096];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync (new ArraySegment<byte> (buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString (buffer, 0, result.Count);
                        using var doc = JsonDocument.Parse (json);
                        if (doc.RootElement.TryGetProperty ("c", out var priceEl) && decimal.TryParse (priceEl.GetString (), out var price))
                            _currentPrices[symbol] = price;
                    }
                    else if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { }
            finally { _sockets.TryRemove (symbol, out _); }
        }

        public decimal GetCurrentPrice(string symbol) => _currentPrices.TryGetValue (symbol, out var price) ? price : 0;
        public string[] GetSubscribedSymbols() => _sockets.Keys.ToArray ();
        public void Dispose() { /* закрытие сокетов */ }
    }
}