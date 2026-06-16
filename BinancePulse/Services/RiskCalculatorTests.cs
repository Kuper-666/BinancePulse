using System.Threading.Tasks;
using BinancePulse.Models;
using BinancePulse.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace BinancePulse.Tests
{
    public class RiskCalculatorTests
    {
        [Fact]
        public async Task CalculatePositionSize_Should_ReturnCorrectQty()
        {
            // Arrange
            var mockClient = new Mock<IBinanceClient> ();
            mockClient.Setup (c => c.GetATRAsync ("BTCUSDC", 14))
                      .ReturnsAsync (1000m); // ATR = 1000 USDT
            mockClient.Setup (c => c.GetStepSizeAsync ("BTCUSDC"))
                      .ReturnsAsync (0.0001m);

            var riskCalc = new RiskCalculator (mockClient.Object, null, null);

            // Act
            decimal qty = await riskCalc.CalculatePositionSizeAsync ("BTCUSDC", riskCapital: 100m, price: 50000m);

            // Assert
            // Расчёт: qty = (100 / 50000) ≈ 0.002, с учётом stepSize = 0.002
            qty.Should ().BeApproximately (0.002m, 0.0001m);
        }

        [Fact]
        public async Task CalculateDynamicRisk_Should_ReturnAdjustedRisk()
        {
            // Arrange
            var riskCalc = new RiskCalculator (null, null, null);

            // Act
            decimal risk = await riskCalc.CalculateDynamicRiskAsync (
                totalBalance: 1000m,
                baseRisk: 0.02m,
                volatility: 0.05m // высокая волатильность
            );

            // Assert: риск должен быть уменьшен
            risk.Should ().BeLessThan (1000m * 0.02m);
            risk.Should ().BeGreaterThan (0);
        }
    }
}