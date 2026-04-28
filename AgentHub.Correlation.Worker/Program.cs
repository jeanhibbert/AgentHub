using AgentHub.Correlation.Worker;
using Azure.Messaging.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton(_ => new ServiceBusClient(
    builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException("ServiceBus:ConnectionString is required.")));
builder.Services.AddHttpClient<OllamaChatClient>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHostedService<CorrelationBackgroundService>();

await builder.Build().RunAsync();
