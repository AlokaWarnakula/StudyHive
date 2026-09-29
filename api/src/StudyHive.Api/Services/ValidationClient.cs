using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyHive.Api.Contracts;

namespace StudyHive.Api.Services;

/// <summary>Talks to the internal FastAPI Validation endpoint. Never called directly by React/Flutter — only from <see cref="WorkflowOrchestrationService"/>.</summary>
public interface IValidationClient
{
    Task<ValidationResponse> ValidateAsync(ValidationRequest request, CancellationToken ct);
}

public sealed class ValidationClient(HttpClient http) : IValidationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ValidationResponse> ValidateAsync(ValidationRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/validation/validate", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ValidationResponse>(JsonOptions, ct);
        return body ?? throw new InvalidOperationException("Validation service returned an empty response body.");
    }
}
