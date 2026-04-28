using AgentHub.Commodities.Domain;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AgentHub.Commodities.Api;

[McpServerToolType]
public sealed class CommoditiesMcpTools(CommoditiesDbContext dbContext)
{
    [McpServerTool, Description("Returns a concise explanation of the commodities-side context for a correlation key.")]
    public async Task<string> GetCommodityScenarioContext(string correlationKey, CancellationToken cancellationToken = default)
    {
        var trades = await dbContext.CommodityTrades
            .Where(item => item.CorrelationKey == correlationKey)
            .OrderBy(item => item.TradeDate)
            .ToListAsync(cancellationToken);

        if (trades.Count == 0)
        {
            return $"No commodity trades found for correlation key '{correlationKey}'.";
        }

        var openingPrice = trades[0].PriceUsd;
        var peakTrade = trades.MaxBy(item => item.PriceUsd)!;
        var peakVolume = trades.Max(item => item.Volume);

        return $"WTI crude traded from ${openingPrice} to ${peakTrade.PriceUsd} across {trades.Count} sessions. Peak price change was {peakTrade.PriceChangePercent}% with max volume {peakVolume:N0}. Narrative: {peakTrade.Narrative}";
    }
}
