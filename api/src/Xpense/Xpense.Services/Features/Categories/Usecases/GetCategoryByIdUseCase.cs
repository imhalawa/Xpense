using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Categories.UseCases;

public class GetCategoryByIdUseCase(ICategoryRepository repository) : IQueryParamHandler<int, Category>
{
    public async Task<Category> Execute(int id, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetById(id);
        if (category == null)
            throw new CategoryNotFoundException(id);
        return category;
    }
}