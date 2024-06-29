using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Tags.Commands;

namespace Xpense.Services.Features.Tags.UseCases;

public class UpdateTagUseCase(ITagRepository repository) : ICommandResultHandler<UpdateTagCommand, Tag>
{
    public async Task<Tag> Handle(UpdateTagCommand command)
    {
        var entity = await repository.GetById(command.Id);
        entity.Name = command.Name;
        entity.BgColorHex = command.BgColorHex;
        entity.FgColorHex = command.FgColorHex;
        repository.Update(entity);
        var result = await repository.SaveChanges();
        if (result < 1)
            throw new TagUpdateFailedException(command.Id);

        return entity;
    }
}