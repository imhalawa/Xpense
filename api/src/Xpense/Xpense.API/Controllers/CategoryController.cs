using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Linq;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Categories.UseCases;

namespace Xpense.API.Controllers
{
    [Route("api/category")]
    [ApiController]
    public class CategoryController(
        CreateCategoryUseCase createCategory,
        GetAllCategoriesUseCase getAllCategoriesUseCase,
        GetCategoryByIdUseCase getCategoryByIdUseCase,
        DeleteCategoryByIdUseCase deleteCategoryByIdUseCase,
        UpdateCategoryUseCase updateCategoryUseCase,
        ILogger logger
    ) : XpenseController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var categories = await getAllCategoriesUseCase.Execute();
            return Ok(categories.Select(CategoryResponse.Of));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            try
            {
                var result = await getCategoryByIdUseCase.Execute(id);
                return Ok(result);
            }
            catch (CategoryNotFoundException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteById(int id)
        {
            try
            {
                await deleteCategoryByIdUseCase.Handle(id);
                return Ok("CategoryId Deleted Successfully!");
            }
            catch (CategoryDeletionFailedException exception)
            {
                logger.Error(exception, exception.Message);
                return Problem(exception);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var category = await createCategory.Handle(request.ToCommand());
                return Ok(CreateCategoryResponse.Of(category));
            }
            catch (CategoryCreationFailedException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateCategoryRequest request)
        {
            try
            {
                var category = await updateCategoryUseCase.Handle(request.ToCommand());
                return Ok(CategoryResponse.Of(category));
            }
            catch (CategoryNotFoundException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
            catch (CategoryUpdateFailedException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
        }
    }
}