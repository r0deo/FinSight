namespace FinSight.Application.Common.Interfaces;

public interface IEmailConfirmationService
{
    Task SendAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ResendAsync(string email, CancellationToken cancellationToken = default);
}
