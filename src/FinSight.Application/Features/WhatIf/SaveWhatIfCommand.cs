using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.WhatIf;

public sealed record SaveWhatIfCommand(WhatIfRequest Request, decimal PreviewPrice,
    decimal PreviewCost) : IRequest<WhatIfResponse>;

public sealed class SaveWhatIfValidator : AbstractValidator<SaveWhatIfCommand>
{
    public SaveWhatIfValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new WhatIfRequestValidator());
        RuleFor(x => x.PreviewPrice).InclusiveBetween(0, WhatIfCalculator.MaxUnitAmount);
        RuleFor(x => x.PreviewCost).InclusiveBetween(0, WhatIfCalculator.MaxUnitAmount);
    }
}

public sealed class SaveWhatIfHandler(IProductRepository products, IBusinessCurrencyReader currencies,
    IWhatIfSimulationRepository simulations, ICurrentUserContext user,
    WhatIfCalculator calculator, TimeProvider clock) : IRequestHandler<SaveWhatIfCommand, WhatIfResponse>
{
    public async Task<WhatIfResponse> Handle(SaveWhatIfCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var product = await products.GetActiveByIdAsync(request.ProductId, user.BusinessId,
            cancellationToken) ?? throw new NotFoundException(nameof(Product), request.ProductId);
        if (product.SellingPrice != command.PreviewPrice || product.UnitCost != command.PreviewCost)
            throw new PricingSimulationConflictException(
                "The product price or cost changed since this preview. Calculate again before saving.");
        var currency = await currencies.GetCurrencyAsync(user.BusinessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var inputs = WhatIfRequestMapping.ToInputs(request, product.SellingPrice, product.UnitCost);
        var simulation = new WhatIfSimulation(Guid.NewGuid(), user.BusinessId, product.Id,
            user.UserId, product.Name, currency, request.Period, inputs,
            clock.GetUtcNow().UtcDateTime, calculator);
        await simulations.AddAsync(simulation, cancellationToken);
        await simulations.SaveChangesAsync(cancellationToken);
        return WhatIfResponse.From(simulation);
    }
}
