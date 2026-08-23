using StudyHive.Api.Contracts;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>Test double replacing the real HTTP-based IPlannerClient (see WithFakePlanner below) so
/// workflow tests never depend on the FastAPI agent service actually running.</summary>
public sealed class FakePlannerClient : IPlannerClient
{
    public Func<PlannerRequest, PlannerResponse>? OnPlan { get; set; }
    public Exception? ThrowOnPlan { get; set; }

    public Task<PlannerResponse> PlanAsync(PlannerRequest request, CancellationToken ct)
    {
        if (ThrowOnPlan is not null) throw ThrowOnPlan;

        var response = OnPlan?.Invoke(request) ?? new PlannerResponse
        {
            PlanId = Guid.NewGuid(),
            Eligible = request.StudentEligible,
            Reasons = request.EligibilityReasons,
            Steps = request.StudentEligible
                ? [new PlannerStep { N = 1, Agent = "Planner", Action = "create_plan", Params = new Dictionary<string, object?>() }]
                : [],
        };
        return Task.FromResult(response);
    }
}
