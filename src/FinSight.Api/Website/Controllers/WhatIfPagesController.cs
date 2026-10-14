using FinSight.Api.Website.Models;
using FinSight.Api.Website;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Application.Features.WhatIf;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = "Owner,Manager"), Route("what-if")]
public sealed class WhatIfPagesController(ISender sender, WhatIfPreviewProofService proof) : WebsiteController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? productId, int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest();
        if (!productId.HasValue)
        {
            var products = await sender.Send(new GetProductsQuery(page, 100, false), cancellationToken);
            ViewData["ProductPage"] = products;
            return View("SelectProduct");
        }
        var model = new WhatIfFormViewModel { ProductId = productId.Value,
            CostIncreasePercent = 0, SalesDeclinePercent = 0, Concern = WhatIfConcern.Supplier };
        await Populate(model, cancellationToken);
        return View("Index", model);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(WhatIfFormViewModel model,
        CancellationToken cancellationToken)
    {
        await Populate(model, cancellationToken);
        Normalize(model);
        ValidateMode(model);
        if (!ModelState.IsValid) return InvalidForm("Index", model);
        try
        {
            var response = await sender.Send(new PreviewWhatIfQuery(model.ToRequest()), cancellationToken);
            model.ProductName = response.ProductName;
            model.Currency = response.Currency;
            model.CurrentPrice = response.Inputs.CurrentPrice;
            model.CurrentUnitCost = response.Inputs.CurrentUnitCost;
            if (model.UsePriceResponse && !model.AlternativePriceQuantity.HasValue)
            {
                model.AlternativePriceQuantity = response.Result.ShockedQuantity;
                ModelState.Remove(nameof(model.AlternativePriceQuantity));
            }
            if (model.UseSupplierResponse && !model.AlternativeSupplierQuantity.HasValue)
            {
                model.AlternativeSupplierQuantity = response.Result.ShockedQuantity;
                ModelState.Remove(nameof(model.AlternativeSupplierQuantity));
            }
            model.Result = response.Result;
            if (model.Period == SalesPeriod.Month && !string.IsNullOrEmpty(model.ContextMonth))
            {
                if (!BusinessCostForm.TryMonth(model.ContextMonth, out var month))
                {
                    ModelState.AddModelError(nameof(model.ContextMonth), Text("Choose a month between 2000 and 2100."));
                    return InvalidForm("Index", model);
                }
                model.CostContext = new(await sender.Send(new FinSight.Application.Features.BusinessCosts.GetBusinessCostsQuery(month), cancellationToken), model.Currency);
            }
            model.PreviewProof = proof.Create(model.ToRequest(), model.CurrentPrice, model.CurrentUnitCost);
            return View("Index", model);
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException
            or ArgumentOutOfRangeException or ArgumentException)
        {
            if (exception is ValidationException or RequestValidationException)
                AddValidationErrors(exception);
            else ModelState.AddModelError(string.Empty, Text("Please check the supplied values and try again."));
            return InvalidForm("Index", model);
        }
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(WhatIfFormViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        ValidateMode(model);
        if (!ModelState.IsValid) return BadRequest();
        if (!proof.Matches(model.PreviewProof, model.ToRequest(), model.PreviewPrice, model.PreviewCost))
            throw new PricingSimulationConflictException("Recalculate the preview before saving.");
        var saved = await sender.Send(new SaveWhatIfCommand(model.ToRequest(),
            model.PreviewPrice, model.PreviewCost,
            new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(model.PreviewProof!)).AsSpan(0, 16))), cancellationToken);
        TempData["Success"] = "What-if scenario saved.";
        return LocalRedirect($"/what-if/{saved.Id}");
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(int page = 1,
        CancellationToken cancellationToken = default) =>
        View(await sender.Send(new GetWhatIfHistoryQuery(page), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        View(await sender.Send(new GetWhatIfByIdQuery(id), cancellationToken));

    private async Task Populate(WhatIfFormViewModel model, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new GetProductByIdQuery(model.ProductId), cancellationToken);
        if (!product.IsActive) throw new NotFoundException(nameof(Product), model.ProductId);
        model.ProductName = product.Name;
        model.CurrentPrice = product.SellingPrice;
        model.CurrentUnitCost = product.UnitCost;
        model.Currency = await sender.Send(new GetBusinessCurrencyQuery(), cancellationToken);
    }

    private void Normalize(WhatIfFormViewModel model)
    {
        // Hidden assumptions are canonicalized on the server, not merely hidden by JavaScript.
        if (model.Concern == WhatIfConcern.Demand)
        {
            model.CostInputMode = CostShockInputMode.Percentage;
            model.CostIncreasePercent = 0;
            model.NewUnitCost = null;
            ModelState.Remove(nameof(model.CostInputMode));
            ModelState.Remove(nameof(model.CostIncreasePercent));
            ModelState.Remove(nameof(model.NewUnitCost));
        }
        if (model.Concern == WhatIfConcern.Supplier)
        {
            model.SalesDeclinePercent = 0;
            ModelState.Remove(nameof(model.SalesDeclinePercent));
        }
        if (model.CostInputMode == CostShockInputMode.Percentage)
        {
            model.NewUnitCost = null;
            ModelState.Remove(nameof(model.NewUnitCost));
        }
        else if (model.CostInputMode == CostShockInputMode.NewUnitCost)
        {
            model.CostIncreasePercent = null;
            ModelState.Remove(nameof(model.CostIncreasePercent));
        }
        if (!model.UsePriceResponse)
        {
            model.AlternativePrice = null;
            model.AlternativePriceQuantity = null;
            ModelState.Remove(nameof(model.AlternativePrice));
            ModelState.Remove(nameof(model.AlternativePriceQuantity));
        }
        if (!model.UseSupplierResponse)
        {
            model.AlternativeSupplierCost = null;
            model.AlternativeSupplierQuantity = null;
            ModelState.Remove(nameof(model.AlternativeSupplierCost));
            ModelState.Remove(nameof(model.AlternativeSupplierQuantity));
        }
        model.Result = null;
    }

    private void ValidateMode(WhatIfFormViewModel model)
    {
        if (model.CostInputMode == CostShockInputMode.Percentage && !model.CostIncreasePercent.HasValue)
            ModelState.AddModelError(nameof(model.CostIncreasePercent), Text("Enter a cost increase percentage."));
        if (model.CostInputMode == CostShockInputMode.NewUnitCost && !model.NewUnitCost.HasValue)
            ModelState.AddModelError(nameof(model.NewUnitCost), Text("Enter the resulting new unit cost."));
        if (model.UsePriceResponse && !model.AlternativePrice.HasValue)
            ModelState.AddModelError(nameof(model.AlternativePrice), Text("Enter an alternative selling price."));
        if (model.UseSupplierResponse && !model.AlternativeSupplierCost.HasValue)
            ModelState.AddModelError(nameof(model.AlternativeSupplierCost), Text("Enter an alternative total unit cost."));
        if (!Enum.IsDefined(model.Period) || !Enum.IsDefined(model.CostInputMode) || !Enum.IsDefined(model.Concern))
            ModelState.AddModelError(string.Empty, Text("Please check the supplied values and try again."));
    }
}
