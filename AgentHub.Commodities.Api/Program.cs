using AgentHub.Commodities.Api;
using AgentHub.Commodities.Domain;
using AgentHub.Contracts;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<CommoditiesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("commoditiesdb")));
builder.Services.AddScoped<CommoditiesScenarioService>();
builder.Services.AddSingleton(_ => new ServiceBusClient(
    builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException("ServiceBus:ConnectionString is required.")));
builder.Services.AddSingleton<CommodityEventPublisher>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = false;
#pragma warning disable MCP9004
        options.EnableLegacySse = true;
#pragma warning restore MCP9004
    })
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await EnsureDatabaseAsync(app.Services);

app.MapGet("/api/trades", async (CommoditiesDbContext dbContext, CancellationToken cancellationToken) =>
{
    var trades = await dbContext.CommodityTrades
        .OrderBy(item => item.TradeDate)
        .ToListAsync(cancellationToken);

    return Results.Ok(trades);
});

    var publishedEvents = 0;
    var publishFailures = 0;

app.MapPost("/api/scenarios/scenario-1/inject", async (
    CorrelationScenarioRequest? request,
    CommoditiesDbContext dbContext,
    CommoditiesScenarioService scenarioService,
    CommodityEventPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var scenario = NormalizeRequest(request);
    var existing = await dbContext.CommodityTrades
        .Where(item => item.ScenarioId == scenario.ScenarioId && item.CorrelationKey == scenario.CorrelationKey)
        .ToListAsync(cancellationToken);

    if (existing.Count > 0)
    {
        dbContext.CommodityTrades.RemoveRange(existing);
    }

    var trades = scenarioService.BuildOilSpikeScenario(scenario);
    await dbContext.CommodityTrades.AddRangeAsync(trades, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    foreach (var trade in trades)
    {
        var tradeEvent = new CommodityTradeEvent(
            trade.TradeId,
            trade.Commodity,
            trade.Benchmark,
            trade.TradeDate,
            trade.PriceUsd,
            trade.Volume,
            trade.PriceChangePercent,
            trade.Trader,
            trade.Desk,
            trade.Narrative);

        var envelope = new MarketEventEnvelope(
            EventId: trade.TradeId,
            SourceSystem: TradingSystem.Commodities,
            Category: EventCategory.CommodityTrade,
            OccurredAt: trade.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CorrelationKey: scenario.CorrelationKey,
            Subject: "WTI crude oil inflation shock",
            Narrative: trade.Narrative,
            Dimensions: new Dictionary<string, string>
            {
                ["scenarioId"] = scenario.ScenarioId,
                ["benchmark"] = trade.Benchmark,
                ["desk"] = trade.Desk
            },
            PayloadJson: JsonSerializer.Serialize(tradeEvent));

        if (await publisher.PublishAsync(envelope, cancellationToken))
        {
            publishedEvents++;
        }
        else
        {
            publishFailures++;
        }
    }

    return Results.Ok(new
    {
        scenario.ScenarioId,
        scenario.CorrelationKey,
        injectedTrades = trades.Count,
        publishedEvents,
        publishFailures,
        firstDate = trades.Min(item => item.TradeDate),
        lastDate = trades.Max(item => item.TradeDate)
    });
});

app.MapGet("/api/scenarios/scenario-1/context/{correlationKey}", async (
    string correlationKey,
    CommoditiesDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var trades = await dbContext.CommodityTrades
        .Where(item => item.CorrelationKey == correlationKey)
        .OrderBy(item => item.TradeDate)
        .ToListAsync(cancellationToken);

    return trades.Count == 0 ? Results.NotFound() : Results.Ok(trades);
});

app.MapMcp("/mcp");
app.MapDefaultEndpoints();
app.Run();

static CorrelationScenarioRequest NormalizeRequest(CorrelationScenarioRequest? request)
{
    var defaultStartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-5);

    return request ?? new CorrelationScenarioRequest(
        ScenarioCatalog.OilPriceSpikeToSwapRepricing,
        defaultStartDate,
        CommodityShockDays: 5,
        InterestRateLagDays: 2,
        CorrelationKey: ScenarioCatalog.DefaultCorrelationKey);
}

static async Task EnsureDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CommoditiesDbContext>();

    for (var attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            return;
        }
        catch when (attempt < 15)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
