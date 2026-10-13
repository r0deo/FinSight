using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace FinSight.Api.Website.Localization;

public sealed class WebsiteNumberModelBinder(IStringLocalizer<RebazText> text) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        var raw = value.FirstValue;
        if (string.IsNullOrWhiteSpace(raw)) return Task.CompletedTask;
        var type = Nullable.GetUnderlyingType(context.ModelType) ?? context.ModelType;
        if (type == typeof(decimal) && WebsiteNumberParser.TryDecimal(raw, out var amount))
            context.Result = ModelBindingResult.Success(amount);
        else if (type == typeof(int) && WebsiteNumberParser.TryInt(raw, out var count))
            context.Result = ModelBindingResult.Success(count);
        else
            context.ModelState.TryAddModelError(context.ModelName, text["Enter a valid number without grouping separators."]);
        return Task.CompletedTask;
    }
}
