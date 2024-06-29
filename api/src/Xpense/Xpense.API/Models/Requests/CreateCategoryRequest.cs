using Xpense.Services.Enums;
using Xpense.Services.Features.Categories.Commands;

namespace Xpense.API.Models.Requests;

public class CreateCategoryRequest(string name, Priority priority)
{
    public string Name { get; } = name;
    public Priority Priority { get; } = priority;

    public CreateCategoryCommand ToCommand() => new(Name, Priority);
}