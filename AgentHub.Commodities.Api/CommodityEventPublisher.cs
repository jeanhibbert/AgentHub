using AgentHub.Contracts;
using Azure.Messaging.ServiceBus;

namespace AgentHub.Commodities.Api;

public sealed class CommodityEventPublisher(ServiceBusClient client, ILogger<CommodityEventPublisher> logger) : IAsyncDisposable
{
    private readonly ServiceBusSender _sender = client.CreateSender(ServiceBusTopology.CommoditiesTopic);

    public async Task<bool> PublishAsync(MarketEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(envelope))
        {
            MessageId = envelope.EventId,
            CorrelationId = envelope.CorrelationKey,
            Subject = envelope.Subject,
            ContentType = "application/json"
        };

        message.ApplicationProperties["sourceSystem"] = envelope.SourceSystem.ToString();
        message.ApplicationProperties["category"] = envelope.Category.ToString();

        using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        publishTimeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await _sender.SendMessageAsync(message, publishTimeout.Token);
            logger.LogInformation("Published commodity event {EventId} for {CorrelationKey}", envelope.EventId, envelope.CorrelationKey);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Commodity event publish failed fast for {EventId}; scenario data remains injected locally.", envelope.EventId);
            return false;
        }
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
