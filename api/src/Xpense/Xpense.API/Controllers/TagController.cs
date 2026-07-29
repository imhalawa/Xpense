using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Tags.UseCases;

namespace Xpense.API.Controllers;

[Route("api/v1/tags")]
[ApiController]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public class TagController(
    GetAllTagsUseCase getAllTagsUseCase,
    GetTagByIdUseCase getTagByIdUseCase,
    UpdateTagUseCase updateTagUseCase,
    CreateTagUseCase createTagUseCase,
    DeleteTagUseCase deleteTagUseCase
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TagResponse[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var tags = await getAllTagsUseCase.Execute();
        return Ok(tags.Select(TagResponse.Of));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var tag = await getTagByIdUseCase.Execute(id);
        return Ok(TagResponse.Of(tag));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        await deleteTagUseCase.Handle(id);
        return NoContent();
    }

    [HttpPost]
    [ProducesResponseType<TagResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        request.BgColorHex = TrimHashSign(request.BgColorHex);
        request.FgColorHex = TrimHashSign(request.FgColorHex);

        var tag = await createTagUseCase.Handle(request.ToCommand());
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, TagResponse.Of(tag));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTagRequest request)
    {
        request.Id = id;
        request.BgColorHex = TrimHashSign(request.BgColorHex);
        request.FgColorHex = TrimHashSign(request.FgColorHex);

        var tag = await updateTagUseCase.Handle(request.ToCommand());
        return Ok(TagResponse.Of(tag));
    }

    private static string TrimHashSign(string hashStr) => hashStr?.TrimStart('#');
}
