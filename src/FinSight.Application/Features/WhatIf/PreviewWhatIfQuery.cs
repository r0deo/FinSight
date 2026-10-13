using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.WhatIf;

public sealed record PreviewWhatIfQuery(WhatIfRequest Request) : IRequest<WhatIfPreviewResponse>;

public sealed record WhatIfPreviewResponse(string ProductName, string Currency,
    WhatIfInputs Inputs, WhatIfResult Result);

public sealed class PreviewWhatIfValidator : AbstractValidator<PreviewWhatIfQuery>
{
    public PreviewWhatIfValidator() => RuleFor(x => x.Request).NotNull()
        .SetValidator(new WhatIfRequestValidator());
}

public sealed class PreviewWhatIfHandler(IProductRepository products,
    IBusinessCurrencyReader currencies, ICurrentUserContext user, WhatIfCalculator calculator)
    : IRequestHandler<PreviewWhatIfQuery, WhatIfPreviewResponse>
{
    public async Task<WhatIfPreviewResponse> Handle(PreviewWhatIfQuery query,
        CancellationToken cancellationToken)
    {
        var request = query.Request;
        var product = await products.GetActiveByIdAsync(request.ProductId, user.BusinessId,
            cancellationToken) ?? throw new NotFoundException(nameof(Product), request.ProductId);
        var currency = await currencies.GetCurrencyAsync(user.BusinessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var inputs = WhatIfRequestMapping.ToInputs(request, product.SellingPrice, product.UnitCost);
        return new WhatIfPreviewResponse(product.Name, currency, inputs,
            calculator.Calculate(inputs));
    }
}

internal static class WhatIfRequestMapping
{
    internal static WhatIfInputs ToInputs(WhatIfRequest request, decimal price, decimal cost) =>
        new(price, cost, request.CurrentQuantity, request.CostInputMode,
            request.CostIncreasePercent, request.NewUnitCost, request.SalesDeclinePercent,
            request.AlternativePrice, request.AlternativePriceQuantity,
            request.AlternativeSupplierCost, request.AlternativeSupplierQuantity);
}
