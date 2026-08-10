using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OldPhonePad.Api.Tests;

/// <summary>
/// Hosts the API in memory for integration tests.
/// </summary>
/// <remarks>
/// The application is run as Production so the tests see what a deployed instance would return.
/// In Development ASP.NET Core serves the developer exception page, which would make the tests
/// asserting that failures do not leak internal detail pass for the wrong reason.
/// </remarks>
public sealed class KeypadApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);

        // Adds a route that fails in an unforeseen way, so the tests can prove that an
        // exception the API does not anticipate still produces a safe response. It is
        // registered from the test project only: the API itself has no such endpoint.
        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, UnexpectedFailureRoute>());
    }
}

/// <summary>
/// Hosts the API as it runs on a developer's machine, which is what <c>dotnet run</c> produces.
/// </summary>
/// <remarks>
/// ASP.NET Core reports some request failures differently in development: a body it cannot read
/// is answered directly in production but raised as an exception here. Since the demo is run in
/// development far more often than not, that path is worth covering.
/// </remarks>
public sealed class DevelopmentKeypadApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment(Environments.Development);
}

/// <summary>
/// Appends a route to the very end of the pipeline that throws an exception the API has no
/// handler for.
/// </summary>
/// <remarks>
/// The middleware is added after the application's own configuration, which puts it downstream
/// of both the exception handler and endpoint routing. Requests for a path that matches no
/// endpoint fall through to it, and the exception it throws then travels back up to the
/// centralised handler exactly as an unforeseen fault would.
/// </remarks>
public sealed class UnexpectedFailureRoute : IStartupFilter
{
    /// <summary>The path that fails. Chosen to look nothing like a real route.</summary>
    public const string Path = "/test-only/unexpected-failure";

    /// <summary>
    /// Included in the thrown exception so a test can assert the response does not repeat it.
    /// </summary>
    public const string LeakCanary = "SensitiveInternalDetail";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            next(app);

            app.Use((HttpContext context, Func<Task> next) => context.Request.Path == Path
                ? throw new InvalidOperationException($"Simulated fault exposing {LeakCanary}.")
                : next());
        };
}
