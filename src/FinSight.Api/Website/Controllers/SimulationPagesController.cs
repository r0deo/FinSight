using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Commands.CreateDecisionSimulation;
using FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingDecision;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Domain.Entities;
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
        if (!product.IsActive) throw new NotFoundException(nameof(Product), productId.Value);
        var currency = await sender.Send(new GetBusinessCurrencyQuery(), cancellationToken);
        return View(new PreviewSimulationViewModel
        {
            ProductId = product.Id, ProductName = product.Name, ProposedPrice = product.SellingPrice,
            CurrentPrice = product.SellingPrice, UnitCost = product.UnitCost, Currency = currency,
            UnknownExpectedSales = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Preview(PreviewSimulationViewModel model, CancellationToken cancellationToken)
    {
        // Always re-read product and business data; submitted display fields cannot override tenant snapshots.
        var product = await sender.Send(new GetProductByIdQuery(model.ProductId), cancellationToken);
        if (!product.IsActive) throw new NotFoundException(nameof(Product), model.ProductId);
        model.ProductName = product.Name;
        model.CurrentPrice = product.SellingPrice;
        model.UnitCost = product.UnitCost;
        model.Currency = await sender.Send(new GetBusinessCurrencyQuery(), cancellationToken);
        model.Result = null;
        if (model.UnknownExpectedSales)
        {
            model.ExpectedSalesQuantity = null;
            ModelState.Remove(nameof(model.ExpectedSalesQuantity));
        }
        else if (!model.ExpectedSalesQuantity.HasValue)
            ModelState.AddModelError(nameof(model.ExpectedSalesQuantity),
                Text("Enter expected sales or choose I don't know expected sales."));
        if (ModelState.TryGetValue(nameof(model.ComparisonPeriod), out var periodState)
            && periodState.Errors.Count > 0)
        {
            ModelState.Remove(nameof(model.ComparisonPeriod));
            ModelState.AddModelError(nameof(model.ComparisonPeriod), Text("Choose a valid comparison period."));
        }
        else if (!Enum.IsDefined(model.ComparisonPeriod))
            ModelState.AddModelError(nameof(model.ComparisonPeriod), Text("Choose a valid comparison period."));
        if (!model.DeclinePercentage.HasValue)
            ModelState.AddModelError(nameof(model.DeclinePercentage), Text("Enter an illustrative decline from 0 to 100%."));
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            var preview = await sender.Send(new PreviewPricingDecisionQuery(model.ProductId,
                model.ProposedPrice, model.CurrentSalesQuantity, model.ExpectedSalesQuantity,
                model.DeclinePercentage!.Value), cancellationToken);
            model.Result = preview.Decision;
            if (model.ComparisonPeriod == FinSight.Domain.Enums.SalesPeriod.Month && !string.IsNullOrEmpty(model.ContextMonth))
            {
                if (!BusinessCostForm.TryMonth(model.ContextMonth, out var month))
                {
                    ModelState.AddModelError(nameof(model.ContextMonth), Text("Choose a month between 2000 and 2100."));
                    return InvalidForm("Preview", model);
                }
                model.CostContext = new(await sender.Send(new FinSight.Application.Features.BusinessCosts.GetBusinessCostsQuery(month), cancellationToken), model.Currency);
            }
            return View(model);
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Preview", model);
        }
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(SaveDecisionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var saved = await sender.Send(new CreateDecisionSimulationCommand(model.ProductId,
            model.ProposedPrice, model.CurrentSalesQuantity, model.ExpectedSalesQuantity,
            model.ComparisonPeriod, model.DeclinePercentage, model.PreviewCurrentPrice,
            model.PreviewUnitCost), cancellationToken);
        TempData["Success"] = "Pricing simulation saved.";
        return LocalRedirect($"/simulations/{saved.Id}");
    }
}
