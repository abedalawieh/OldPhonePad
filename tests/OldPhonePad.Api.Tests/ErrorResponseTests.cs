using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// How the API behaves at the edges of the protocol, and what it discloses when it fails.
/// </summary>
public sealed class ErrorResponseTests(KeypadApiFactory factory) : IClassFixture<KeypadApiFactory>
{
    [Fact]
    public async Task UnknownRoute_Returns404AsProblemDetails()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/keypad/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // A matching content type on an empty body would satisfy the assertion above without the
        // client actually receiving a problem document.
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task DecodeRoute_WithWrongHttpMethod_Returns405AsProblemDetails()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/keypad/decode");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(405, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UnexpectedFailure_Returns500WithoutRevealingAnythingAboutTheCause()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(UnexpectedFailureRoute.Path);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();

        // The exception message, its type and any stack frame would all be useful to an attacker
        // and useless to a legitimate client.
        Assert.DoesNotContain(UnexpectedFailureRoute.LeakCanary, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("OldPhonePad.Api", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedArgumentFailure_Returns500RatherThanBeingMistakenForBadKeypadInput()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(UnexpectedFailureRoute.ArgumentFailurePath);

        string body = await response.Content.ReadAsStringAsync();

        // A server fault that happens to be an ArgumentException is still a server fault. Reporting
        // it as a 400 would tell the client to fix a request that was never the problem, and would
        // put the exception's own message - written for a developer, not a caller - in the response.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(UnexpectedFailureRoute.LeakCanary, body, StringComparison.Ordinal);
        Assert.DoesNotContain("someInternalParameter", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Invalid keypad input", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryFailure_CarriesATraceIdentifierForSupportToCorrelate()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/keypad/decode",
            new { input = "33" });

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out JsonElement traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }
}
