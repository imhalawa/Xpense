using Xpense.Services.Features.Categories.Commands;

namespace Xpense.API.Models.Requests;

public class CreateCategoryRequest(string name, int priorityId)
{
    public string Name { get; } = name;
    public int Priority { get; } = priorityId;

    public CreateCategoryCommand ToCommand() => new(Name, priorityId);
}