using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Tags.UseCases;

public class GetTagByIdUseCase(ITagRepository repository) : IQueryParamHandler<int, Tag>
{
    public async Task<Tag> Execute(int accountNumber, CancellationToken cancellationToken = default)
    {
        var tag = await repository.GetById(accountNumber);
        if (tag == null)
            throw new TagNotFoundException(accountNumber);
        return tag;
    }
}