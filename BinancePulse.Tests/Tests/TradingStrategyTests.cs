using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinancePulse.Models;
using BinancePulse.Services;
using FluentAssertions;
using Xunit;

namespace BinancePulse.Tests
{
    public class TradingStrategyTests
    {
        private readonly TradingStrategy _strategy;

        public TradingStrategyTests()
        {
            _strategy = new TradingStrategy (null);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnBuy_WhenGoldenCrossAndLowRsi()
        {
            // Arrange: создаём 50 свечей с восходящим трендом и RSI < 30
            var klines = CreateKlines (50, startPrice: 100m, trend: 1.02m); // рост на 2% за свечу
            klines.Reverse (); // последняя свеча – самая высокая

            // Act
            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            // Assert
            result.Action.Should ().Be (TradeAction.Buy);
            result.Reason.Should ().Contain ("Золотой крест");
            result.Indicators["fastSma"].Should ().BeGreaterThan (result.Indicators["slowSma"]);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnSell_WhenDeathCrossAndHighRsi()
        {
            // Arrange: нисходящий тренд с RSI > 70
            var klines = CreateKlines (50, startPrice: 200m, trend: 0.98m); // падение на 2% за свечу
            klines.Reverse ();

            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            result.Action.Should ().Be (TradeAction.Sell);
            result.Reason.Should ().Contain ("Смертельный крест");
            result.Indicators["fastSma"].Should ().BeLessThan (result.Indicators["slowSma"]);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnHold_WhenNoClearSignal()
        {
            var klines = CreateKlines (50, startPrice: 150m, trend: 1.0m); // боковик
            klines.Reverse ();

            var result = await _strategy.AnalyzeAsync ("BTCUSDC", klines);

            result.Action.Should ().Be (TradeAction.Hold);
        }

        // Вспомогательный метод для создания свечей
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