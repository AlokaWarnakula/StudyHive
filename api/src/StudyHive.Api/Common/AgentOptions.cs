namespace StudyHive.Api.Common;

/// <summary>Bound from the "Agent" config section — where to reach the internal FastAPI agent service and the shared secret it expects on X-Internal-Api-Key. See agent/app/security.py.</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string BaseUrl { get; init; } = "http://localhost:8001";
    public string InternalApiKey { get; init; } = "";
}
