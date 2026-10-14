using FinSight.Api.Website.Models;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = "Owner,Manager"), Route("business-costs")]
public sealed class BusinessCostPagesController(ISender sender, TimeProvider clock) : WebsiteController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? month, CancellationToken ct)
    {
        month ??= clock.GetUtcNow().ToString("yyyy-MM");
        if (!BusinessCostForm.TryMonth(month, out var selected)) return BadRequest();
        return View(await Workspace(selected, ct));
    }
    [HttpPost("review"), Authorize(Roles = "Owner")]
    public async Task<IActionResult> Review(string month, long version, bool reviewed, CancellationToken ct)
    {
        if (!ModelState.IsValid || !BusinessCostForm.TryMonth(month, out var selected)) return BadRequest();
        await sender.Send(new ReviewBusinessCostMonthCommand(selected, version, reviewed), ct);
        TempData["Success"] = reviewed ? "Month marked as reviewed." : "Month reopened for review.";
        return RedirectToAction(nameof(Index), new { month });
    }
    [HttpPost("adjustment")]
    public async Task<IActionResult> Adjustment(CostAdjustmentForm form, CancellationToken ct)
    {
        if (!BusinessCostForm.TryMonth(form.Month, out var selected)) return BadRequest();
        var workspace = await Workspace(selected, ct);
        form.Categories = workspace.AdjustmentForm.Categories;
        var query = new PreviewCostAdjustmentQuery(selected, form.Category, form.Percent ?? -1);
        if (!ModelState.IsValid || !(await new PreviewCostAdjustmentValidator().ValidateAsync(query, ct)).IsValid
            || !form.Categories.Contains(form.Category))
        {
            ModelState.Clear();
            ModelState.AddModelError("", Text("Choose a recorded operating category and a reduction from 0 to 100, with up to two decimals."));
            return InvalidForm("Index", workspace with { AdjustmentForm = form });
        }
        var adjustment = await sender.Send(query, ct);
        return View("Index", workspace with { Summary = adjustment.Source, AdjustmentForm = form, Adjustment = adjustment });
    }
    private async Task<BusinessCostsViewModel> Workspace(DateOnly month, CancellationToken ct)
    {
        var summary = await sender.Send(new GetBusinessCostsQuery(month), ct);
        return new(summary, await sender.Send(new GetBusinessCurrencyQuery(), ct))
        {
            Previous = month > new DateOnly(2000, 1, 1) ? await sender.Send(new GetBusinessCostsQuery(month.AddMonths(-1)), ct) : null,
            AdjustmentForm = new() { Month = month.ToString("yyyy-MM"), Categories = summary.OperatingCategories.Select(c => c.Category).ToArray(),
                Category = summary.OperatingCategories.FirstOrDefault()?.Category ?? FinSight.Domain.Enums.BusinessCostCategory.Rent }
        };
    }
    [HttpGet("create")]
    public IActionResult Create(string? month)
    {
        month ??= clock.GetUtcNow().ToString("yyyy-MM");
        if (!BusinessCostForm.TryMonth(month, out _)) return BadRequest();
        return View("Form", new BusinessCostForm { Month = month });
    }
    [HttpPost("create")]
    public async Task<IActionResult> Create(BusinessCostForm model, CancellationToken ct)
    {
        if (!await Validate(model)) return InvalidForm("Form", model);
        await sender.Send(new CreateBusinessCostCommand(model.Details()), ct);
        TempData["Success"] = "Cost entry added.";
        return RedirectToAction(nameof(Index), new { month = model.Month });
    }
    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var c = await sender.Send(new GetBusinessCostQuery(id), ct);
        ViewData["CostId"] = id;
        return View("Form", new BusinessCostForm { Name = c.Name, Category = c.Category, Amount = c.Amount,
            Month = c.Month.ToString("yyyy-MM"), Certainty = c.Certainty, Inclusion = c.Inclusion, Note = c.Note, Version = c.Version });
    }
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, BusinessCostForm model, CancellationToken ct)
    {
        ViewData["CostId"] = id;
        if (!await Validate(model)) return InvalidForm("Form", model);
        await sender.Send(new UpdateBusinessCostCommand(id, model.Version, model.Details()), ct);
        TempData["Success"] = "Cost entry updated.";
        return RedirectToAction(nameof(Index), new { month = model.Month });
    }
    [HttpGet("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        View(await sender.Send(new GetBusinessCostQuery(id), ct));
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, long version, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var c = await sender.Send(new GetBusinessCostQuery(id), ct);
        await sender.Send(new DeleteBusinessCostCommand(id, version), ct);
        TempData["Success"] = "Cost entry deleted.";
        return RedirectToAction(nameof(Index), new { month = c.Month.ToString("yyyy-MM") });
    }
    private async Task<bool> Validate(BusinessCostForm model)
    {
        foreach (var field in new[] { nameof(model.Category), nameof(model.Certainty), nameof(model.Inclusion) })
        {
            if (ModelState.TryGetValue(field, out var state) && state.Errors.Count > 0)
            {
                ModelState.Remove(field);
                ModelState.AddModelError(field, Text("Please check the supplied values and try again."));
            }
        }
        if (!BusinessCostForm.TryMonth(model.Month, out _))
            ModelState.AddModelError(nameof(model.Month), Text("Choose a month between 2000 and 2100."));
        if (!ModelState.IsValid) return false;
        var result = await new CostDetailsValidator().ValidateAsync(model.Details());
        if (!result.IsValid) ModelState.AddModelError("", Text("Check the name, category, amount (up to two decimals), and note."));
        return ModelState.IsValid;
    }
}
