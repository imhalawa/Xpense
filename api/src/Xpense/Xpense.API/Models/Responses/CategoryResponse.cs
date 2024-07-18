using System;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public class CategoryResponse(int id, string label, PriorityResponse priority, long? createdOn, long? lastUpdated)
{
    public int Id { get; set; } = id;
    public string Label { get; set; } = label;
    public PriorityResponse Priority { get; set; } = priority;
    public long? CreatedOn { get; set; } = createdOn;
    public long? LastUpdated { get; set; } = lastUpdated;

    public static CategoryResponse Of(Category category)
    {
        var priority = PriorityResponse.Of(category.Priority);
        var createdOn = new DateTimeOffset(category.CreatedOn).ToUnixTimeSeconds();
        long? lastUpdated = category.LastUpdated.HasValue
            ? new DateTimeOffset(category.LastUpdated.Value).ToUnixTimeSeconds()
            : null;
        return new CategoryResponse(category.Id, category.Label, priority, createdOn, lastUpdated);
    }
}