namespace AgentHub.Commodities.Domain;

public sealed class CommodityTradeEntity
{
    public int Id { get; set; }
    public string TradeId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string CorrelationKey { get; set; } = string.Empty;
    public string Commodity { get; set; } = "WTI Crude Oil";
    public string Benchmark { get; set; } = "WTI";
    public DateOnly TradeDate { get; set; }
    public decimal PriceUsd { get; set; }
    public decimal Volume { get; set; }
    public decimal PriceChangePercent { get; set; }
    public string Trader { get; set; } = string.Empty;
    public string Desk { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
