namespace FinSight.Application.Common.Interfaces;

public interface IWebsiteAuthenticationService
{
    Task SignInAsync(string email, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync();
}
