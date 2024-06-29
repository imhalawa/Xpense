using Xpense.Services.Enums;
using Xpense.Services.Features.Categories.Commands;

namespace Xpense.API.Models.Requests;

public class UpdateCategoryRequest(int id, string name, Priority priority)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public Priority Priority { get; set; } = priority;

    public UpdateCategoryCommand ToCommand() => new UpdateCategoryCommand(Id, Name, Priority);
}