using AgentHub.Web;
using AgentHub.Web.Components;
using AgentHub.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
builder.Services.AddScoped<MacroNarrativeService>();

builder.Services.AddHttpClient("commodities-api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:CommoditiesApi"] ?? "http://localhost:17011");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient("rates-api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:RatesApi"] ?? "http://localhost:17012");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:Ollama"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapPost("/api/scenario-1/bootstrap", async (MacroNarrativeService narrativeService, CancellationToken cancellationToken) =>
{
    var result = await narrativeService.BootstrapScenarioOneAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/macro-query", async (MacroQueryRequest request, MacroNarrativeService narrativeService, CancellationToken cancellationToken) =>
{
    var result = await narrativeService.QueryAsync(request.Question, request.CorrelationKey, cancellationToken);
    return Results.Ok(result);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

internal sealed record MacroQueryRequest(string Question, string? CorrelationKey);
