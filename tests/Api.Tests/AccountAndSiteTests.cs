using System.Net;
using System.Net.Http.Json;
using B1.Api.Endpoints;
using AwesomeAssertions;

namespace B1.Api.Tests;

public class AccountAndSiteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountAndSiteTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Site_WhenAnonymous_ReturnsPlaceholdersFromConfiguration()
    {
        var client = _factory.CreateBrowser();

        var response = await client.GetAsync("/site");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var site = await response.Content.ReadFromJsonAsync<SiteDto>();
        site!.Name.Should().NotBeNullOrWhiteSpace();
        site.ContactEmail.Should().Contain("@");
        site.OwnerName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteAccount_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateBrowser();

        var response = await client.DeleteAsync("/auth/account");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_WhenLoggedIn_RemovesUserAndEndsSession()
    {
        var client = await _factory.LoginAsync("leaving@example.com");
        (await client.GetAsync("/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.DeleteAsync("/auth/account");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var again = await _factory.LoginAsync("leaving@example.com");
        var me = await again.GetFromJsonAsync<MeDto>("/auth/me");
        me!.Email.Should().Be("leaving@example.com");
    }

    [Fact]
    public async Task Settings_WhenLanguageChosen_IsStoredInProfileAndReturnedByMe()
    {
        var client = await _factory.LoginAsync("lang@example.com");
        var before = await client.GetFromJsonAsync<MeDto>("/auth/me");
        before!.UiLanguage.Should().BeEmpty();

        var response = await client.PutAsJsonAsync("/auth/settings", new SettingsRequest("B1", "dorosli", "pl"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<MeDto>("/auth/me");
        after!.UiLanguage.Should().Be("pl");
    }

    [Fact]
    public async Task Settings_WhenLanguageOmitted_KeepsPreviousLanguage()
    {
        var client = await _factory.LoginAsync("keep@example.com");
        await client.PutAsJsonAsync("/auth/settings", new SettingsRequest("B1", "dorosli", "en"));

        await client.PutAsJsonAsync("/auth/settings", new SettingsRequest("B2", "dim"));

        var me = await client.GetFromJsonAsync<MeDto>("/auth/me");
        me!.UiLanguage.Should().Be("en");
        me.TargetLevel.Should().Be("B2");
    }

    [Fact]
    public async Task Settings_WhenLanguageUnknown_ReturnsBadRequest()
    {
        var client = await _factory.LoginAsync("bad@example.com");

        var response = await client.PutAsJsonAsync("/auth/settings", new SettingsRequest("B1", "dorosli", "de"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
