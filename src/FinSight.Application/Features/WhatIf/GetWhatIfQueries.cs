using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.WhatIf;

public sealed record GetWhatIfByIdQuery(Guid Id) : IRequest<WhatIfResponse>;
public sealed record GetWhatIfHistoryQuery(int Page = 1) : IRequest<WhatIfHistoryResponse>;
public sealed record WhatIfHistoryResponse(IReadOnlyList<WhatIfResponse> Items,
    int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed class GetWhatIfByIdValidator : AbstractValidator<GetWhatIfByIdQuery>
{
    public GetWhatIfByIdValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetWhatIfHistoryValidator : AbstractValidator<GetWhatIfHistoryQuery>
{
    public GetWhatIfHistoryValidator() => RuleFor(x => x.Page)
        .InclusiveBetween(1, int.MaxValue / 20);
}

public sealed class GetWhatIfByIdHandler(IWhatIfSimulationRepository simulations,
    ICurrentUserContext user) : IRequestHandler<GetWhatIfByIdQuery, WhatIfResponse>
{
    public async Task<WhatIfResponse> Handle(GetWhatIfByIdQuery query,
        CancellationToken cancellationToken) => WhatIfResponse.From(
            await simulations.GetByIdAsync(query.Id, user.BusinessId, cancellationToken)
            ?? throw new NotFoundException(nameof(WhatIfSimulation), query.Id));
}

public sealed class GetWhatIfHistoryHandler(IWhatIfSimulationRepository simulations,
    ICurrentUserContext user) : IRequestHandler<GetWhatIfHistoryQuery, WhatIfHistoryResponse>
{
    public async Task<WhatIfHistoryResponse> Handle(GetWhatIfHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await simulations.GetPagedAsync(user.BusinessId, query.Page, 20,
            cancellationToken);
        return new WhatIfHistoryResponse(result.Items.Select(WhatIfResponse.From).ToArray(),
            query.Page, 20, result.TotalCount);
    }
}
