using AgentHub.Contracts;

namespace AgentHub.Rates.Domain;

public sealed class RatesScenarioService
{
    public IReadOnlyList<SwapCurveShiftEntity> BuildOilDrivenSwapRepricingScenario(CorrelationScenarioRequest request)
    {
        var snapshots = new (decimal TwoYearRate, decimal FiveYearRate, decimal TenYearRate, decimal TwoYearMove, decimal FiveYearMove, decimal TenYearMove)[]
        {
            (392m, 401m, 417m, 8m, 25m, 28m),
            (397m, 409m, 424m, 5m, 8m, 7m),
            (401m, 414m, 429m, 4m, 5m, 5m)
        };

        var startDate = request.StartDate.AddDays(request.InterestRateLagDays);
        var shifts = new List<SwapCurveShiftEntity>(snapshots.Length);

        for (var index = 0; index < snapshots.Length; index++)
        {
            var snapshot = snapshots[index];
            var curveDate = startDate.AddDays(index);

            shifts.Add(new SwapCurveShiftEntity
            {
                ShiftId = $"IRS-{curveDate:yyyyMMdd}-{index + 1}",
                ScenarioId = request.ScenarioId,
                CorrelationKey = request.CorrelationKey,
                CurveDate = curveDate,
                TwoYearRateBps = snapshot.TwoYearRate,
                FiveYearRateBps = snapshot.FiveYearRate,
                TenYearRateBps = snapshot.TenYearRate,
                TwoYearDailyMoveBps = snapshot.TwoYearMove,
                FiveYearDailyMoveBps = snapshot.FiveYearMove,
                TenYearDailyMoveBps = snapshot.TenYearMove,
                Desk = "Macro Swaps",
                Narrative = $"Swap repricing day {index + 1}: 5Y/10Y fixed rates moved higher after the oil shock while the 2Y lagged, flattening the short end."
            });
        }

        return shifts;
    }
}
