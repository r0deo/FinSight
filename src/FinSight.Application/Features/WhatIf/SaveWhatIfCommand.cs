using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.WhatIf;

public sealed record SaveWhatIfCommand(WhatIfRequest Request, decimal PreviewPrice,
    decimal PreviewCost, Guid? SaveId = null) : FinSight.Application.Features.BusinessCosts.CostLinkedRequest, IRequest<WhatIfResponse>;

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
    WhatIfCalculator calculator, TimeProvider clock, FinSight.Application.Features.BusinessCosts.SimulationCostContextService? costContext = null) : IRequestHandler<SaveWhatIfCommand, WhatIfResponse>
{
    public async Task<WhatIfResponse> Handle(SaveWhatIfCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        // Website supplies a stable ID derived from a validated, user-bound preview proof.
        if (command.SaveId is { } saveId)
        {
            var existing = await simulations.GetByIdAsync(saveId, user.BusinessId, cancellationToken);
            if (existing is not null)
            {
                var settings = (command.CostLink ?? new()).Normalize();
                if (settings != (existing.CostContext?.Settings ?? new())
                    || (!string.IsNullOrEmpty(command.CostBasis) && command.CostBasis != existing.CostContext?.BasisFingerprint))
                    throw new PricingSimulationConflictException("Costs or assumptions changed. Calculate again before saving.");
                return WhatIfResponse.From(existing);
            }
        }
        var product = await products.GetActiveByIdAsync(request.ProductId, user.BusinessId,
            cancellationToken) ?? throw new NotFoundException(nameof(Product), request.ProductId);
        if (product.SellingPrice != command.PreviewPrice || product.UnitCost != command.PreviewCost)
            throw new PricingSimulationConflictException(
                "The product price or cost changed since this preview. Calculate again before saving.");
        var currency = await currencies.GetCurrencyAsync(user.BusinessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var inputs = WhatIfRequestMapping.ToInputs(request, product.SellingPrice, product.UnitCost);
        var simulation = new WhatIfSimulation(command.SaveId ?? Guid.NewGuid(), user.BusinessId, product.Id,
            user.UserId, product.Name, currency, request.Period, inputs,
            clock.GetUtcNow().UtcDateTime, calculator);
        simulation.CaptureCostContext(await FinSight.Application.Features.BusinessCosts.SimulationCostContextService.Capture(costContext, command.CostLink, request.Period,
            simulation.Result.BaselineContribution, simulation.Result.Options.Select(x => new FinSight.Domain.Models.ContributionOption(x.Kind.ToString(), x.Contribution)).ToArray(),
            cancellationToken, command.CostBasis, true));
        await simulations.AddAsync(simulation, cancellationToken);
        await simulations.SaveChangesAsync(cancellationToken);
        return WhatIfResponse.From(simulation);
    }
}
