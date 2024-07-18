namespace Xpense.Services.Features.Categories.Commands;

public class CreateCategoryCommand(string name, int priorityId)
{
    public string Name { get; set; } = name;
    public int PriorityId { get; set; } = priorityId;
}