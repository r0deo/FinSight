using FinSight.Api.Contracts.PricingSimulations;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pricing-simulations")]
public sealed class PricingSimulationsController(ISender sender) : ControllerBase
{
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
}
