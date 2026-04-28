using AgentHub.Contracts;

namespace AgentHub.Commodities.Domain;

public sealed class CommoditiesScenarioService
{
    public IReadOnlyList<CommodityTradeEntity> BuildOilSpikeScenario(CorrelationScenarioRequest request)
    {
        var prices = new[] { 81.25m, 85.90m, 90.80m, 94.75m, 97.45m };
        var volumes = new[] { 120_000m, 138_000m, 156_000m, 178_000m, 205_000m };
        var trades = new List<CommodityTradeEntity>(prices.Length);
        var baseline = prices[0];

        for (var index = 0; index < prices.Length; index++)
        {
            var tradeDate = request.StartDate.AddDays(index);
            var price = prices[index];
            var priceChange = Math.Round(((price - baseline) / baseline) * 100m, 2);

            trades.Add(new CommodityTradeEntity
            {
                TradeId = $"WTI-{tradeDate:yyyyMMdd}-{index + 1}",
                ScenarioId = request.ScenarioId,
                CorrelationKey = request.CorrelationKey,
                TradeDate = tradeDate,
                PriceUsd = price,
                Volume = volumes[index],
                PriceChangePercent = priceChange,
                Trader = "EnergyFlow-Desk-01",
                Desk = "Global Commodities",
                Narrative = $"WTI price shock day {index + 1}: price reached ${price} with {volumes[index]:N0} barrels traded as inflation-sensitive energy risk repriced."
            });
        }

        return trades;
    }
}
