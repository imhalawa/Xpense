using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Tags.UseCases;

namespace Xpense.API.Controllers;

[Route("api/tag")]
[ApiController]
public class TagController(
    GetAllTagsUseCase getAllTagsUseCase,
    GetTagByIdUseCase getTagByIdUseCase,
    UpdateTagUseCase updateTagUseCase,
    CreateTagUseCase createTagUseCase,
    DeleteTagUseCase deleteTagUseCase,
    ILogger logger
) : XpenseController
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tags = await getAllTagsUseCase.Execute();
        return Ok(tags.Select(TagResponse.Of));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromRoute] int id)
    {
        try
        {
            var tag = await getTagByIdUseCase.Execute(id);
            return Ok(TagResponse.Of(tag));
        }
        catch (TagNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        try
        {
            await deleteTagUseCase.Handle(id);
            return Ok("Tag Deleted Successfully!");
        }
        catch (TagNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (TagDeletionFailedException exception)
        {
            logger.Warning(exception, exception.Message);
            return Problem(exception.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        try
        {
            request.BgColorHex = TrimHashSign(request.BgColorHex);
            request.FgColorHex = TrimHashSign(request.FgColorHex);

            var tag = await createTagUseCase.Handle(request.ToCommand());
            return Ok(CreateTagResponse.Of(tag));
        }
        catch (TagCreationFailedException exception)
        {
            logger.Warning(exception, exception.Message);
            return Problem(exception.Message);
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateTagRequest request)
    {
        try
        {
            request.BgColorHex = TrimHashSign(request.BgColorHex);
            request.FgColorHex = TrimHashSign(request.FgColorHex);

            var tag = await updateTagUseCase.Handle(request.ToCommand());
            return Ok(TagResponse.Of(tag));
        }
        catch (TagNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (TagUpdateFailedException exception)
        {
            logger.Warning(exception, exception.Message);
            return Problem(exception.Message);
        }
    }

    // TODO: Move to Helper functions 
    private string TrimHashSign(string hashStr)
    {
        return hashStr.TrimStart('#');
    }
}