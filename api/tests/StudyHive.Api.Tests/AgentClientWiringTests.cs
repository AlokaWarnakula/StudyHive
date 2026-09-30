using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Contracts;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// Every other test swaps the agent clients for fakes, so none of them exercises the real DI wiring.
/// These resolve the clients exactly as Program.cs registers them and capture the outgoing request
/// (the network is replaced, not the client). Regression for the Day 2 exit-gate run, where the
/// Scheduling client had no BaseAddress and no X-Internal-Api-Key and every real workflow failed.
/// </summary>
public sealed class AgentClientWiringTests : IDisposable
{
    private readonly CapturingHandler handler = new();
    private readonly WebApplicationFactory<Program> factory;

    public AgentClientWiringTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                    options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler))));
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Scheduling_Client_Calls_The_Agent_Base_Url_With_The_Internal_Key()
    {
        var agent = factory.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
        handler.ResponseJson = """{"slots":[],"conflicts":[]}""";

        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ISchedulingAgentClient>();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        await client.ProposeAsync(new SchedulingRequest
        {
            GroupSize = 2,
            PreferredDateFrom = day,
            PreferredDateTo = day,
            PreferredTimeFrom = new TimeOnly(9, 0),
            PreferredTimeTo = new TimeOnly(11, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 60,
            RequiredEquipmentTypeIds = [],
            Rooms = [],
        }, CancellationToken.None);

        var sent = handler.Requests.Should().ContainSingle().Subject;
        sent.RequestUri.Should().Be(new Uri(new Uri(agent.BaseUrl), "/scheduling/propose"));
        agent.InternalApiKey.Should().NotBeNullOrWhiteSpace();
        sent.Headers.GetValues("X-Internal-Api-Key").Should().Equal(agent.InternalApiKey);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string ResponseJson { get; set; } = "{}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
