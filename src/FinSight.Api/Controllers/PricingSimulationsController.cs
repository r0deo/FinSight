using FinSight.Api.Contracts.PricingSimulations;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Features.PricingSimulations.Commands.ArchiveSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.DeleteSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.RestoreSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.UnarchiveSimulation;
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
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetSimulationsQuery(page, pageSize, includeArchived),
            cancellationToken);
        return Ok(new PagedPricingSimulationsResponse(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PricingSimulationVersionResponse>> Update(
        Guid id,
        UpdatePricingSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateSimulationCommand(
            id,
            request.ProposedPrice,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity,
            request.Version), cancellationToken);
        return Ok(new PricingSimulationVersionResponse(result.Version));
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<ActionResult<PricingSimulationVersionResponse>> Archive(
        Guid id,
        PricingSimulationVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ArchiveSimulationCommand(id, request.Version),
            cancellationToken);
        return Ok(new PricingSimulationVersionResponse(result.Version));
    }

    [HttpPatch("{id:guid}/unarchive")]
    public async Task<ActionResult<PricingSimulationVersionResponse>> Unarchive(
        Guid id,
        PricingSimulationVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UnarchiveSimulationCommand(id, request.Version),
            cancellationToken);
        return Ok(new PricingSimulationVersionResponse(result.Version));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<PricingSimulationVersionResponse>> Delete(
        Guid id,
        PricingSimulationVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteSimulationCommand(id, request.Version),
            cancellationToken);
        return Ok(new PricingSimulationVersionResponse(result.Version));
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPatch("{id:guid}/restore")]
    public async Task<ActionResult<PricingSimulationVersionResponse>> Restore(
        Guid id,
        PricingSimulationVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RestoreSimulationCommand(id, request.Version),
            cancellationToken);
        return Ok(new PricingSimulationVersionResponse(result.Version));
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
        simulation.UpdatedAtUtc,
        simulation.IsArchived,
        simulation.ArchivedAtUtc,
        simulation.Version);
}
