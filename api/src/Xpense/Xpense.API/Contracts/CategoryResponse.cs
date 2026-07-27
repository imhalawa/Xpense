using System;
using Xpense.Domain.Entities;

namespace Xpense.API.Contracts;

public sealed record CategoryResponse(
    int Id,
    string Label,
    PriorityResponse Priority,
    long? CreatedOn,
    long? LastUpdated)
{
    public static CategoryResponse Of(Category category) => new(
        category.Id,
        category.Label,
        PriorityResponse.Of(category.Priority),
        new DateTimeOffset(category.CreatedOn).ToUnixTimeSeconds(),
        category.LastUpdated.HasValue
            ? new DateTimeOffset(category.LastUpdated.Value).ToUnixTimeSeconds()
            : null);
}

public sealed record PriorityResponse(
    int Id,
    string Label,
    double Weight,
    long? CreatedOn,
    long? LastUpdated)
{
    public static PriorityResponse Of(Priority priority) => new(
        priority.Id,
        priority.Label,
        priority.Weight,
        new DateTimeOffset(priority.CreatedOn).ToUnixTimeSeconds(),
        priority.LastUpdated.HasValue
            ? new DateTimeOffset(priority.LastUpdated.Value).ToUnixTimeSeconds()
            : null);
}
