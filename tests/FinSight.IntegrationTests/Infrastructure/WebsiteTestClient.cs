using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinSight.IntegrationTests.Infrastructure;

internal static class WebsiteTestClient
{
    public static HttpClient Browser(this FinSightApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost:7080"), AllowAutoRedirect = false, HandleCookies = true
    });

    public static async Task<HttpResponseMessage> Submit(this HttpClient browser, string path,
        Dictionary<string, string> fields, string? formPage = null)
    {
        var page = await browser.GetAsync(formPage ?? path);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = token;
        return await browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public static Dictionary<string, string> Registration(string email) => new()
    {
        ["Email"] = email, ["Password"] = "Password1!", ["ConfirmPassword"] = "Password1!",
        ["BusinessName"] = "Test Cafe", ["Location"] = "Baghdad", ["Currency"] = "IQD", ["ContactInformation"] = email
    };

    public static async Task Confirm(this HttpClient browser, EmailMessage message)
    {
        var entry = await browser.GetAsync(message.ConfirmationLink.PathAndQuery);
        Assert.Equal("/account/confirm-email", entry.Headers.Location!.OriginalString);
        var form = await browser.GetStringAsync("/account/confirm-email");
        Assert.DoesNotContain(message.ConfirmationToken, form);
        Assert.DoesNotContain("name=\"Token\"", form);
        var result = await browser.Submit("/account/confirm-email", []);
        Assert.Equal("/account/confirmation-success", result.Headers.Location!.OriginalString);
    }
}
