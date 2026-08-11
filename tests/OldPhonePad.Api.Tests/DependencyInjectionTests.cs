using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using IronSoftware.OldPhonePad;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// Proves the endpoint resolves the converter from the container rather than calling the library's
/// static method.
/// </summary>
/// <remarks>
/// Every other test would pass either way, because the two paths produce identical results. This
/// one replaces the registration with a stand-in: if the response changes, the endpoint genuinely
/// depends on <see cref="IOldPhonePadConverter"/>, which is the point of registering it.
/// </remarks>
public sealed class DependencyInjectionTests(KeypadApiFactory factory) : IClassFixture<KeypadApiFactory>
{
    private const string DecodeUrl = "/api/keypad/decode";

    [Fact]
    public async Task Decode_WhenTheRegisteredConverterIsReplaced_UsesTheReplacement()
    {
        using WebApplicationFactory<Program> withStub = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOldPhonePadConverter>();
                services.AddSingleton<IOldPhonePadConverter, StubConverter>();
            }));

        using HttpClient client = withStub.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = "4433555 555666#" });

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The real converter would answer "HELLO".
        Assert.Equal(StubConverter.Output, body.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Decode_WithTheApplicationsOwnRegistration_UsesTheRealLibrary()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = "4433555 555666#" });

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("HELLO", body.GetProperty("output").GetString());
    }

    /// <summary>A converter that decodes nothing, so its output is unmistakable.</summary>
    private sealed class StubConverter : IOldPhonePadConverter
    {
        internal const string Output = "STUBBED";

        public string Convert(string input) => Output;

        public bool TryConvert(
            string? input,
            [NotNullWhen(true)] out string? output,
            [NotNullWhen(false)] out string? errorMessage)
        {
            output = Output;
            errorMessage = null;
            return true;
        }
    }
}
