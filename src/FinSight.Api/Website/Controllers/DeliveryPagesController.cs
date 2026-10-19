using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Delivery;
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
public sealed class DeliveryPagesController(ISender sender) : WebsiteController
{
    [HttpGet("/simulations/delivery")]
    public async Task<IActionResult> Preview(Guid? productId, int page = 1, CancellationToken cancellationToken = default)
    {
        if (productId is null)
            return View("~/Views/Shared/DecisionProductPicker.cshtml", new DecisionProductPickerViewModel("delivery", await sender.Send(new GetProductsQuery(page), cancellationToken)));
        var model = new DeliveryViewModel { ProductId = productId.Value, UnitsPerOrder = 1 };
        await Load(model, null, cancellationToken);
        return View("Preview", model);
    }

    [HttpGet("/simulations/{id:guid}/delivery/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var saved = (await sender.Send(new GetSavedDecisionQuery(id), cancellationToken)).Simulation;
        if (saved.DeliverySnapshot is not { } snapshot) throw new NotFoundException(nameof(PricingSimulation), id);
        var input = snapshot.Inputs;
        var model = new DeliveryViewModel { UnitsPerOrder = input.UnitsPerOrder, DeliveryUnitPrice = input.DeliveryUnitPrice,
            CommissionPercentage = input.CommissionPercentage, PackagingPerOrder = input.PackagingPerOrder,
            OtherChargesPerOrder = input.OtherChargesPerOrder, FixedPeriodCost = input.FixedPeriodCost,
            ComparisonPeriod = input.ComparisonPeriod, CurrentDirectOrders = input.CurrentDirectOrders,
            ExpectedDeliveryOrders = input.ExpectedDeliveryOrders, TransferredOrders = input.TransferredOrders, Version = saved.Version };
        await Load(model, id, cancellationToken);
        return View("Preview", model);
    }

    [HttpPost("/simulations/delivery")]
    [HttpPost("/simulations/{id:guid}/delivery/edit")]
    public async Task<IActionResult> Calculate(DeliveryViewModel model, Guid? id, CancellationToken cancellationToken)
    {
        await Load(model, id, cancellationToken);
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            model.Result = id.HasValue
                ? await sender.Send(new PreviewSavedDeliveryQuery(id.Value, model.Inputs(), model.Version), cancellationToken)
                : await sender.Send(new PreviewDeliveryQuery(model.ProductId, model.Inputs()), cancellationToken);
            return View("Preview", model);
        }
        catch (ValidationException exception) { AddValidationErrors(exception); return InvalidForm("Preview", model); }
    }

    [HttpPost("/simulations/delivery/save")]
    [HttpPost("/simulations/{id:guid}/delivery/save")]
    public async Task<IActionResult> Save(DeliveryViewModel model, Guid? id, CancellationToken cancellationToken)
    {
        await Load(model, id, cancellationToken);
        if (!id.HasValue && (!model.PreviewCurrentPrice.HasValue || !model.PreviewUnitCost.HasValue))
            ModelState.AddModelError("", Text("Recalculate the preview, then save it again."));
        if (!ModelState.IsValid) return InvalidForm("Preview", model);
        try
        {
            if (id.HasValue) await sender.Send(new UpdateDeliveryCommand(id.Value, model.Inputs(), model.Version), cancellationToken);
            else
            {
                var saved = await sender.Send(new CreateDeliveryCommand(model.ProductId, model.Inputs(),
                    model.PreviewCurrentPrice!.Value, model.PreviewUnitCost!.Value), cancellationToken);
                id = saved.Id;
            }
            TempData["Success"] = "Simulation saved.";
            return LocalRedirect($"/simulations/{id}");
        }
        catch (ValidationException exception) { AddValidationErrors(exception); return InvalidForm("Preview", model); }
    }

    private async Task Load(DeliveryViewModel model, Guid? id, CancellationToken ct)
    {
        model.Result = null; model.SimulationId = id;
        if (id.HasValue)
        {
            var saved = (await sender.Send(new GetSavedDecisionQuery(id.Value), ct)).Simulation;
            if (saved.DeliverySnapshot is null) throw new NotFoundException(nameof(PricingSimulation), id.Value);
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
