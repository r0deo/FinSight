using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = "Owner,Manager"), Route("simulations/preview")]
public sealed class SimulationPagesController(ISender sender) : WebsiteController
{
    [HttpGet]
    public async Task<IActionResult> Preview(Guid? productId, CancellationToken cancellationToken)
    {
        if (productId is null)
        {
            TempData["Success"] = "Choose an active product and select Preview price change. No simulation is saved.";
            return LocalRedirect("/products");
        }
        var product = await sender.Send(new GetProductByIdQuery(productId.Value), cancellationToken);
        return View(new PreviewSimulationViewModel
        { ProductId = product.Id, ProductName = product.Name, ProposedPrice = product.SellingPrice });
    }

    [HttpPost]
    public async Task<IActionResult> Preview(PreviewSimulationViewModel model, CancellationToken cancellationToken)
    {
        // Never render submitted product names/results; retrieve the authenticated tenant's product.
        var product = await sender.Send(new GetProductByIdQuery(model.ProductId), cancellationToken);
        model.ProductName = product.Name;
        model.Result = null;
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            model.Result = await sender.Send(new PreviewPricingSimulationQuery(model.ProductId,
                model.ProposedPrice, model.CurrentSalesQuantity, model.ExpectedSalesQuantity), cancellationToken);
            return View(model);
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Preview", model);
        }
    }
}
