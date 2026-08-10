using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// Behaviour that differs between environments, verified as a developer running the demo locally
/// would experience it.
/// </summary>
/// <remarks>
/// These duplicate a handful of assertions from <see cref="DecodeEndpointTests"/> on purpose. The
/// rest of the suite runs in production, and a malformed request travels a different path in
/// development - it arrives as an exception rather than being answered by the framework - so the
/// production tests cannot prove anything about what a customer running <c>dotnet run</c> sees.
/// </remarks>
public sealed class DevelopmentEnvironmentTests(DevelopmentKeypadApiFactory factory)
    : IClassFixture<DevelopmentKeypadApiFactory>
{
    private const string DecodeUrl = "/api/keypad/decode";

    [Fact]
    public async Task Decode_WithMalformedJson_Returns400RatherThan500()
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("{\"input\": ", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Malformed request", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Decode_WithMalformedJson_DoesNotRevealTheEndpointsParameterOrType()
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("not json at all", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);
        string body = await response.Content.ReadAsStringAsync();

        // The framework's own message for this failure quotes the parameter declaration.
        Assert.DoesNotContain("DecodeRequest", body, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonException", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WithOfficialExample_StillReturns200()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = "4433555 555666#" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("HELLO", body.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Decode_WithInvalidKeypadInput_Returns400InDevelopmentToo()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = "2A#" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid keypad input", problem.GetProperty("title").GetString());
    }
}
