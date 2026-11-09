using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Utcp.Sample.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    [HttpGet("{id:int}", Name = "GetProduct")]
    [EndpointSummary("Get a product by id")]
    [Tags("Products")]
    [ProducesResponseType<Product>(StatusCodes.Status200OK)]
    public IActionResult GetProduct(int id, [FromQuery] bool includePricing = false) =>
        Ok(new Product(id, $"Product {id}", includePricing ? 19.99m : null));
}

public record Product(int Id, string Name, decimal? Price);
