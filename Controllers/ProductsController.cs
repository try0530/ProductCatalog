using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using ProductCatalog.DTOs;

namespace ProductCatalog.Controllers
{

    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int size = 20, [FromQuery] string? category = null)
        {
            if ( page < 1 )
            {
                return BadRequest(new
                {
                    message = "Page must be greater than or equal to 1."
                });
            }

            if ( size < 1 || size > 100 )
            {
                return BadRequest(new
                {
                    message = "Size must be between 1 and 100."
                });
            }

            var query = _db.Products.AsNoTracking();

            // if there is category, query db by category too.
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => x.Category == category);
            }

            // total query items
            var totalCount = await query.CountAsync();

            var products = await query.OrderBy(x => x.Id).Skip((page - 1) * size).Take(size).Select(x => new ProductResponse
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                StockQuantity = x.StockQuantity,
                Category = x.Category
            }).ToListAsync();

            // return DTO, should not despose the db information.
            return Ok(new PagedResponse<ProductResponse>
            {
                Items = products,
                Page = page,
                Size = size,
                TotalCount = totalCount
            });
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetProduct(long id)
        {
            var product = await _db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => new ProductResponse
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                StockQuantity = x.StockQuantity,
                Category = x.Category
            }).FirstOrDefaultAsync();

            if (product is null)
            {
                return NotFound(new
                {
                    message = $"Product with id {id} was not found."
                });
            }

            return Ok(product);
        }
    }
}