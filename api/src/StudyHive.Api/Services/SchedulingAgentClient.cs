using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyHive.Api.Contracts;

namespace StudyHive.Api.Services;

/// <summary>Calls the internal FastAPI S2 Scheduling Agent.</summary>
public interface ISchedulingAgentClient
{
    Task<SchedulingResponse> ProposeAsync(SchedulingRequest request, CancellationToken ct);
}

public sealed class SchedulingAgentClient(HttpClient http) : ISchedulingAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SchedulingResponse> ProposeAsync(
        SchedulingRequest request,
        CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            "/scheduling/propose", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SchedulingResponse>(JsonOptions, ct);
        return body ?? throw new InvalidOperationException(
            "Scheduling Agent returned an empty response body.");
    }
}
