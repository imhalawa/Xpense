using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Tags.UseCases;

public class DeleteTagUseCase(ITagRepository repository) : ICommandHandler<int>
{
    public async Task Handle(int id)
    {
        var entity = await repository.GetById(id);
        if (entity == null)
            throw new TagNotFoundException(id);
        repository.Delete(entity);
        var result = await repository.SaveChanges();
        if (result < 1)
            throw new TagDeletionFailedException(id);
    }
}