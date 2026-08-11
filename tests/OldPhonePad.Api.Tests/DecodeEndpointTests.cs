using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OldPhonePad.Api.Contracts;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// Integration tests for POST /api/keypad/decode.
/// </summary>
/// <remarks>
/// These exercise the HTTP pipeline: routing, model binding, request validation, the centralised
/// exception handler and JSON shaping. Keypad decoding itself is covered exhaustively by the
/// library's own unit tests and is deliberately not re-tested case by case over HTTP - only enough
/// to prove the API is wired to the library and reports its failures faithfully.
/// </remarks>
public sealed class DecodeEndpointTests(KeypadApiFactory factory) : IClassFixture<KeypadApiFactory>
{
    private const string DecodeUrl = "/api/keypad/decode";

    [Theory]
    [InlineData("33#", "E")]
    [InlineData("227*#", "B")]
    [InlineData("4433555 555666#", "HELLO")]
    [InlineData("8 88777444666*664#", "TURING")]
    public async Task Decode_WithOfficialChallengeExample_Returns200AndDecodedText(
        string input,
        string expected)
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Decode_WithMessageThatProducesNoText_Returns200AndEmptyOutput()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = "#" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("", body.GetProperty("output").GetString());
    }

    [Theory]
    [InlineData("33", "input that never sends")]
    [InlineData("33#999", "content after the send key")]
    [InlineData("2A#", "a character that is not on the keypad")]
    [InlineData("2\t2#", "whitespace that is not the keypad pause")]
    public async Task Decode_WithInvalidKeypadInput_Returns400ProblemDetailsExplainingWhy(
        string input,
        string reason)
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid keypad input", problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());

        // The customer is told what was wrong, not merely that something was: see `reason`.
        string? detail = problem.GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail), $"No explanation given for {reason}.");
    }

    [Fact]
    public async Task Decode_WithInvalidKeypadInput_DoesNotExposeTheLibrarysParameterName()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = "33" });

        // "input" is a parameter of a method inside the library. .NET appends it to the exception
        // message; the API strips it because it means nothing to an HTTP client.
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Parameter", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WithoutInputProperty_Returns400NamingTheMissingProperty()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out JsonElement errors));

        // The key must match the JSON contract the client sent, not the CLR property name, so
        // that a client reading errors.input finds it where the docs say it will be.
        Assert.True(errors.TryGetProperty("input", out _));
        Assert.False(errors.TryGetProperty("Input", out _));
    }

    [Fact]
    public async Task Decode_WithEmptyInput_IsRejectedByTheLibraryRatherThanRequestValidation()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // An empty string is a well-formed request carrying an invalid keypad message, so it is a
        // keypad question rather than a request-shape one. This asserts the boundary split holds:
        // the API validates shape and size, the library validates keypad rules.
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid keypad input", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Decode_WithNullInput_Returns400NamingTheProperty()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Asserting the status alone would pass even if null reached the library and came back
        // described as a keypad problem. An absent value is a request-shape problem, and the
        // client needs to be told which property is missing.
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out JsonElement errors));
        Assert.True(errors.TryGetProperty("input", out _));
    }

    [Fact]
    public async Task Decode_WithInputOfExactlyTheMaximumLength_IsAccepted()
    {
        using HttpClient client = factory.CreateClient();

        // The boundary itself: 1023 key presses plus the send key. Off-by-one here would reject a
        // message the contract promises to accept, and a test using a wildly oversized input
        // cannot tell the difference.
        string atTheLimit = new string('2', DecodeRequest.MaxInputLength - 1) + "#";
        Assert.Equal(DecodeRequest.MaxInputLength, atTheLimit.Length);

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = atTheLimit });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Decode_WithInputOneCharacterOverTheMaximumLength_Returns400()
    {
        using HttpClient client = factory.CreateClient();

        string justOver = new string('2', DecodeRequest.MaxInputLength) + "#";

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = justOver });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out JsonElement errors));
        Assert.True(errors.TryGetProperty("input", out _));
    }

    [Fact]
    public async Task Decode_WithInputLongerThanTheLimit_Returns400()
    {
        using HttpClient client = factory.CreateClient();
        string tooLong = new string('2', 2000) + "#";

        using HttpResponseMessage response = await client.PostAsJsonAsync(DecodeUrl, new { input = tooLong });

        // Rejected on size before any decoding is attempted.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("{\"input\": 123}", "a number")]
    [InlineData("{\"input\": true}", "a boolean")]
    [InlineData("{\"input\": [\"33#\"]}", "an array")]
    [InlineData("{\"input\": {\"value\": \"33#\"}}", "an object")]
    public async Task Decode_WithInputOfTheWrongJsonType_Returns400RatherThan500(
        string body,
        string wrongType)
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);

        // Well-formed JSON that does not fit the contract is still the client's mistake. A 500
        // here would blame the server for a request it was right to refuse.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        string raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DecodeRequest", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonException", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", raw, StringComparison.Ordinal);

        // Status and absence of a leak would both hold for a message that explains the wrong thing.
        // The body here parsed perfectly well and does carry an 'input' property, so saying it
        // "could not be read" would be false; what the client needs told is the shape expected.
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        string? detail = problem.GetProperty("detail").GetString();

        Assert.False(string.IsNullOrWhiteSpace(detail), $"No explanation given for {wrongType}.");
        Assert.Contains("string", detail, StringComparison.Ordinal);
        Assert.Contains("'input'", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WithPropertiesTheContractDoesNotDeclare_IgnoresThem()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = "33#", unexpected = "ignored", anotherOne = 42 });

        // Tolerating unknown properties is what lets a client add fields without this API
        // breaking, and is the default System.Text.Json behaviour worth pinning deliberately.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement decoded = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E", decoded.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Decode_WithInputNamedInDifferentCasing_IsBoundCaseInsensitively()
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("{\"Input\": \"33#\"}", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);

        // ASP.NET Core binds JSON case-insensitively by default. A client that sends "Input"
        // should not be told the property is missing.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement decoded = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E", decoded.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Decode_WithMalformedJson_Returns400ExplainingWhatTheBodyShouldBe()
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("{\"input\": ", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // This factory runs in production, which is what a deployed instance does. Left to itself
        // ASP.NET Core answers a binding failure there with a bare 400 carrying no explanation,
        // while development raises it as an exception and gets the helpful message. Asserting the
        // detail here is what stops the two environments drifting apart again.
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Malformed request", problem.GetProperty("title").GetString());
        Assert.Contains(
            "must be a JSON object",
            problem.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WithoutARequestBody_Returns400ExplainingWhatTheBodyShouldBe()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Malformed request", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Decode_WithNonJsonContentType_Returns415()
    {
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("33#", Encoding.UTF8, "text/plain");

        using HttpResponseMessage response = await client.PostAsync(DecodeUrl, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

}
