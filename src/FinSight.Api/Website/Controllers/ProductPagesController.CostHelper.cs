using FinSight.Api.Website.Models;
using FinSight.Application.Features.Products;
using FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FinSight.Api.Website.Controllers;

public sealed partial class ProductPagesController
{
    private async Task PopulateCurrency(ProductFormViewModel model, CancellationToken ct) =>
        model.Currency = await sender.Send(new GetBusinessCurrencyQuery(), ct);

    private void IgnoreUnusedHelper(ProductFormViewModel model)
    {
        if (!model.UseCostHelper)
            foreach (var key in ModelState.Keys.Where(x => x.StartsWith("CostHelper.", StringComparison.Ordinal)).ToArray()) ModelState.Remove(key);
        else LocalizeUnitErrors();
    }
    private void LocalizeUnitErrors()
    {
        foreach (var entry in ModelState.Where(x => x.Key.EndsWith(".PackUnit", StringComparison.Ordinal) || x.Key.EndsWith(".UsedUnit", StringComparison.Ordinal)))
            if (entry.Value?.Errors.Count > 0) { entry.Value.Errors.Clear(); ModelState.AddModelError(entry.Key, Text("Choose a supported ingredient unit.")); }
    }
    private void AddCostErrors(Exception exception)
    {
        // Domain supplies business-rule explanations; translation belongs only in the website.
        if (exception is ValidationException validation && validation.Errors.Any())
            foreach (var error in validation.Errors)
            {
                if (error.PropertyName.StartsWith("CostInputs", StringComparison.Ordinal) || error.PropertyName.StartsWith("Inputs", StringComparison.Ordinal))
                    ModelState.AddModelError(CostField(error.PropertyName), CostErrorText(error.ErrorMessage));
                else AddValidationErrors(new ValidationException([error]));
            }
        else if (exception is ProductCostValidationException cost)
            ModelState.AddModelError(string.IsNullOrEmpty(cost.FieldPath) ? "" : "CostHelper." + cost.FieldPath, CostErrorText(cost.Message));
        else if (exception is ValidationException) ModelState.AddModelError("", CostErrorText(exception.Message));
        else AddValidationErrors(exception);
    }
    private string CostErrorText(string? message) => Text(string.IsNullOrWhiteSpace(message)
        ? "Please check the supplied values and try again." : message);
    private static string CostField(string path)
    {
        foreach (var prefix in new[] { "CostInputs", "Inputs" })
            if (path == prefix || path.StartsWith(prefix + ".", StringComparison.Ordinal))
                return "CostHelper" + path[prefix.Length..];
        return "";
    }
    private static string CostFingerprint(FinSight.Domain.Models.ProductCostInputs inputs) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(inputs)));
    private async Task<IActionResult> HandleCostAction(ProductFormViewModel model, string action, CancellationToken ct, ProductResponse? existing = null)
    {
        // Helper actions do not require completed product details and never invoke a save handler.
        foreach (var entry in ModelState.Where(x => x.Key != "CostHelper" && !x.Key.StartsWith("CostHelper.", StringComparison.Ordinal)))
        {
            // Clearing messages alone leaves ValidationState.Invalid and blocks a valid estimate.
            // Skip only unrelated fields for this request; retain their raw values for redisplay.
            entry.Value!.Errors.Clear();
            entry.Value.ValidationState = ModelValidationState.Skipped;
        }
        model.HelperOpen = true;
        ModelState.Remove(nameof(model.HelperOpen));
        LocalizeUnitErrors();
        if (action is "cancel" or "manual")
        {
            var restoreExisting = action == "cancel" && existing?.CostBreakdown is not null &&
                (model.UnitCost == existing.UnitCost || (model.UseCostHelper && model.UnitCostBeforeHelper == existing.UnitCost));
            if (action == "cancel" && model.UseCostHelper)
            { model.UnitCost = model.UnitCostBeforeHelper; ModelState.Remove(nameof(model.UnitCost)); }
            model.UseCostHelper = false; model.ClearCostBreakdown = true; model.HelperOpen = false;
            model.CostHelper = restoreExisting ? ProductCostForm.From(existing!.CostBreakdown!.Inputs) : new();
            model.CostPreview = restoreExisting ? existing!.CostBreakdown : null;
            model.CostPreviewFingerprint = restoreExisting ? CostFingerprint(existing!.CostBreakdown!.Inputs) : null;
            if (restoreExisting) { model.UseCostHelper = true; model.ClearCostBreakdown = false; }
            foreach (var key in ModelState.Keys.Where(x => x.StartsWith("CostHelper.", StringComparison.Ordinal)).ToArray()) ModelState.Remove(key);
            ModelState.Remove(nameof(model.UseCostHelper)); ModelState.Remove(nameof(model.ClearCostBreakdown));
            ModelState.Remove(nameof(model.CostPreviewFingerprint));
            return View("Form", model);
        }
        if (action == "add")
        {
            if (model.CostHelper.Ingredients.Count >= ProductCostCalculator.MaximumIngredients)
                ModelState.AddModelError("", Text("Enter between 1 and 30 ingredients."));
            else model.CostHelper.Ingredients.Add(new());
            return View("Form", model);
        }
        if (action.StartsWith("remove:", StringComparison.Ordinal) && int.TryParse(action[7..], out var index))
        {
            if (index < 0 || index >= model.CostHelper.Ingredients.Count || model.CostHelper.Ingredients.Count <= 1) return BadRequest();
            model.CostHelper.Ingredients.RemoveAt(index);
            // Preserve raw numeric input (including errors) when renumbering the remaining rows.
            // Snapshot scalar values first: ModelState.Remove clears the original entry object.
            var states = ModelState.Where(x => x.Key.StartsWith("CostHelper.Ingredients[", StringComparison.Ordinal))
                .Select(x => (x.Key, x.Value!.RawValue, x.Value.AttemptedValue,
                    Errors: x.Value.Errors.Select(e => e.ErrorMessage).ToArray())).ToArray();
            foreach (var pair in states) ModelState.Remove(pair.Key);
            foreach (var pair in states)
            {
                var end = pair.Key.IndexOf(']');
                if (!int.TryParse(pair.Key[23..end], out var old) || old == index) continue;
                var key = $"CostHelper.Ingredients[{(old > index ? old - 1 : old)}]" + pair.Key[(end + 1)..];
                ModelState.SetModelValue(key, pair.RawValue, pair.AttemptedValue);
                foreach (var error in pair.Errors) ModelState.AddModelError(key, error);
            }
            return View("Form", model);
        }
        if (action is not ("calculate" or "use")) return BadRequest();
        if (!ModelState.IsValid) return InvalidForm("Form", model);
        try
        {
            model.CostPreview = await sender.Send(new PreviewProductCostQuery(model.CostHelper.Inputs()), ct);
            var reviewedFingerprint = model.CostPreviewFingerprint;
            model.CostPreviewFingerprint = CostFingerprint(model.CostPreview.Inputs);
            ModelState.Remove(nameof(model.CostPreviewFingerprint));
            if (action == "use")
            {
                // UX stale-review check only; all arithmetic and validation are still performed on the server.
                if (reviewedFingerprint != model.CostPreviewFingerprint)
                { ModelState.AddModelError("", Text("Ingredients changed. Review the recalculated breakdown before using this cost.")); return InvalidForm("Form", model); }
                if (!model.UseCostHelper) model.UnitCostBeforeHelper = model.UnitCost;
                model.UnitCost = model.CostPreview.Result.EstimatedUnitCost;
                model.UseCostHelper = true; model.ClearCostBreakdown = false; model.HelperOpen = false;
                foreach (var key in new[] { nameof(model.UnitCost), nameof(model.UnitCostBeforeHelper), nameof(model.UseCostHelper), nameof(model.ClearCostBreakdown) }) ModelState.Remove(key);
            }
            return View("Form", model);
        }
        catch (Exception e) when (e is ProductCostValidationException or ValidationException)
        { AddCostErrors(e); return InvalidForm("Form", model); }
    }
}
