using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2 equipment-type catalogue. A type is a capability such as a projector;
/// room_equipment stores the quantity installed in each room.
/// </summary>
[ApiController]
[Route("api/equipment")]
[Authorize]
public sealed class EquipmentController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>Add an equipment type.</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(typeof(EquipmentTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEquipmentTypeRequest request,
        CancellationToken ct)
    {
        if (!Validate(request.Name, request.Category, request.Description))
        {
            return ValidationProblem(ModelState);
        }

        var name = request.Name.Trim();
        if (await db.EquipmentTypes.AnyAsync(e => e.Name == name, ct))
        {
            ModelState.AddModelError(nameof(request.Name), "An equipment type with this name already exists.");
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var equipment = new EquipmentType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = request.Category.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.EquipmentTypes.Add(equipment);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, ToResponse(equipment));
    }

    /// <summary>List equipment types. Backs W-16.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EquipmentTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        IQueryable<EquipmentType> equipment = db.EquipmentTypes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            equipment = equipment.Where(e =>
                EF.Functions.ILike(e.Name, pattern) ||
                EF.Functions.ILike(e.Category, pattern) ||
                (e.Description != null && EF.Functions.ILike(e.Description, pattern)));
        }

        var direction = query.SortDir.ToLowerInvariant();
        if (direction is not ("asc" or "desc"))
        {
            ModelState.AddModelError(nameof(query.SortDir), "sortDir must be either 'asc' or 'desc'.");
            return ValidationProblem(ModelState);
        }

        var descending = direction == "desc";
        equipment = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending
                ? equipment.OrderByDescending(e => e.CreatedAt)
                : equipment.OrderBy(e => e.CreatedAt),
            "name" => descending
                ? equipment.OrderByDescending(e => e.Name)
                : equipment.OrderBy(e => e.Name),
            "category" => descending
                ? equipment.OrderByDescending(e => e.Category)
                : equipment.OrderBy(e => e.Category),
            _ => null!,
        };

        if (equipment is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await equipment.CountAsync(ct);
        var items = await equipment
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new EquipmentTypeResponse(
                e.Id,
                e.Name,
                e.Category,
                e.Description,
                e.IsActive,
                e.CreatedAt,
                e.UpdatedAt))
            .ToListAsync(ct);

        return Ok(PagedResult<EquipmentTypeResponse>.Create(
            items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>Update an equipment type.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(EquipmentTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEquipmentTypeRequest request,
        CancellationToken ct)
    {
        if (!Validate(request.Name, request.Category, request.Description))
        {
            return ValidationProblem(ModelState);
        }

        var equipment = await db.EquipmentTypes.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (equipment is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (await db.EquipmentTypes.AnyAsync(e => e.Id != id && e.Name == name, ct))
        {
            ModelState.AddModelError(nameof(request.Name), "An equipment type with this name already exists.");
            return ValidationProblem(ModelState);
        }

        equipment.Name = name;
        equipment.Category = request.Category.Trim();
        equipment.Description = NormalizeOptional(request.Description);
        equipment.IsActive = request.IsActive;
        equipment.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(equipment));
    }

    private bool Validate(string? name, string? category, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Equipment name is required.");
        }
        else if (name.Trim().Length > 80)
        {
            ModelState.AddModelError(nameof(name), "Equipment name cannot exceed 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            ModelState.AddModelError(nameof(category), "Category is required.");
        }
        else if (category.Trim().Length > 40)
        {
            ModelState.AddModelError(nameof(category), "Category cannot exceed 40 characters.");
        }

        if (description?.Length > 500)
        {
            ModelState.AddModelError(nameof(description), "Description cannot exceed 500 characters.");
        }

        return ModelState.IsValid;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EquipmentTypeResponse ToResponse(EquipmentType equipment) => new(
        equipment.Id,
        equipment.Name,
        equipment.Category,
        equipment.Description,
        equipment.IsActive,
        equipment.CreatedAt,
        equipment.UpdatedAt);
}

public sealed record CreateEquipmentTypeRequest(
    string Name,
    string Category,
    string? Description);

public sealed record UpdateEquipmentTypeRequest(
    string Name,
    string Category,
    string? Description,
    bool IsActive);

public sealed record EquipmentTypeResponse(
    Guid Id,
    string Name,
    string Category,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
