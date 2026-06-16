using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Models;
using BinancePulse.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace BinancePulse.Tests
{
    public class BacktestEngineTests
    {
        private readonly Mock<IBinanceClient> _mockClient;
        private readonly BacktestEngine _engine;

        public BacktestEngineTests()
        {
            _mockClient = new Mock<IBinanceClient> ();
            _engine = new BacktestEngine (_mockClient.Object);
        }

        [Fact]
        public async Task RunAsync_Should_ReturnResult_WithTrades()
        {
            var klines = CreateKlinesForBacktest ();
            _mockClient.Setup (c => c.GetKlinesAsync ("BTCUSDC", "5m", 1000))
                       .ReturnsAsync (klines);

            var result = await _engine.RunAsync (
                symbol: "BTCUSDC",
                startDate: DateTime.Now.AddDays (-5),
                endDate: DateTime.Now,
                fastSma: 9,
                slowSma: 21,
                rsiPeriod: 14,
                rsiBuyThreshold: 30,
                rsiSellThreshold: 70,
                stopLossPercent: 0.02m,
                takeProfitPercent: 0.04m,
                initialCapital: 1000m
            );

            result.TotalTrades.Should ().BeGreaterThan (0);
            result.EquityCurve.Should ().NotBeEmpty ();
            result.Trades.Should ().NotBeEmpty ();
        }

        [Fact]
        public async Task RunAsync_Should_ReturnZeroTrades_WhenNoData()
        {
            _mockClient.Setup (c => c.GetKlinesAsync (It.IsAny<string> (), It.IsAny<string> (), It.IsAny<int> ()))
                       .ReturnsAsync (new List<BinanceKline> ());

            var result = await _engine.RunAsync ("BTCUSDC", DateTime.Now, DateTime.Now, 9, 21, 14, 30, 70, 0.02m, 0.04m);

            result.TotalTrades.Should ().Be (0);
            result.EquityCurve.Should ().ContainSingle ();
        }

        private List<BinanceKline> CreateKlinesForBacktest()
        {
            var list = new List<BinanceKline> ();
            var random = new Random ();
            decimal price = 50000m;
            for (int i = 0; i < 1000; i++)
            {
                if (i % 2 == 0) price *= 1.01m;
                else price *= 0.99m;
                list.Add (new BinanceKline
                {
                    OpenTime = DateTime.Now.AddMinutes (-i * 5),
                    Open = price * (decimal)( 0.999 + random.NextDouble () * 0.002 ),
                    High = price * (decimal)( 1.001 + random.NextDouble () * 0.002 ),
                    Low = price * (decimal)( 0.998 - random.NextDouble () * 0.002 ),
                    Close = price,
                    Volume = 1000m + (decimal)random.NextDouble () * 500
                });
            }
            return list.OrderBy (k => k.OpenTime).ToList ();
        }
    }
}