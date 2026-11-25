using System.Net;
using AwesomeAssertions;

namespace B1.Api.Tests;

public class MediaTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MediaTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Media_WhenValidKey_ReturnsImageWithHeaders()
    {
        var client = await _factory.LoginAsync("viewer@example.com");

        var response = await client.GetAsync($"/media/zadania/{ApiFactory.ImageFile}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        response.Headers.CacheControl!.ToString().Should().Contain("max-age");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(_factory.ImageBytes);
    }

    [Theory]
    [InlineData("zadania/..%2Fsecret.png")]
    [InlineData("zadania/test-image.gif")]
    [InlineData("zadania/test.image.png")]
    [InlineData("zadania/%2E%2E%5Csecret.png")]
    public async Task Media_WhenFileNameInvalid_Returns400(string key)
    {
        var client = await _factory.LoginAsync("viewer@example.com");

        var response = await client.GetAsync($"/media/{key}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("other/test-image.png")]
    [InlineData("..%2Fmedia/secret.png")]
    [InlineData("zdjecia/test-image.png")]
    public async Task Media_WhenFolderUnknownOrFileMissing_Returns404(string key)
    {
        var client = await _factory.LoginAsync("viewer@example.com");

        var response = await client.GetAsync($"/media/{key}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Media_WhenAnonymous_Returns401()
    {
        var response = await _factory.CreateBrowser().GetAsync($"/media/zadania/{ApiFactory.ImageFile}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
