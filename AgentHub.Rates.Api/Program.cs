using AgentHub.Contracts;
using AgentHub.Rates.Api;
using AgentHub.Rates.Domain;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<RatesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ratesdb")));
builder.Services.AddScoped<RatesScenarioService>();
builder.Services.AddSingleton(_ => new ServiceBusClient(
    builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException("ServiceBus:ConnectionString is required.")));
builder.Services.AddSingleton<RatesEventPublisher>();
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

app.MapGet("/api/swaps", async (RatesDbContext dbContext, CancellationToken cancellationToken) =>
{
    var shifts = await dbContext.SwapCurveShifts
        .OrderBy(item => item.CurveDate)
        .ToListAsync(cancellationToken);

    return Results.Ok(shifts);
});

    var publishedEvents = 0;
    var publishFailures = 0;

app.MapPost("/api/scenarios/scenario-1/inject", async (
    CorrelationScenarioRequest? request,
    RatesDbContext dbContext,
    RatesScenarioService scenarioService,
    RatesEventPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var scenario = NormalizeRequest(request);
    var existing = await dbContext.SwapCurveShifts
        .Where(item => item.ScenarioId == scenario.ScenarioId && item.CorrelationKey == scenario.CorrelationKey)
        .ToListAsync(cancellationToken);

    if (existing.Count > 0)
    {
        dbContext.SwapCurveShifts.RemoveRange(existing);
    }

    var shifts = scenarioService.BuildOilDrivenSwapRepricingScenario(scenario);
    await dbContext.SwapCurveShifts.AddRangeAsync(shifts, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    foreach (var shift in shifts)
    {
        var swapEvent = new SwapCurveShiftEvent(
            shift.ShiftId,
            shift.CurveDate,
            shift.TwoYearRateBps,
            shift.FiveYearRateBps,
            shift.TenYearRateBps,
            shift.TwoYearDailyMoveBps,
            shift.FiveYearDailyMoveBps,
            shift.TenYearDailyMoveBps,
            shift.Desk,
            shift.Narrative);

        var envelope = new MarketEventEnvelope(
            EventId: shift.ShiftId,
            SourceSystem: TradingSystem.InterestRateDerivatives,
            Category: EventCategory.CurveShift,
            OccurredAt: shift.CurveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CorrelationKey: scenario.CorrelationKey,
            Subject: "Oil-driven swap repricing",
            Narrative: shift.Narrative,
            Dimensions: new Dictionary<string, string>
            {
                ["scenarioId"] = scenario.ScenarioId,
                ["desk"] = shift.Desk,
                ["curve"] = "USD swap"
            },
            PayloadJson: JsonSerializer.Serialize(swapEvent));

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
        injectedCurveSnapshots = shifts.Count,
        publishedEvents,
        publishFailures,
        firstDate = shifts.Min(item => item.CurveDate),
        lastDate = shifts.Max(item => item.CurveDate)
    });
});

app.MapGet("/api/scenarios/scenario-1/context/{correlationKey}", async (
    string correlationKey,
    RatesDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var shifts = await dbContext.SwapCurveShifts
        .Where(item => item.CorrelationKey == correlationKey)
        .OrderBy(item => item.CurveDate)
        .ToListAsync(cancellationToken);

    return shifts.Count == 0 ? Results.NotFound() : Results.Ok(shifts);
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
    var dbContext = scope.ServiceProvider.GetRequiredService<RatesDbContext>();

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
