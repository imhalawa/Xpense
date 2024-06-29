using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
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
        ILogger logger
    ) : XpenseController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var categories = await getAllCategoriesUseCase.Execute();
            return Ok(categories);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
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
                deleteCategoryByIdUseCase.Handle(id);
                return Ok("Categor Deleted Successfully!");
            }
            catch (CategoryDeletionFailedException exception)
            {
                logger.Error(exception,exception.Message);
                return Problem(exception);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCategoryRequest request)
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
        // public IActionResult<GetCategoryResponse> Update()
        public async Task<IActionResult> Update()
        {
            return Ok();
        }
    }
}