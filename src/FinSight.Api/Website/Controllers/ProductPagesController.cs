using FinSight.Api.Website.Models;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.Products.Commands.CreateProduct;
using FinSight.Application.Features.Products.Commands.UpdateProduct;
using FinSight.Application.Features.Products.Commands.DeactivateProduct;
using FinSight.Application.Features.Products.Commands.ReactivateProduct;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = $"{AppRoles.Owner},{AppRoles.Manager}")]
[Route("products")]
public sealed class ProductPagesController(ISender sender) : WebsiteController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await sender.Send(new GetProductsQuery(page, pageSize, includeInactive), cancellationToken);
        return View(new ProductListViewModel(result, includeInactive));
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new ProductFormViewModel());

    [HttpPost("create")]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return InvalidForm("Form", model);
        try
        {
            await sender.Send(new CreateProductCommand(model.Name, model.Category, model.Size,
                model.SellingPrice!.Value, model.UnitCost!.Value), cancellationToken);
            TempData["Success"] = "Product added. You're ready to manage its price and cost.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Form", model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new GetProductByIdQuery(id), cancellationToken);
        ViewData["ProductId"] = id;
        return View("Form", new ProductFormViewModel
        {
            Name = product.Name, Category = product.Category, Size = product.Size,
            SellingPrice = product.SellingPrice, UnitCost = product.UnitCost
        });
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["ProductId"] = id;
        if (!ModelState.IsValid) return InvalidForm("Form", model);
        try
        {
            await sender.Send(new UpdateProductCommand(id, model.Name, model.Category, model.Size,
                model.SellingPrice!.Value, model.UnitCost!.Value), cancellationToken);
            TempData["Success"] = "Product updated.";
            return RedirectToAction(nameof(Index), new { includeInactive = true });
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Form", model);
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateProductCommand(id), cancellationToken);
        TempData["Success"] = "Product deactivated. It remains in your history and can be reactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ReactivateProductCommand(id), cancellationToken);
        TempData["Success"] = "Product reactivated.";
        return RedirectToAction(nameof(Index));
    }
}
