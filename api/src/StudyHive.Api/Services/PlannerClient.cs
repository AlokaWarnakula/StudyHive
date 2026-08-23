using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyHive.Api.Contracts;

namespace StudyHive.Api.Services;

/// <summary>Talks to the internal FastAPI Planner endpoint. Never called directly by React/Flutter — only from <see cref="WorkflowOrchestrationService"/>.</summary>
public interface IPlannerClient
{
    Task<PlannerResponse> PlanAsync(PlannerRequest request, CancellationToken ct);
}

public sealed class PlannerClient(HttpClient http) : IPlannerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<PlannerResponse> PlanAsync(PlannerRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/planner/plan", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PlannerResponse>(JsonOptions, ct);
        return body ?? throw new InvalidOperationException("Planner service returned an empty response body.");
    }
}
