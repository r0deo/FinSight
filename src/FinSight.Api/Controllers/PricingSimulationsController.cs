using FinSight.Api.Contracts.PricingSimulations;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulationById;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{AppRoles.Owner},{AppRoles.Manager}")]
[Route("api/pricing-simulations")]
public sealed class PricingSimulationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PricingSimulationResponse>> Create(
        CreatePricingSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSimulationCommand(
            request.ProductId,
            request.ProposedPrice,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity), cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ToResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PricingSimulationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSimulationByIdQuery(id),
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet]
    public async Task<ActionResult<PagedPricingSimulationsResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetSimulationsQuery(page, pageSize),
            cancellationToken);
        return Ok(new PagedPricingSimulationsResponse(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdatePricingSimulationRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateSimulationCommand(
            id,
            request.ProposedPrice,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity), cancellationToken);
        return NoContent();
    }

    [HttpPost("preview")]
    [ProducesResponseType<PreviewPricingSimulationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PreviewPricingSimulationResponse>> Preview(
        PreviewPricingSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new PreviewPricingSimulationQuery(
                request.ProductId,
                request.ProposedPrice,
                request.CurrentSalesQuantity,
                request.ExpectedSalesQuantity),
            cancellationToken);

        return Ok(response);
    }

    private static PricingSimulationResponse ToResponse(
        Application.Features.PricingSimulations.PricingSimulationResponse simulation) => new(
        simulation.Id,
        simulation.ProductId,
        simulation.CreatedByUserId,
        simulation.CurrentPrice,
        simulation.UnitCost,
        simulation.ProposedPrice,
        simulation.CurrentSalesQuantity,
        simulation.ExpectedSalesQuantity,
        simulation.CurrentContribution,
        simulation.ProposedContribution,
        simulation.Difference,
        simulation.IsProposedPriceBelowCost,
        simulation.Recommendation,
        simulation.CreatedAtUtc,
        simulation.UpdatedAtUtc);
}
