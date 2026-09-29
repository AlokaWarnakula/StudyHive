using StudyHive.Api.Contracts;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// S4 test double used when API workflow tests should not call the FastAPI process. By default it
/// prices the proposal with the agent's own arithmetic (agent/app/agents/validation.py: hours and
/// prices rounded to 2 dp, line total = quantity x unit price) and fails the stock and budget rules
/// the same way, so the workflow's response checks and the persisted quotation are exercised against
/// realistic numbers. <see cref="ValidationContractTests"/> pins the wire shape against a real agent
/// response so this fake cannot drift from it unnoticed.
/// </summary>
public sealed class FakeValidationClient : IValidationClient
{
    public Func<ValidationRequest, ValidationResponse>? OnValidate { get; init; }
    public Exception? ThrowOnValidate { get; init; }
    public int Calls { get; private set; }

    public Task<ValidationResponse> ValidateAsync(ValidationRequest request, CancellationToken ct)
    {
        Calls++;
        if (ThrowOnValidate is not null)
        {
            throw ThrowOnValidate;
        }

        return Task.FromResult(OnValidate is not null ? OnValidate(request) : Price(request));
    }

    public static ValidationResponse Price(ValidationRequest request)
    {
        var lines = new List<ValidationQuotationLineItem>();
        foreach (var slot in request.ProposedSlots)
        {
            var hours = Math.Round((decimal)(slot.EndsAt - slot.StartsAt).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);
            var rate = request.Rooms.FirstOrDefault(r => r.RoomId == slot.RoomId)?.HourlyRate ?? slot.HourlyRate;
            lines.Add(new ValidationQuotationLineItem
            {
                ItemType = "Room",
                ItemName = $"{slot.RoomName} {slot.StartsAt:O}",
                Quantity = hours,
                UnitPrice = rate,
                LineTotal = Math.Round(hours * rate, 2, MidpointRounding.AwayFromZero),
                RoomId = slot.RoomId,
            });
        }

        foreach (var item in request.Items)
        {
            lines.Add(new ValidationQuotationLineItem
            {
                ItemType = "Consumable",
                ItemName = item.Name,
                Quantity = item.Requested,
                UnitPrice = item.UnitPrice,
                LineTotal = Math.Round(item.Requested * item.UnitPrice, 2, MidpointRounding.AwayFromZero),
                ConsumableId = item.ConsumableId,
            });
        }

        var roomFee = lines.Where(l => l.ItemType == "Room").Sum(l => l.LineTotal);
        var consumableCost = lines.Where(l => l.ItemType == "Consumable").Sum(l => l.LineTotal);
        var total = roomFee + consumableCost;

        var results = new List<ValidationRuleResult>
        {
            new() { Rule = "validate_schema", Passed = true, Detail = "Fake: schema ok." },
        };
        if (request.Items.Count > 0)
        {
            var shortItems = request.Items.Where(i => i.Available < i.Requested).ToList();
            results.Add(new ValidationRuleResult
            {
                Rule = "validate_stock",
                Passed = shortItems.Count == 0,
                Detail = shortItems.Count == 0
                    ? "Every requested item is in stock."
                    : "Insufficient stock. " + string.Join(" ", shortItems.Select(i => $"{i.Name}: {i.Requested} requested, {i.Available} available.")),
            });
        }
        results.Add(new ValidationRuleResult
        {
            Rule = "validate_budget",
            Passed = total <= request.Budget,
            Detail = total <= request.Budget
                ? $"Quotation total {total:0.00} is within the budget of {request.Budget:0.00}."
                : $"Quotation total {total:0.00} exceeds the budget of {request.Budget:0.00} by {total - request.Budget:0.00}.",
        });

        var failures = results.Where(r => !r.Passed).Select(r => r.Detail).ToList();
        return new ValidationResponse
        {
            Valid = failures.Count == 0,
            Results = results,
            Quotation = new ValidationQuotation
            {
                RoomFee = roomFee,
                ConsumableCost = consumableCost,
                Total = total,
                LineItems = lines,
            },
            Failures = failures,
            RevisionNote = failures.Count == 0
                ? null
                : "Please revise this request before it can be approved: " + string.Join(" ", failures),
        };
    }
}
