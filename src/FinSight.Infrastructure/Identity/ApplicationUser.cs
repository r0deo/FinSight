using Microsoft.AspNetCore.Identity;

namespace FinSight.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid BusinessId { get; private set; }

    private ApplicationUser() { }

    public ApplicationUser(Guid id, Guid businessId, string email)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(id));
        }

        if (businessId == Guid.Empty)
        {
            throw new ArgumentException("Business ID cannot be empty.", nameof(businessId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        }

        var normalizedEmail = email.Trim();

        Id = id;
        BusinessId = businessId;
        Email = normalizedEmail;
        UserName = normalizedEmail;
    }
}
