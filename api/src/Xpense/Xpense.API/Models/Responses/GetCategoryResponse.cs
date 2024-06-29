using System;
using Xpense.Services.Entities;
using Xpense.Services.Enums;

namespace Xpense.API.Models.Responses;

public class GetCategoryResponse(int id, string name, Priority priority, DateTime createdAt, DateTime? lastModifiedOn)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public Priority Priority { get; set; } = priority;
    public DateTime CreatedAt { get; set; } = createdAt;
    public DateTime? LastModifiedOn { get; set; } = lastModifiedOn;

    public static GetCategoryResponse Of(Category category) => new(category.Id, category.Name, category.Priority,
        category.CreatedOn, category.LastUpdated);
}