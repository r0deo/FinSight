using FinSight.Api.Website.Controllers;
using FinSight.Api.Website.Models;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Authentication.Commands.Login;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace FinSight.UnitTests.Website;

public class AccountControllerTests
{
    [Theory]
    [InlineData("https://attacker.example", "/products")]
    [InlineData("//attacker.example", "/products")]
    [InlineData("/products/create", "/products/create")]
    public async Task Login_OnlyRedirectsToLocalUrls(string returnUrl, string expectedUrl)
    {
        var authentication = new WebsiteAuthenticationStub();
        var controller = CreateController(authentication);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "owner@example.com", Password = "Password1!", ReturnUrl = returnUrl
        }, CancellationToken.None);

        Assert.Equal(expectedUrl, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.True(authentication.Called);
    }

    [Fact]
    public async Task Login_WithInvalidEmail_DoesNotCallAuthentication()
    {
        var authentication = new WebsiteAuthenticationStub();
        var controller = CreateController(authentication);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "invalid", Password = "Password1!"
        }, CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ViewResult>(result).StatusCode);
        Assert.False(authentication.Called);
        Assert.Contains(nameof(LoginViewModel.Email), controller.ModelState.Keys);
    }

    [Fact]
    public async Task Login_WithRejectedCredentials_ReturnsFormError()
    {
        var controller = CreateController(new WebsiteAuthenticationStub { Reject = true });

        var result = await controller.Login(new LoginViewModel
        {
            Email = "owner@example.com", Password = "wrong"
        }, CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ViewResult>(result).StatusCode);
        Assert.False(controller.ModelState.IsValid);
    }

    private static AccountController CreateController(WebsiteAuthenticationStub authentication)
    {
        var controller = new AccountController(null!, authentication, new LoginValidator(), null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            }
        };
        controller.Url = new UrlHelper(controller.ControllerContext);
        return controller;
    }

    private sealed class WebsiteAuthenticationStub : IWebsiteAuthenticationService
    {
        public bool Called { get; private set; }
        public bool Reject { get; init; }
        public Task SignInAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            Called = true;
            if (Reject) throw new UnauthorizedAccessException("Unable to sign in.");
            return Task.CompletedTask;
        }
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
