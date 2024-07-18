using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Categories.Commands;

namespace Xpense.Services.Features.Categories.UseCases;

public class CreateCategoryUseCase(
        ICategoryRepository repository,
        IPriorityRepository priorityRepository)
    : ICommandResultHandler<CreateCategoryCommand, Category>
{
    public async Task<Category> Handle(CreateCategoryCommand command)
    {
        var priority = await priorityRepository.GetById(command.PriorityId);

        if (priority == null)
            throw new PriorityNotFoundException(command.PriorityId);

        var category = new Category()
        {
            Label = command.Name,
            Priority = priority
        };

        repository.Create(category);
        var result = await repository.SaveChanges();

        if (result < 1)
            throw new CategoryCreationFailedException(command.Name);

        return category;
    }
}