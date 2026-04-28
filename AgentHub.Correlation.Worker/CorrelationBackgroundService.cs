using AgentHub.Contracts;
using Azure.Messaging.ServiceBus;
using ModelContextProtocol.Client;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentHub.Correlation.Worker;

public sealed class CorrelationBackgroundService(
    ServiceBusClient client,
    OllamaChatClient ollamaClient,
    IConfiguration configuration,
    ILogger<CorrelationBackgroundService> logger) : BackgroundService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, MarketEventEnvelope> _commodityEvents = new();
    private readonly ConcurrentDictionary<string, MarketEventEnvelope> _ratesEvents = new();
    private readonly ConcurrentDictionary<string, byte> _completedCorrelations = new();
    private readonly ServiceBusSender _sender = client.CreateSender(ServiceBusTopology.CorrelationResultsTopic);
    private ServiceBusProcessor? _commodityProcessor;
    private ServiceBusProcessor? _ratesProcessor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _commodityProcessor = CreateProcessor(ServiceBusTopology.CommoditiesTopic);
        _ratesProcessor = CreateProcessor(ServiceBusTopology.RatesTopic);

        await _commodityProcessor.StartProcessingAsync(stoppingToken);
        await _ratesProcessor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private ServiceBusProcessor CreateProcessor(string topicName)
    {
        var processor = client.CreateProcessor(topicName, ServiceBusTopology.CorrelationWorkerSubscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async args =>
        {
            var envelope = args.Message.Body.ToObjectFromJson<MarketEventEnvelope>();
            if (envelope is null)
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            if (envelope.SourceSystem == TradingSystem.Commodities)
            {
                _commodityEvents[envelope.CorrelationKey] = envelope;
            }
            else if (envelope.SourceSystem == TradingSystem.InterestRateDerivatives)
            {
                _ratesEvents[envelope.CorrelationKey] = envelope;
            }

            await TryCorrelateAsync(envelope.CorrelationKey, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Correlation processor failed for {EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };

        return processor;
    }

    private async Task TryCorrelateAsync(string correlationKey, CancellationToken cancellationToken)
    {
        if (_completedCorrelations.ContainsKey(correlationKey))
        {
            return;
        }

        if (!_commodityEvents.TryGetValue(correlationKey, out var commodityEvent)
            || !_ratesEvents.TryGetValue(correlationKey, out var ratesEvent))
        {
            return;
        }

        if (!_completedCorrelations.TryAdd(correlationKey, 0))
        {
            return;
        }

        var commodityContext = await GetMcpContextAsync(configuration["McpEndpoints:Commodities"], "GetCommodityScenarioContext", correlationKey, cancellationToken);
        var ratesContext = await GetMcpContextAsync(configuration["McpEndpoints:Rates"], "GetSwapRepricingContext", correlationKey, cancellationToken);

        var prompt = $"""
You are evaluating whether two trading-system event streams show the same macro relationship.

Scenario hypothesis:
Oil spikes -> inflation expectations rise -> 5Y and 10Y swap rates reprice upward after a short lag while the 2Y moves less.

Commodity event:
{commodityEvent.Narrative}

Rates event:
{ratesEvent.Narrative}

Commodity MCP context:
{commodityContext}

Rates MCP context:
{ratesContext}

Return a compact explanation stating whether scenario 1 is matched, why, and the confidence as a decimal between 0 and 1.
""";

        var (response, model) = await ollamaClient.GenerateCorrelationNarrativeAsync(prompt, cancellationToken);
        var scenarioMatched = response.Contains("match", StringComparison.OrdinalIgnoreCase)
            || response.Contains("correlat", StringComparison.OrdinalIgnoreCase)
            || response.Contains("inflation", StringComparison.OrdinalIgnoreCase);
        var confidence = scenarioMatched ? 0.92m : 0.35m;

        var result = new CorrelationResult(
            CorrelationId: Guid.NewGuid().ToString("N"),
            ScenarioId: ScenarioCatalog.OilPriceSpikeToSwapRepricing,
            CorrelationKey: correlationKey,
            EvaluatedAt: DateTimeOffset.UtcNow,
            Summary: response,
            Confidence: confidence,
            OllamaModel: model,
            ScenarioMatched: scenarioMatched);

        var envelope = new MarketEventEnvelope(
            EventId: result.CorrelationId,
            SourceSystem: TradingSystem.Correlation,
            Category: EventCategory.CorrelationInsight,
            OccurredAt: result.EvaluatedAt,
            CorrelationKey: correlationKey,
            Subject: "Scenario 1 correlation result",
            Narrative: result.Summary,
            Dimensions: new Dictionary<string, string>
            {
                ["scenarioId"] = result.ScenarioId,
                ["confidence"] = result.Confidence.ToString("0.00")
            },
            PayloadJson: JsonSerializer.Serialize(result));

        await _sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(envelope))
        {
            MessageId = envelope.EventId,
            CorrelationId = envelope.CorrelationKey,
            Subject = envelope.Subject,
            ContentType = "application/json"
        }, cancellationToken);

        logger.LogInformation("Correlation result for {CorrelationKey}: {Summary}", correlationKey, response);
    }

    private static async Task<string> GetMcpContextAsync(string? endpoint, string toolName, string correlationKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "MCP endpoint not configured.";
        }

        try
        {
            await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint)
            }), cancellationToken: cancellationToken);

            var result = await client.CallToolAsync(toolName, new Dictionary<string, object?>
            {
                ["correlationKey"] = correlationKey
            }, cancellationToken: cancellationToken);

            return JsonSerializer.Serialize(result.Content);
        }
        catch (Exception exception)
        {
            return $"MCP lookup failed: {exception.Message}";
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_commodityProcessor is not null)
        {
            await _commodityProcessor.StopProcessingAsync(cancellationToken);
            await _commodityProcessor.DisposeAsync();
        }

        if (_ratesProcessor is not null)
        {
            await _ratesProcessor.StopProcessingAsync(cancellationToken);
            await _ratesProcessor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}
