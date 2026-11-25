using System.Net;
using System.Net.Http.Json;
using System.Text;
using B1.Api.Endpoints;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Api.Tests;

public class SecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/\t/evil.example/")]
    [InlineData("/\n/evil.example/")]
    [InlineData("//evil")]
    [InlineData(@"/\evil")]
    [InlineData("https://evil")]
    public async Task DevLogin_WhenReturnUrlIsNotLocal_RedirectsToRoot(string returnUrl)
    {
        var response = await _factory.CreateBrowser().GetAsync($"/auth/dev-login?email=redirect@example.com&returnUrl={Uri.EscapeDataString(returnUrl)}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task DevLogin_WhenReturnUrlIsLocal_KeepsIt()
    {
        var response = await _factory.CreateBrowser().GetAsync($"/auth/dev-login?email=redirect@example.com&returnUrl={Uri.EscapeDataString("/egzamin?x=1")}");

        response.Headers.Location!.OriginalString.Should().Be("/egzamin?x=1");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/patterns")]
    public async Task Responses_HaveSecurityHeaders_EvenWhenUnauthorized(string url)
    {
        var response = await _factory.CreateBrowser().GetAsync(url);

        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'self'").And.Contain("frame-ancestors 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Single().Should().Be("strict-origin-when-cross-origin");
        response.Headers.GetValues("Permissions-Policy").Single().Should().Be("microphone=(self), camera=()");
    }

    [Fact]
    public async Task Post_WhenOriginIsForeign_Returns403()
    {
        var client = await _factory.LoginAsync("origin@example.com");
        var request = new HttpRequestMessage(HttpMethod.Post, "/patterns") { Content = JsonContent.Create(Pattern("Origin")) };
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        TestData.Code(await TestData.ProblemAsync(response)).Should().Be("forbidden-origin");
    }

    [Fact]
    public async Task Post_WhenOriginIsSameSite_IsAllowed()
    {
        var client = await _factory.LoginAsync("origin@example.com");
        var request = new HttpRequestMessage(HttpMethod.Post, "/patterns") { Content = JsonContent.Create(Pattern("Same origin")) };
        request.Headers.Add("Origin", "http://localhost");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_WhenBodyLargerThanOneMegabyte_Returns413()
    {
        var client = await _factory.LoginAsync("large@example.com");
        var content = new StringContent("{\"title\":\"" + new string('a', 1_100_000) + "\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/patterns", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        TestData.Code(await TestData.ProblemAsync(response)).Should().Be("payload-too-large");
    }

    [Fact]
    public async Task Patterns_WhenTitleTooLong_Returns400WithValidationCode()
    {
        var client = await _factory.LoginAsync("limits@example.com");

        var response = await client.PostAsJsonAsync("/patterns", Pattern(new string('t', 513)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        TestData.Code(await TestData.ProblemAsync(response)).Should().Be("validation");
    }

    [Fact]
    public async Task Groups_WhenNameTooLong_Returns400()
    {
        var client = await _factory.LoginAsync(ApiFactory.TeacherEmail);

        var response = await client.PostAsJsonAsync("/groups", new { name = new string('g', 201) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Site_InDevelopmentWithDevLogin_ReportsDevLogin()
    {
        var site = await _factory.CreateBrowser().GetFromJsonAsync<SiteDto>("/site");

        site!.DevLogin.Should().BeTrue();
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task Site_WhenNotDevelopmentOrDisabled_HidesDevLogin(string environment, bool devLogin)
    {
        await using var factory = new ApiFactory { Environment = environment, DevLogin = devLogin };

        var site = await factory.CreateBrowser().GetFromJsonAsync<SiteDto>("/site");

        site!.DevLogin.Should().BeFalse();
    }

    [Fact]
    public async Task Cookie_OutsideDevelopment_IsHostPrefixedAndSecure()
    {
        await using var factory = new ApiFactory { Environment = "Production", DevLogin = false };
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

        options.Cookie.Name.Should().Be("__Host-b1.auth");
        options.Cookie.SecurePolicy.Should().Be(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always);
    }

    private static object Pattern(string title) => new { title, wrongExample = "z moja", correctForm = "z moją", rule = "z + narzędnik", category = "gramatyka" };
}

public class FeedbackRateLimitTests
{
    [Fact]
    public async Task Feedback_WhenMoreThanFivePerHourFromOneAddress_Returns429WithRetryAfter()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateBrowser();
        // the honeypot field makes the endpoint answer 204 without storing anything; the rate limiter still counts
        var body = new { topic = "question", message = "Dzień dobry, mam pytanie.", website = "bot" };

        for (var i = 0; i < 5; i++) (await client.PostAsJsonAsync("/feedback", body)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var limited = await client.PostAsJsonAsync("/feedback", body);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        TestData.Code(await TestData.ProblemAsync(limited)).Should().Be("rate-limited");
    }
}
