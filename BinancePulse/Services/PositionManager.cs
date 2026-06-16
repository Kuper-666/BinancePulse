using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BinancePulse.Models;

namespace BinancePulse.Services
{
    public class PositionManager
    {
        private readonly string _filePath;
        private Dictionary<string, OpenPosition> _positions = new ();

        // События для UI
        public event Action<OpenPosition> PositionAdded;
        public event Action<OpenPosition> PositionUpdated;
        public event Action<string> PositionRemoved; // symbol

        public PositionManager()
        {
            string dir = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists (dir)) Directory.CreateDirectory (dir);
            _filePath = Path.Combine (dir, "open_positions.json");
            Load ();
        }

        private void Load()
        {
            if (File.Exists (_filePath))
            {
                try
                {
                    string json = File.ReadAllText (_filePath);
                    _positions = JsonSerializer.Deserialize<Dictionary<string, OpenPosition>> (json) ?? new ();
                }
                catch { }
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize (_positions, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync (_filePath, json);
            }
            catch { }
        }

        public async Task LoadAndUpdateAsync(Func<string, Task<decimal>> getPrice, Func<decimal, decimal> getStopLossPercent, Func<decimal, decimal> getTakeProfitPercent)
        {
            Load ();
            foreach (var kv in _positions)
            {
                var pos = kv.Value;
                decimal currentPrice = await getPrice (pos.Symbol);
                if (currentPrice > 0)
                {
                    pos.StopLossPrice = currentPrice * ( 1 - getStopLossPercent (currentPrice) );
                    pos.TakeProfitPrice = currentPrice * ( 1 + getTakeProfitPercent (currentPrice) );
                    pos.HighestPrice = currentPrice;
                    pos.HighestPriceSinceOpen = currentPrice;
                }
            }
            await SaveAsync ();
        }

        public bool TryGet(string symbol, out OpenPosition pos) => _positions.TryGetValue (symbol, out pos);

        public void AddOrUpdate(string symbol, OpenPosition pos)
        {
            bool exists = _positions.ContainsKey (symbol);
            _positions[symbol] = pos;
            SaveAsync ().ConfigureAwait (false);
            if (exists)
                PositionUpdated?.Invoke (pos);
            else
                PositionAdded?.Invoke (pos);
        }

        public bool Remove(string symbol)
        {
            if (_positions.Remove (symbol))
            {
                SaveAsync ().ConfigureAwait (false);
                PositionRemoved?.Invoke (symbol);
                return true;
            }
            return false;
        }

        public int Count => _positions.Count;
        public List<string> GetSymbols() => new List<string> (_positions.Keys);
        public List<OpenPosition> GetAllPositions() => new List<OpenPosition> (_positions.Values);
    }
}