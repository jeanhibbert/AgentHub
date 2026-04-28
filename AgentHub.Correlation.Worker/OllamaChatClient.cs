using System.Net.Http.Json;

namespace AgentHub.Correlation.Worker;

public sealed class OllamaChatClient(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<(string Response, string Model)> GenerateCorrelationNarrativeAsync(string prompt, CancellationToken cancellationToken)
    {
        var model = configuration["Ollama:Model"] ?? "phi4-mini";

        await EnsureModelAvailableAsync(model, cancellationToken);

        using var response = await httpClient.PostAsJsonAsync("/api/generate", new
        {
            model,
            prompt,
            stream = false
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        return (payload?.Response ?? "Ollama returned no response.", model);
    }

    private async Task EnsureModelAvailableAsync(string model, CancellationToken cancellationToken)
    {
        using var tagsResponse = await httpClient.GetAsync("/api/tags", cancellationToken);
        tagsResponse.EnsureSuccessStatusCode();

        var tags = await tagsResponse.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);
        if (tags?.Models.Any(item => string.Equals(item.Name, model, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return;
        }

        using var pullResponse = await httpClient.PostAsJsonAsync("/api/pull", new
        {
            name = model,
            stream = false
        }, cancellationToken);

        pullResponse.EnsureSuccessStatusCode();
    }

    private sealed record OllamaGenerateResponse(string Response);

    private sealed record OllamaTagsResponse(IReadOnlyList<OllamaModelTag> Models);

    private sealed record OllamaModelTag(string Name);
}
