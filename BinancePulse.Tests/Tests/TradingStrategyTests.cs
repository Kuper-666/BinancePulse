using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Models;
using BinancePulse.Services;
using BinancePulse.Configuration;
using FluentAssertions;
using Xunit;

namespace BinancePulse.Tests
{
    public class TradingStrategyTests
    {
        private readonly SmaStrategy _strategy;

        public TradingStrategyTests()
        {
            var options = new TradingOptions
            {
                FastSmaPeriod = 9,
                SlowSmaPeriod = 21,
                RsiPeriod = 14,
                RsiBuyThreshold = 30,
                RsiSellThreshold = 70
            };
            _strategy = new SmaStrategy (options);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnBuy_WhenGoldenCrossAndLowRsi()
        {
            var klines = CreateKlines (50, startPrice: 100m, trend: 1.02m);
            klines = klines.OrderBy (k => k.OpenTime).ToList ();

            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            result.Action.Should ().Be (TradeAction.Buy);
            result.Reason.Should ().Contain ("SMA");
            result.Indicators["fastSma"].Should ().BeGreaterThan (result.Indicators["slowSma"]);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnSell_WhenDeathCrossAndHighRsi()
        {
            var klines = CreateKlines (50, startPrice: 200m, trend: 0.98m);
            klines = klines.OrderBy (k => k.OpenTime).ToList ();

            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            result.Action.Should ().Be (TradeAction.Sell);
            result.Reason.Should ().Contain ("SMA");
            result.Indicators["fastSma"].Should ().BeLessThan (result.Indicators["slowSma"]);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnHold_WhenNoClearSignal()
        {
            var klines = CreateKlines (50, startPrice: 150m, trend: 1.0m);
            klines = klines.OrderBy (k => k.OpenTime).ToList ();

            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            result.Action.Should ().Be (TradeAction.Hold);
        }

        private List<BinanceKline> CreateKlines(int count, decimal startPrice, decimal trend)
        {
            var list = new List<BinanceKline> ();
            decimal price = startPrice;
            var random = new Random ();
            for (int i = 0; i < count; i++)
            {
                price *= trend;
                list.Add (new BinanceKline
                {
                    OpenTime = DateTime.Now.AddMinutes (-i * 5),
                    Open = price * (decimal)( 1 + random.NextDouble () * 0.001 ),
                    High = price * (decimal)( 1 + random.NextDouble () * 0.002 ),
                    Low = price * (decimal)( 1 - random.NextDouble () * 0.002 ),
                    Close = price,
                    Volume = 1000m
                });
            }
            return list.OrderBy (k => k.OpenTime).ToList ();
        }
    }
}