using AgentHub.Contracts;
using ModelContextProtocol.Client;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgentHub.Web.Services;

public sealed class MacroNarrativeService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    public const string DefaultQuestion = "Is there a coherent macro narrative that explains current positions across both trading books?";

    public async Task<ScenarioBootstrapResult> BootstrapScenarioOneAsync(CancellationToken cancellationToken)
    {
        var request = new CorrelationScenarioRequest(
            ScenarioCatalog.OilPriceSpikeToSwapRepricing,
            DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-5),
            CommodityShockDays: 5,
            InterestRateLagDays: 2,
            CorrelationKey: ScenarioCatalog.DefaultCorrelationKey);

        var commoditiesClient = httpClientFactory.CreateClient("commodities-api");
        var ratesClient = httpClientFactory.CreateClient("rates-api");

        using var commoditiesResponse = await commoditiesClient.PostAsJsonAsync("/api/scenarios/scenario-1/inject", request, cancellationToken);
        using var ratesResponse = await ratesClient.PostAsJsonAsync("/api/scenarios/scenario-1/inject", request, cancellationToken);

        commoditiesResponse.EnsureSuccessStatusCode();
        ratesResponse.EnsureSuccessStatusCode();

        var commoditiesPayload = await commoditiesResponse.Content.ReadAsStringAsync(cancellationToken);
        var ratesPayload = await ratesResponse.Content.ReadAsStringAsync(cancellationToken);

        return new ScenarioBootstrapResult(request.CorrelationKey, commoditiesPayload, ratesPayload);
    }

    public async Task<MacroNarrativeResult> QueryAsync(string question, string? correlationKey, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(correlationKey) ? ScenarioCatalog.DefaultCorrelationKey : correlationKey;
        var commoditiesClient = httpClientFactory.CreateClient("commodities-api");
        var ratesClient = httpClientFactory.CreateClient("rates-api");
        var ollamaModel = configuration["Ollama:Model"] ?? "phi4-mini";
        using var ollamaClient = CreateOllamaClient();

        var commodityPositions = await commoditiesClient.GetStringAsync($"/api/scenarios/scenario-1/context/{key}", cancellationToken);
        var ratesPositions = await ratesClient.GetStringAsync($"/api/scenarios/scenario-1/context/{key}", cancellationToken);
        var commodityMcpContext = await GetMcpContextAsync(configuration["ServiceEndpoints:CommoditiesMcp"], "GetCommodityScenarioContext", key, cancellationToken);
        var ratesMcpContext = await GetMcpContextAsync(configuration["ServiceEndpoints:RatesMcp"], "GetSwapRepricingContext", key, cancellationToken);

        await EnsureModelAvailableAsync(ollamaClient, ollamaModel, cancellationToken);

        var prompt = $"""
You are reviewing two trading books and must answer the user's question using only the supplied evidence.

User question:
{question}

Correlation scenario under test:
Oil spikes -> inflation expectations rise -> longer-duration swap rates reprice upward after a short lag while the 2Y moves less.

Commodities trading book positions:
{commodityPositions}

Interest-rate derivatives trading book positions:
{ratesPositions}

Commodities MCP summary:
{commodityMcpContext}

Rates MCP summary:
{ratesMcpContext}

Produce a direct answer that states whether there is a coherent macro narrative, explain the transmission channel, and mention whether scenario 1 appears to be matched.
""";

        using var response = await ollamaClient.PostAsJsonAsync("/api/generate", new
        {
            model = ollamaModel,
            prompt,
            stream = false
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

        return new MacroNarrativeResult(
            key,
            question,
            commodityMcpContext,
            ratesMcpContext,
            commodityPositions,
            ratesPositions,
            payload?.Response ?? "No response returned by Ollama.",
            ollamaModel,
            DateTimeOffset.UtcNow);
    }

    private static async Task EnsureModelAvailableAsync(HttpClient ollamaClient, string model, CancellationToken cancellationToken)
    {
        using var tagsResponse = await ollamaClient.GetAsync("/api/tags", cancellationToken);
        tagsResponse.EnsureSuccessStatusCode();

        var tags = await tagsResponse.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);
        if (tags?.Models.Any(item => ModelMatches(item.Name, model)) == true)
        {
            return;
        }

        using var pullResponse = await ollamaClient.PostAsJsonAsync("/api/pull", new
        {
            name = model,
            stream = false
        }, cancellationToken);

        pullResponse.EnsureSuccessStatusCode();
    }

    private HttpClient CreateOllamaClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(configuration["ServiceEndpoints:Ollama"] ?? "http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    private static async Task<string> GetMcpContextAsync(string? endpoint, string toolName, string correlationKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "MCP endpoint not configured.";
        }

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint)
        }), cancellationToken: cancellationToken);

        var availableTools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var resolvedToolName = ResolveToolName(availableTools.Select(tool => tool.Name), toolName);

        if (resolvedToolName is null)
        {
            return $"Requested MCP tool '{toolName}' was not found. Available tools: {string.Join(", ", availableTools.Select(tool => tool.Name))}";
        }

        var result = await client.CallToolAsync(resolvedToolName, new Dictionary<string, object?>
        {
            ["correlationKey"] = correlationKey
        }, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(result.Content);
    }

    private static string? ResolveToolName(IEnumerable<string> availableTools, string requestedToolName)
    {
        static string Normalize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        var requestedNormalized = Normalize(requestedToolName);

        return availableTools.FirstOrDefault(tool => string.Equals(tool, requestedToolName, StringComparison.Ordinal))
            ?? availableTools.FirstOrDefault(tool => string.Equals(tool, requestedToolName, StringComparison.OrdinalIgnoreCase))
            ?? availableTools.FirstOrDefault(tool => Normalize(tool) == requestedNormalized);
    }

    private static bool ModelMatches(string availableModelName, string requestedModelName)
    {
        if (string.Equals(availableModelName, requestedModelName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var availableBaseName = availableModelName.Split(':', 2)[0];
        var requestedBaseName = requestedModelName.Split(':', 2)[0];

        return string.Equals(availableBaseName, requestedBaseName, StringComparison.OrdinalIgnoreCase);
    }

    public sealed record ScenarioBootstrapResult(string CorrelationKey, string CommoditiesResponseJson, string RatesResponseJson);

    public sealed record MacroNarrativeResult(
        string CorrelationKey,
        string Question,
        string CommodityMcpContext,
        string RatesMcpContext,
        string CommodityPositionsJson,
        string RatesPositionsJson,
        string Narrative,
        string Model,
        DateTimeOffset GeneratedAt);

    private sealed record OllamaResponse(string Response);

    private sealed record OllamaTagsResponse(IReadOnlyList<OllamaModelTag> Models);

    private sealed record OllamaModelTag(string Name);
}
