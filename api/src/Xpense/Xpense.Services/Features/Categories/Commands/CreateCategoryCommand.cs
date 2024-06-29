using Xpense.Services.Enums;

namespace Xpense.Services.Features.Categories.Commands;

public class CreateCategoryCommand(string name, Priority priority)
{
    public string Name { get; set; } = name;
    public Priority Priority { get; set; } = priority;
}