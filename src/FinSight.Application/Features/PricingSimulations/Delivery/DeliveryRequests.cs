using FinSight.Domain.Models;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Delivery;

public interface IDeliveryRequest { DeliveryAssumptions Inputs { get; } }
public sealed record PreviewDeliveryQuery(Guid ProductId, DeliveryAssumptions Inputs) : IRequest<DeliverySimulationSnapshot>, IDeliveryRequest;
public sealed record PreviewSavedDeliveryQuery(Guid SimulationId, DeliveryAssumptions Inputs, long Version) : IRequest<DeliverySimulationSnapshot>, IDeliveryRequest;
public sealed record CreateDeliveryCommand(Guid ProductId, DeliveryAssumptions Inputs, decimal PreviewCurrentPrice, decimal PreviewUnitCost)
    : IRequest<PricingSimulationResponse>, IDeliveryRequest;
public sealed record UpdateDeliveryCommand(Guid SimulationId, DeliveryAssumptions Inputs, long Version)
    : IRequest<PricingSimulationVersionResponse>, IDeliveryRequest;

public abstract class DeliveryValidator<T> : AbstractValidator<T> where T : IDeliveryRequest
{
    protected DeliveryValidator() => RuleFor(x => x.Inputs).Custom((inputs, context) =>
    {
        try { DeliverySimulationCalculator.ValidateAssumptions(inputs); }
        catch (DeliveryValidationException error) { context.AddFailure(DeliveryValidation.Failure(error)); }
    });
}
public sealed class PreviewDeliveryValidator : DeliveryValidator<PreviewDeliveryQuery>
{
    public PreviewDeliveryValidator() => RuleFor(x => x.ProductId).NotEmpty();
}
public sealed class PreviewSavedDeliveryValidator : DeliveryValidator<PreviewSavedDeliveryQuery>
{
    public PreviewSavedDeliveryValidator() { RuleFor(x => x.SimulationId).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0); }
}
public sealed class CreateDeliveryValidator : DeliveryValidator<CreateDeliveryCommand>
{
    public CreateDeliveryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.PreviewCurrentPrice).InclusiveBetween(0, DeliverySimulationCalculator.MaximumMoney);
        RuleFor(x => x.PreviewUnitCost).InclusiveBetween(0, DeliverySimulationCalculator.MaximumMoney);
    }
}
public sealed class UpdateDeliveryValidator : DeliveryValidator<UpdateDeliveryCommand>
{
    public UpdateDeliveryValidator() { RuleFor(x => x.SimulationId).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0); }
}
internal static class DeliveryValidation
{
    public static ValidationFailure Failure(DeliveryValidationException error) =>
        new(error.Field, error.Message) { ErrorCode = "DeliveryCalculation" };
    public static T Run<T>(Func<T> operation)
    {
        try { return operation(); }
        catch (DeliveryValidationException error) { throw new ValidationException([Failure(error)]); }
    }
}
