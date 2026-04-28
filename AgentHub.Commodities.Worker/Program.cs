using AgentHub.Commodities.Worker;
using Azure.Messaging.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton(_ => new ServiceBusClient(
    builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException("ServiceBus:ConnectionString is required.")));
builder.Services.AddHostedService<CommodityEventWorker>();

await builder.Build().RunAsync();
