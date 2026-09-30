using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers.Api
{
    // Read-only catalog API.
    [ApiController]
    [Route("api/products")]
    public class ProductsApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductsApiController(AppDbContext db) => _db = db;

        public record ProductDto(int ProductCode, ProductType Type, string Name, string? AuthorName, string Category,
            decimal Price, int? Stock, string? Description, string? ImageUrl);

        [HttpGet]
        public async Task<IEnumerable<ProductDto>> List(string? q, ProductType? type, string? category)
        {
            var query = _db.Products.AsNoTracking().Listed().Search(q);
            if (type.HasValue)
            {
                query = query.Where(p => p.Type == type.Value);
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var products = await query.OrderBy(p => p.ProductCode).ToListAsync();
            return products.Select(ToDto);
        }

        [HttpGet("{code:int}")]
        public async Task<ActionResult<ProductDto>> Get(int code)
        {
            var product = await _db.Products.AsNoTracking().Listed().FirstOrDefaultAsync(p => p.ProductCode == code);
            return product == null ? NotFound() : ToDto(product);
        }

        private ProductDto ToDto(Product p) => new(p.ProductCode, p.Type, p.Name, p.AuthorName, p.Category, p.Price,
            p.IsPhysical ? p.Stock : null, p.Description,
            p.ImagePath == null ? null : Url.Action("Image", "Media", new { id = p.ProductCode }, Request.Scheme));
    }
}
