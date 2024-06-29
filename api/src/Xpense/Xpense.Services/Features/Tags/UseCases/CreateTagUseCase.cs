using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Tags.Commands;

namespace Xpense.Services.Features.Tags.UseCases;

public class CreateTagUseCase(ITagRepository repository) : ICommandResultHandler<CreateTagCommand, Tag>
{
    public async Task<Tag> Handle(CreateTagCommand command)
    {
        var tag = command.ToEntity();
        repository.Create(tag);
        var result = await repository.SaveChanges();
        if (result < 1)
            throw new TagCreationFailedException(tag.Name);
        return tag;
    }
}