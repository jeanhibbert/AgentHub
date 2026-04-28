using AgentHub.Rates.Domain;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AgentHub.Rates.Api;

[McpServerToolType]
public sealed class RatesMcpTools(RatesDbContext dbContext)
{
    [McpServerTool, Description("Returns a concise explanation of the interest-rate swap context for a correlation key.")]
    public async Task<string> GetSwapRepricingContext(string correlationKey, CancellationToken cancellationToken = default)
    {
        var shifts = await dbContext.SwapCurveShifts
            .Where(item => item.CorrelationKey == correlationKey)
            .OrderBy(item => item.CurveDate)
            .ToListAsync(cancellationToken);

        if (shifts.Count == 0)
        {
            return $"No swap curve shifts found for correlation key '{correlationKey}'.";
        }

        var latest = shifts[^1];
        return $"Interest-rate derivatives context: 2Y={latest.TwoYearRateBps}bps, 5Y={latest.FiveYearRateBps}bps, 10Y={latest.TenYearRateBps}bps. The short end moved {latest.TwoYearDailyMoveBps}bps while 5Y/10Y moved {latest.FiveYearDailyMoveBps}/{latest.TenYearDailyMoveBps}bps. Narrative: {latest.Narrative}";
    }
}
