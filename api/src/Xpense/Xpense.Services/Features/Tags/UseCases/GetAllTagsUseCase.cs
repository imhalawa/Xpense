using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Tags.UseCases;

public class GetAllTagsUseCase(ITagRepository repository): IQueryHandler<IEnumerable<Tag>>
{
    public async Task<IEnumerable<Tag>> Execute()
    {
        return await repository.GetAll();
    }
}