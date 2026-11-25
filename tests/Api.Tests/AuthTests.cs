using System.Net;
using System.Net.Http.Json;
using B1.Api.Endpoints;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Endpoints_WhenMapped_OnlyHealthAuthSiteFeedbackAndDevRootAllowAnonymous()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var anonymous = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            .Where(route => route != "/health" && route != "/" && route != "/site" && route != "/feedback" && !route.StartsWith("/auth/"))
            .ToList();

        anonymous.Should().BeEmpty();
    }

    [Fact]
    public void Endpoints_WhenMapped_AuthMeAndSettingsRequireLogin()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        endpoints.Where(e => e.RoutePattern.RawText is "/auth/me" or "/auth/settings")
            .Should().HaveCount(2).And.OnlyContain(e => e.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    [Theory]
    [InlineData("/auth/me")]
    [InlineData("/dashboard/me")]
    [InlineData("/groups/mine")]
    [InlineData("/groups")]
    [InlineData("/teacher/summary")]
    [InlineData("/arkusze")]
    [InlineData("/exam-attempts")]
    [InlineData("/exam-sessions")]
    [InlineData("/patterns")]
    [InlineData("/today")]
    [InlineData("/checklists")]
    [InlineData("/join/abc")]
    [InlineData("/audio/test-key")]
    [InlineData("/no-such-endpoint")]
    public async Task Get_WhenAnonymous_Returns401(string url)
    {
        var response = await _factory.CreateBrowser().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_WhenAnonymous_ReturnsOk()
    {
        var response = await _factory.CreateBrowser().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DevLogin_WhenDevelopmentAndEnabled_SignsInAndAuthMeWorks()
    {
        var client = await _factory.LoginAsync("anna@example.com");

        var me = await client.GetFromJsonAsync<MeDto>("/auth/me");

        me!.Email.Should().Be("anna@example.com");
        me.Role.Should().Be("Student");
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData(@"/\evil.example/")]
    public async Task DevLogin_WhenReturnUrlIsExternal_RedirectsToRoot(string returnUrl)
    {
        var response = await _factory.CreateBrowser().GetAsync($"/auth/dev-login?email=a@example.com&returnUrl={Uri.EscapeDataString(returnUrl)}");

        response.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task Groups_WhenStudentCallsTeacherEndpoint_Returns403()
    {
        var client = await _factory.LoginAsync("student@example.com");

        var response = await client.PostAsJsonAsync("/groups", new { name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class DevLoginOutsideDevelopmentTests
{
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task DevLogin_WhenNotDevelopmentOrDisabled_Returns404AndNoCookie(string environment, bool devLogin)
    {
        await using var factory = new ApiFactory { Environment = environment, DevLogin = devLogin };
        var client = factory.CreateBrowser();

        var response = await client.GetAsync("/auth/dev-login?email=anna@example.com");
        var me = await client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Swagger_WhenProduction_IsNotServed()
    {
        await using var factory = new ApiFactory { Environment = "Production", DevLogin = false };

        var response = await factory.CreateBrowser().GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_WhenWwwHost_RedirectsPermanentlyToBareDomain()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://www.egzamin-b1.pl"),
        });

        var response = await client.GetAsync("/egzamin?x=1");

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location.Should().Be(new Uri("https://egzamin-b1.pl/egzamin?x=1"));
    }
}
