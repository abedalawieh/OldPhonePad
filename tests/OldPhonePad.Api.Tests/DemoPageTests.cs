using System.Net;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// The demo page served at the site root.
/// </summary>
/// <remarks>
/// The page is what someone evaluating the library sees first, so it is worth a few tests: that
/// it is served at all, that it reaches the endpoint it claims to, and that adding it did not
/// take the API reference away from the engineers who want that instead.
/// </remarks>
public sealed class DemoPageTests(KeypadApiFactory factory) : IClassFixture<KeypadApiFactory>
{
    [Fact]
    public async Task Root_ReturnsTheDemoPage()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        string html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Old Phone Pad", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoPage_PostsToTheEndpointTheApiActuallyExposes()
    {
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/");

        // The page hard-codes the route it calls. If the endpoint is ever moved, this fails here
        // rather than silently leaving a demo that returns 404 for every visitor.
        Assert.Contains("api/keypad/decode", html, StringComparison.Ordinal);

        using HttpResponseMessage endpoint = await client.PostAsJsonAsync(
            "/api/keypad/decode",
            new { input = "33#" });

        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
    }

    [Fact]
    public async Task DemoPage_ReferencesNoExternalResources()
    {
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/");

        // Everything is inline. A demo that silently depends on a CDN breaks the day the CDN
        // does, and would not work at all on a machine without internet access.
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("//cdn.", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiReference_IsStillServed()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage scalar = await client.GetAsync("/scalar/v1");
        using HttpResponseMessage document = await client.GetAsync("/openapi/v1.json");

        // The demo page took over the root; the engineer-facing reference must survive that.
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
    }
}
