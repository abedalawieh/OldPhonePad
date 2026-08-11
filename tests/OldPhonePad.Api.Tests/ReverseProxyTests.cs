using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// Behaviour when the application is deployed behind a proxy that terminates TLS, which is how it
/// is hosted and how a customer would host it.
/// </summary>
/// <remarks>
/// <para>
/// The proxy accepts HTTPS and forwards the request to this process over plain HTTP, announcing the
/// original scheme in <c>X-Forwarded-Proto</c>. If that header is ignored, HTTPS redirection sees a
/// plain HTTP request and answers 307 to the HTTPS address - which the proxy forwards over HTTP
/// again. The result is a redirect loop and an API that answers nothing.
/// </para>
/// <para>
/// The loop needs an HTTPS port to be known before it can happen, which is a matter of host
/// configuration rather than anything in the code. These tests set one deliberately so the failure
/// is reproducible here rather than only on a deployed instance.
/// </para>
/// </remarks>
public sealed class ReverseProxyTests
{
    private const string DecodeUrl = "/api/keypad/decode";

    /// <summary>
    /// Runs the application as a host with TLS at the edge would: production, an HTTPS port
    /// advertised, and requests arriving over plain HTTP.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory() =>
        new TlsTerminatingProxyFactory();

    [Fact]
    public async Task Decode_WhenTheProxyReportsTheOriginalSchemeWasHttps_IsServedRatherThanRedirected()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, DecodeUrl)
        {
            Content = JsonContent.Create(new { input = "4433555 555666#" }),
        };

        request.Headers.Add("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request);

        // A 307 here is the redirect loop: the proxy would follow it straight back to this process
        // over HTTP and arrive at the same answer forever.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DemoPage_WhenTheProxyReportsTheOriginalSchemeWasHttps_IsServed()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request);

        // The page a recruiter or customer opens first.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Decode_WhenTheRequestGenuinelyArrivedOverHttp_IsStillRedirectedToHttps()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            DecodeUrl,
            new { input = "33#" });

        // Honouring the forwarded scheme must not amount to switching HTTPS redirection off. A
        // request with no proxy header in front of it is exactly what redirection is for.
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
    }

    [Fact]
    public async Task DemoPage_WhenTheRequestGenuinelyArrivedOverHttp_IsStillRedirectedToHttps()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using HttpResponseMessage response = await client.GetAsync("/");

        // Static files are served by middleware that short-circuits the request, so if redirection
        // is registered after it the page becomes the one thing on the site still reachable over
        // plain HTTP. Ordering is easy to get wrong and invisible without a test.
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
    }

    /// <summary>
    /// Hosts the API as a TLS-terminating proxy would: production, with an HTTPS port advertised so
    /// that redirection is live rather than quietly disabled for want of a port to redirect to.
    /// </summary>
    private sealed class TlsTerminatingProxyFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("https_port", "443");
        }
    }
}
