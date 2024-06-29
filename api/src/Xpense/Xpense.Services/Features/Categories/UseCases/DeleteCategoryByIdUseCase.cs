using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Categories.UseCases;

public class DeleteCategoryByIdUseCase(ICategoryRepository repository) : ICommandHandler<int>
{
    public async Task Handle(int id)
    {
        await repository.DeleteById(id);
        var result = await repository.SaveChanges();
        if (result < 1)
            throw new CategoryDeletionFailedException(id);
    }
}