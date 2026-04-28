namespace AgentHub.Contracts;

public enum TradingSystem
{
    Commodities,
    InterestRateDerivatives,
    Correlation
}

public enum EventCategory
{
    CommodityTrade,
    CurveShift,
    CorrelationInsight
}

public sealed record MarketEventEnvelope(
    string EventId,
    TradingSystem SourceSystem,
    EventCategory Category,
    DateTimeOffset OccurredAt,
    string CorrelationKey,
    string Subject,
    string Narrative,
    IReadOnlyDictionary<string, string> Dimensions,
    string PayloadJson);

public sealed record CommodityTradeEvent(
    string TradeId,
    string Commodity,
    string Benchmark,
    DateOnly TradeDate,
    decimal PriceUsd,
    decimal Volume,
    decimal PriceChangePercent,
    string Trader,
    string Desk,
    string Narrative);

public sealed record SwapCurveShiftEvent(
    string ShiftId,
    DateOnly CurveDate,
    decimal TwoYearRateBps,
    decimal FiveYearRateBps,
    decimal TenYearRateBps,
    decimal TwoYearDailyMoveBps,
    decimal FiveYearDailyMoveBps,
    decimal TenYearDailyMoveBps,
    string Desk,
    string Narrative);

public sealed record CorrelationScenarioRequest(
    string ScenarioId,
    DateOnly StartDate,
    int CommodityShockDays,
    int InterestRateLagDays,
    string CorrelationKey);

public sealed record CorrelationResult(
    string CorrelationId,
    string ScenarioId,
    string CorrelationKey,
    DateTimeOffset EvaluatedAt,
    string Summary,
    decimal Confidence,
    string OllamaModel,
    bool ScenarioMatched);

public static class ScenarioCatalog
{
    public const string OilPriceSpikeToSwapRepricing = "scenario-1-oil-price-spike-to-swap-repricing";
    public const string DefaultCorrelationKey = "oil-inflation-swap-repricing";
}

public static class ServiceBusTopology
{
    public const string CommoditiesTopic = "commodities-events";
    public const string RatesTopic = "rates-events";
    public const string CorrelationResultsTopic = "correlation-results";
    public const string CorrelationResultsObserverSubscription = "correlation-results-observer";
    public const string CommoditiesWorkerSubscription = "commodities-worker";
    public const string RatesWorkerSubscription = "rates-worker";
    public const string CorrelationWorkerSubscription = "correlation-worker";
}
