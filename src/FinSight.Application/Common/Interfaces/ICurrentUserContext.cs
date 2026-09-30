namespace FinSight.Application.Common.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    Guid BusinessId { get; }

    string Role { get; }
}
