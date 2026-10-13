using System.Security.Cryptography;
using System.Text.Json;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.WhatIf;
using Microsoft.AspNetCore.DataProtection;

namespace FinSight.Api.Website;

// A saved scenario must match a recent server-calculated preview, including its product snapshot.
public sealed class WhatIfPreviewProofService(IDataProtectionProvider protection,
    ICurrentUserContext currentUser)
{
    private readonly ITimeLimitedDataProtector _protector = protection
        .CreateProtector("FinSight.Website.WhatIfPreview.v1").ToTimeLimitedDataProtector();

    public string Create(WhatIfRequest request, decimal price, decimal cost) =>
        _protector.Protect(JsonSerializer.Serialize(new Proof(currentUser.UserId,
            currentUser.BusinessId, request, price, cost)), TimeSpan.FromMinutes(20));

    public bool Matches(string? token, WhatIfRequest request, decimal price, decimal cost)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var proof = JsonSerializer.Deserialize<Proof>(_protector.Unprotect(token));
            return proof is not null && proof.UserId == currentUser.UserId
                && proof.BusinessId == currentUser.BusinessId && proof.Request == request
                && proof.Price == price && proof.Cost == cost;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException
            or FormatException)
        {
            return false;
        }
    }

    private sealed record Proof(Guid UserId, Guid BusinessId, WhatIfRequest Request,
        decimal Price, decimal Cost);
}
