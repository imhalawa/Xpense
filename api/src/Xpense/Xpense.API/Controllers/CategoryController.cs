using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Categories.UseCases;

namespace Xpense.API.Controllers
{
    [Route("api/v1/categories")]
    [ApiController]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public class CategoryController(
        CreateCategoryUseCase createCategory,
        GetAllCategoriesUseCase getAllCategoriesUseCase,
        GetCategoryByIdUseCase getCategoryByIdUseCase,
        DeleteCategoryByIdUseCase deleteCategoryByIdUseCase,
        UpdateCategoryUseCase updateCategoryUseCase
    ) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<CategoryResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var categories = await getAllCategoriesUseCase.Execute();
            return Ok(categories.Select(CategoryResponse.Of));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var result = await getCategoryByIdUseCase.Execute(id);
            return Ok(CategoryResponse.Of(result));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteById(int id)
        {
            await deleteCategoryByIdUseCase.Handle(id);
            return NoContent();
        }

        [HttpPost]
        [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
        {
            var category = await createCategory.Handle(request.ToCommand());
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, CategoryResponse.Of(category));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request)
        {
            request.Id = id;
            var category = await updateCategoryUseCase.Handle(request.ToCommand());
            return Ok(CategoryResponse.Of(category));
        }
    }
}
