namespace AgentHub.Rates.Domain;

public sealed class SwapCurveShiftEntity
{
    public int Id { get; set; }
    public string ShiftId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string CorrelationKey { get; set; } = string.Empty;
    public DateOnly CurveDate { get; set; }
    public decimal TwoYearRateBps { get; set; }
    public decimal FiveYearRateBps { get; set; }
    public decimal TenYearRateBps { get; set; }
    public decimal TwoYearDailyMoveBps { get; set; }
    public decimal FiveYearDailyMoveBps { get; set; }
    public decimal TenYearDailyMoveBps { get; set; }
    public string Desk { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
