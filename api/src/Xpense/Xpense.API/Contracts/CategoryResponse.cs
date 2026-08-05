using Xpense.Domain.Entities;

namespace Xpense.API.Contracts;

public sealed record CategoryResponse(
    int Id,
    string Label,
    PriorityResponse Priority,
    string CreatedAt,
    string? UpdatedAt)
{
    public static CategoryResponse Of(Category category) => new(
        category.Id,
        category.Label,
        PriorityResponse.Of(category.Priority),
        Timestamps.Iso(category.CreatedAt),
        Timestamps.Iso(category.UpdatedAt));
}

public sealed record PriorityResponse(
    int Id,
    string Label,
    double Weight,
    string CreatedAt,
    string? UpdatedAt)
{
    public static PriorityResponse Of(Priority priority) => new(
        priority.Id,
        priority.Label,
        priority.Weight,
        Timestamps.Iso(priority.CreatedAt),
        Timestamps.Iso(priority.UpdatedAt));
}
