using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Categories.UseCases;

public class GetAllCategoriesUseCase(ICategoryRepository repository) : IQueryHandler<IEnumerable<Category>>
{
    public async Task<IEnumerable<Category>> Execute()
    {
        var categories = await repository.GetAll();
        return categories;
    }
}