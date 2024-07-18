using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Categories.Commands;

namespace Xpense.Services.Features.Categories.UseCases;

public class UpdateCategoryUseCase(ICategoryRepository repository, IPriorityRepository priorityRepository) : ICommandResultHandler<UpdateCategoryCommand, Category>
{
    public async Task<Category> Handle(UpdateCategoryCommand command)
    {
        var priority = await priorityRepository.GetById(command.PriorityId);

        if (priority == null)
            throw new PriorityNotFoundException(command.PriorityId);

        var category = await repository.GetById(command.Id);

        if (category == null)
            throw new CategoryNotFoundException(command.Id);

        category.Label = command.Name;
        category.Priority = priority;

        repository.Update(category);

        var result = await repository.SaveChanges();
        if (result < 1)
            throw new CategoryUpdateFailedException(command.Id);

        return category;
    }
}