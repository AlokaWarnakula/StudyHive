using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using StudyHive.Api.Contracts;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// Pins the C# Validation contract to the Python agent's real wire shape (agent/app/schemas.py), so
/// <see cref="FakeValidationClient"/> cannot hide drift: the real <see cref="ValidationClient"/> must
/// post to the agent's route, send the exact camelCase keys the agent validates, and parse a response
/// the agent actually produced.
/// </summary>
public class ValidationContractTests
{
    private static readonly Guid RoomId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConsumableId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Captured verbatim from POST /validation/validate on the agent (GROK_API_KEY blank) for the
    // request built by Request() below with budget 400 and 1 marker available.
    private const string AgentResponseJson = """
        {"valid": false, "results": [{"rule": "validate_schema", "passed": true, "detail": "Proposal and quotation match the required schema."}, {"rule": "validate_capacity", "passed": true, "detail": "Every proposed room holds the group of 4."}, {"rule": "validate_no_overlap", "passed": true, "detail": "No proposed slot overlaps a booking or maintenance."}, {"rule": "validate_stock", "passed": false, "detail": "Insufficient stock. Marker: 4 requested, 1 available."}, {"rule": "validate_budget", "passed": true, "detail": "Quotation total 230.00 is within the budget of 400.00."}], "quotation": {"roomFee": 225.0, "consumableCost": 5.0, "total": 230.0, "lineItems": [{"itemType": "Room", "itemName": "B-204 2026-10-01T09:00:00+05:30", "quantity": 1.5, "unitPrice": 150.0, "lineTotal": 225.0, "roomId": "11111111-1111-1111-1111-111111111111", "consumableId": null}, {"itemType": "Consumable", "itemName": "Marker", "quantity": 4.0, "unitPrice": 1.25, "lineTotal": 5.0, "roomId": null, "consumableId": "22222222-2222-2222-2222-222222222222"}]}, "failures": ["Insufficient stock. Marker: 4 requested, 1 available."], "revisionNote": "Please revise this request before it can be approved: Insufficient stock. Marker: 4 requested, 1 available."}
        """;

    private static ValidationRequest Request()
    {
        var startsAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromMinutes(330));
        return new ValidationRequest
        {
            Objective = "x",
            GroupSize = 4,
            Budget = 400m,
            SessionsRequired = 1,
            SessionDurationMinutes = 90,
            ProposedSlots =
            [
                new SchedulingSlot { RoomId = RoomId, RoomName = "B-204", StartsAt = startsAt, EndsAt = startsAt.AddMinutes(90), HourlyRate = 150m },
            ],
            Rooms =
            [
                new SchedulingRoom
                {
                    RoomId = RoomId, RoomName = "B-204", Capacity = 8, HourlyRate = 150m, IsActive = true,
                    EquipmentTypeIds = [], Bookings = [], MaintenanceWindows = [],
                },
            ],
            Items = [new ResourceRequestItem { ConsumableId = ConsumableId, Name = "Marker", Requested = 4, Available = 1, UnitPrice = 1.25m }],
        };
    }

    [Fact]
    public async Task ValidationClient_Posts_The_Agents_Request_Shape_And_Parses_A_Real_Agent_Response()
    {
        var handler = new CapturingHandler(AgentResponseJson);
        var client = new ValidationClient(new HttpClient(handler) { BaseAddress = new Uri("http://agent.test") });

        var response = await client.ValidateAsync(Request(), CancellationToken.None);

        handler.RequestUri!.AbsolutePath.Should().Be("/validation/validate");
        using (var sent = JsonDocument.Parse(handler.Body!))
        {
            // agent/app/schemas.py ValidationRequest aliases
            sent.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                "objective", "groupSize", "budget", "sessionsRequired", "sessionDurationMinutes",
                "proposedSlots", "rooms", "items");
            sent.RootElement.GetProperty("proposedSlots")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                "roomId", "roomName", "startsAt", "endsAt", "hourlyRate");
            sent.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                "consumableId", "name", "requested", "available", "unitPrice");
        }

        response.Valid.Should().BeFalse();
        response.Results.Select(r => r.Rule).Should().Equal(
            "validate_schema", "validate_capacity", "validate_no_overlap", "validate_stock", "validate_budget");
        response.Failures.Should().ContainSingle().Which.Should().Contain("Marker: 4 requested, 1 available.");
        response.RevisionNote.Should().StartWith("Please revise this request");

        response.Quotation.RoomFee.Should().Be(225m);
        response.Quotation.ConsumableCost.Should().Be(5m);
        response.Quotation.Total.Should().Be(230m);
        var room = response.Quotation.LineItems[0];
        room.ItemType.Should().Be("Room");
        room.RoomId.Should().Be(RoomId);
        room.ConsumableId.Should().BeNull();
        room.Quantity.Should().Be(1.5m);
        room.LineTotal.Should().Be(225m);
        var consumable = response.Quotation.LineItems[1];
        consumable.ItemType.Should().Be("Consumable");
        consumable.ConsumableId.Should().Be(ConsumableId);
        consumable.RoomId.Should().BeNull();
    }

    [Fact]
    public void FakeValidationClient_Prices_The_Same_Request_Exactly_As_The_Agent_Did()
    {
        var agent = JsonSerializer.Deserialize<ValidationResponse>(AgentResponseJson, TestSupport.JsonOptions)!;

        var fake = FakeValidationClient.Price(Request());

        fake.Valid.Should().Be(agent.Valid);
        fake.Quotation.RoomFee.Should().Be(agent.Quotation.RoomFee);
        fake.Quotation.ConsumableCost.Should().Be(agent.Quotation.ConsumableCost);
        fake.Quotation.Total.Should().Be(agent.Quotation.Total);
        fake.Quotation.LineItems.Select(l => (l.ItemType, l.Quantity, l.UnitPrice, l.LineTotal, l.RoomId, l.ConsumableId))
            .Should().Equal(agent.Quotation.LineItems.Select(l => (l.ItemType, l.Quantity, l.UnitPrice, l.LineTotal, l.RoomId, l.ConsumableId)));
        fake.Failures.Should().Equal(agent.Failures);
    }

    [Fact]
    public async Task ValidationClient_Throws_On_A_Non_Success_Status()
    {
        var client = new ValidationClient(new HttpClient(new CapturingHandler("{}", HttpStatusCode.Unauthorized))
        {
            BaseAddress = new Uri("http://agent.test"),
        });

        var act = () => client.ValidateAsync(Request(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class CapturingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
