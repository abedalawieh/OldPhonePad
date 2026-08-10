using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// The interactive documentation is how a customer discovers this API, so it is verified rather
/// than assumed.
/// </summary>
public sealed class OpenApiTests(KeypadApiFactory factory) : IClassFixture<KeypadApiFactory>
{
    [Fact]
    public async Task OpenApiDocument_DescribesTheDecodeEndpoint()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Old Phone Pad API", document.GetProperty("info").GetProperty("title").GetString());
        Assert.True(
            document.GetProperty("paths").TryGetProperty("/api/keypad/decode", out JsonElement decode),
            "The decode endpoint is missing from the OpenAPI document.");
        Assert.True(decode.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task InteractiveReference_IsServed()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InteractiveReference_IsLinkedFromTheDemoPage()
    {
        using HttpClient client = factory.CreateClient();

        // The root used to redirect here. It now serves the demo page, so the reference has to be
        // reachable from it - otherwise the engineer-facing documentation becomes unlisted.
        string html = await client.GetStringAsync("/");

        Assert.Contains("/scalar/v1", html, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);
    }
}
