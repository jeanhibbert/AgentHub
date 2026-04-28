using AgentHub.Contracts;
using Azure.Messaging.ServiceBus;

namespace AgentHub.Rates.Worker;

public sealed class RatesEventWorker(ServiceBusClient client, ILogger<RatesEventWorker> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(
            ServiceBusTopology.RatesTopic,
            ServiceBusTopology.RatesWorkerSubscription,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

        _processor.ProcessMessageAsync += async args =>
        {
            var envelope = args.Message.Body.ToObjectFromJson<MarketEventEnvelope>();
            logger.LogInformation("Rates worker observed event {EventId} at {OccurredAt}: {Narrative}", envelope?.EventId, envelope?.OccurredAt, envelope?.Narrative);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };

        _processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Rates worker processor failure for entity {EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
