using FinSight.Api.Contracts.Products;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Features.Products.Commands.CreateProduct;
using FinSight.Application.Features.Products.Commands.DeactivateProduct;
using FinSight.Application.Features.Products.Commands.ReactivateProduct;
using FinSight.Application.Features.Products.Commands.UpdateProduct;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Application.Features.Products.Queries.GetProducts;
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
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProductCommand(
            request.Name,
            request.Category,
            request.Size,
            request.SellingPrice,
            request.UnitCost), cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ToResponse(result));
    }

    [HttpGet]
    public async Task<ActionResult<PagedProductsResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProductsQuery(page, pageSize, includeInactive),
            cancellationToken);

        return Ok(new PagedProductsResponse(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateProductCommand(
            id,
            request.Name,
            request.Category,
            request.Size,
            request.SellingPrice,
            request.UnitCost), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateProductCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReactivateProductCommand(id), cancellationToken);
        return NoContent();
    }

    private static ProductResponse ToResponse(CreateProductResponse product) => new(
        product.Id,
        product.Name,
        product.Category,
        product.Size,
        product.SellingPrice,
        product.UnitCost,
        product.IsActive);

    private static ProductResponse ToResponse(
        Application.Features.Products.ProductResponse product) => new(
        product.Id,
        product.Name,
        product.Category,
        product.Size,
        product.SellingPrice,
        product.UnitCost,
        product.IsActive);
}
