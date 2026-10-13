using FinSight.Api.Website.Models;
using FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[Authorize(Policy = "Website", Roles = "Owner,Manager"), Route("simulations")]
public sealed class SavedSimulationPagesController(ISender sender)
    : WebsiteController
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest();
        var result = await sender.Send(new GetSimulationsQuery(page, 20, includeArchived, true), cancellationToken);
        return View(new SavedSimulationListViewModel(result, includeArchived));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var saved = await sender.Send(new GetSavedDecisionQuery(id), cancellationToken);
        return View(new SavedSimulationDetailViewModel(saved.Simulation, saved.Decision,
            saved.Currency, saved.Simulation.ProductNameSnapshot ?? Text("Historical product")));
    }
}
