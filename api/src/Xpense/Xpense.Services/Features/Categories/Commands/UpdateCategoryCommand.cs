namespace Xpense.Services.Features.Categories.Commands;

public class UpdateCategoryCommand(int id, string name, int priorityId)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public int PriorityId { get; set; } = priorityId;
}