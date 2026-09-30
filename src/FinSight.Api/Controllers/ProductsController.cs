using FinSight.Api.Contracts.Products;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Features.Products.Commands.CreateProduct;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{AppRoles.Owner},{AppRoles.Manager}")]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProductCommand(
            request.Name,
            request.Category,
            request.Size,
            request.SellingPrice,
            request.UnitCost), cancellationToken);
        return Created($"/api/products/{result.Id}", result);
    }
}
