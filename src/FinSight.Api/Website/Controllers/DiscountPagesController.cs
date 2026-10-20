using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Discounts;
using FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;
using FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Domain.Entities;
using FinSight.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = "Owner,Manager")]
public sealed class DiscountPagesController(ISender sender) : WebsiteController
{
    [HttpGet("/simulations/discount")]
    public async Task<IActionResult> Preview(Guid? productId, int page = 1, CancellationToken cancellationToken = default)
    {
        if (productId is null)
            return View("~/Views/Shared/DecisionProductPicker.cshtml", new DecisionProductPickerViewModel("discount", await sender.Send(new GetProductsQuery(page), cancellationToken)));
        var model = new DiscountViewModel { ProductId = productId.Value };
        await Load(model, null, cancellationToken);
        return View("Preview", model);
    }

    [HttpGet("/simulations/{id:guid}/discount/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = new DiscountViewModel();
        var saved = await sender.Send(new GetSavedDecisionQuery(id), cancellationToken);
        if (saved.Simulation.DiscountSnapshot is not { } snapshot) throw new NotFoundException(nameof(PricingSimulation), id);
        model.DiscountPercentage = snapshot.Inputs.DiscountPercentage;
        model.CurrentSalesQuantity = snapshot.Inputs.CurrentSalesQuantity;
        model.ExpectedSalesQuantity = snapshot.Inputs.ExpectedSalesQuantity;
        model.CampaignCost = snapshot.Inputs.CampaignCost;
        model.ComparisonPeriod = saved.Simulation.ComparisonPeriod;
        model.Version = saved.Simulation.Version;
        model.RestoreCostSettings(saved.Simulation.CostContext);
        await Load(model, id, cancellationToken);
        NormalizeCostLink(model);
        return View("Preview", model);
    }

    [HttpPost("/simulations/discount")]
    [HttpPost("/simulations/{id:guid}/discount/edit")]
    public async Task<IActionResult> Calculate(DiscountViewModel model, Guid? id, CancellationToken cancellationToken)
    {
        await Load(model, id, cancellationToken);
        NormalizeCostLink(model);
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            model.Result = id.HasValue
                ? await sender.Send(new PreviewSavedDiscountQuery(id.Value, model.DiscountPercentage!.Value,
                    model.CurrentSalesQuantity!.Value, model.ExpectedSalesQuantity, model.CampaignCost,
                    model.ComparisonPeriod!.Value, model.Version), cancellationToken)
                : await sender.Send(new PreviewDiscountQuery(model.ProductId, model.DiscountPercentage!.Value,
                    model.CurrentSalesQuantity!.Value, model.ExpectedSalesQuantity, model.CampaignCost,
                    model.ComparisonPeriod!.Value), cancellationToken);
            model.CostContext = await sender.Send(new FinSight.Application.Features.BusinessCosts.PreviewCostContextQuery(
                model.CostOptions(), model.ComparisonPeriod!.Value, model.Result.Result.CurrentContribution,
                [new FinSight.Domain.Models.ContributionOption("Proposed contribution", model.Result.Result.ScenarioContribution)]), cancellationToken);
            return View("Preview", model);
        }
        catch (ValidationException exception) { model.Result = null; model.CostContext = null; AddValidationErrors(exception); return InvalidForm("Preview", model); }
    }

    [HttpPost("/simulations/discount/save")]
    [HttpPost("/simulations/{id:guid}/discount/save")]
    public async Task<IActionResult> Save(DiscountViewModel model, Guid? id, CancellationToken cancellationToken)
    {
        await Load(model, id, cancellationToken);
        NormalizeCostLink(model);
        if (!id.HasValue && (!model.PreviewCurrentPrice.HasValue || !model.PreviewUnitCost.HasValue))
            ModelState.AddModelError("", Text("Recalculate the preview, then save it again."));
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            if (id.HasValue)
                await sender.Send(new UpdateDiscountCommand(id.Value, model.DiscountPercentage!.Value,
                    model.CurrentSalesQuantity!.Value, model.ExpectedSalesQuantity, model.CampaignCost,
                    model.ComparisonPeriod!.Value, model.Version) { CostLink = model.CostOptions(), CostBasis = model.CostBasis }, cancellationToken);
            else
            {
                var saved = await sender.Send(new CreateDiscountCommand(model.ProductId, model.DiscountPercentage!.Value,
                    model.CurrentSalesQuantity!.Value, model.ExpectedSalesQuantity, model.CampaignCost,
                    model.ComparisonPeriod!.Value, model.PreviewCurrentPrice!.Value, model.PreviewUnitCost!.Value) { CostLink = model.CostOptions(), CostBasis = model.CostBasis }, cancellationToken);
                id = saved.Id;
            }
            TempData["Success"] = "Simulation saved.";
            return LocalRedirect($"/simulations/{id}");
        }
        catch (ValidationException exception) { AddValidationErrors(exception); return InvalidForm("Preview", model); }
    }

    private async Task Load(DiscountViewModel model, Guid? id, CancellationToken ct)
    {
        model.Result = null; model.SimulationId = id;
        if (id.HasValue)
        {
            var saved = (await sender.Send(new GetSavedDecisionQuery(id.Value), ct)).Simulation;
            if (saved.DiscountSnapshot is null) throw new NotFoundException(nameof(PricingSimulation), id.Value);
            if (saved.IsArchived) throw new PricingSimulationConflictException("An archived pricing simulation cannot be edited.");
            model.ProductId = saved.ProductId; model.ProductName = saved.ProductNameSnapshot!;
            model.CurrentPrice = saved.CurrentPrice; model.UnitCost = saved.UnitCost; model.Currency = saved.CurrencySnapshot!;
        }
        else
        {
            var product = await sender.Send(new GetProductByIdQuery(model.ProductId), ct);
            if (!product.IsActive) throw new NotFoundException(nameof(Product), model.ProductId);
            model.ProductName = product.Name; model.CurrentPrice = product.SellingPrice; model.UnitCost = product.UnitCost;
            model.Currency = await sender.Send(new GetBusinessCurrencyQuery(), ct);
        }
    }
}
