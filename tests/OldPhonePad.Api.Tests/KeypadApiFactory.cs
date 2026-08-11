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
/// Appends routes to the very end of the pipeline that throw exceptions the API has no
/// handler for.
/// </summary>
/// <remarks>
/// <para>
/// The middleware is added after the application's own configuration, which puts it downstream
/// of both the exception handler and endpoint routing. Requests for a path that matches no
/// endpoint fall through to it, and the exception it throws then travels back up to the
/// centralised handler exactly as an unforeseen fault would.
/// </para>
/// <para>
/// Two exception types are offered on purpose. Testing only one that the handler is known not to
/// recognise proves very little: the interesting question is what happens to a fault whose type
/// the handler <em>does</em> match for an unrelated reason.
/// </para>
/// </remarks>
public sealed class UnexpectedFailureRoute : IStartupFilter
{
    /// <summary>The path that fails. Chosen to look nothing like a real route.</summary>
    public const string Path = "/test-only/unexpected-failure";

    /// <summary>
    /// A path that fails with <see cref="ArgumentException"/> for a reason that has nothing to do
    /// with keypad input.
    /// </summary>
    /// <remarks>
    /// <see cref="ArgumentException"/> is thrown all over the base class library and ASP.NET Core,
    /// and <see cref="ArgumentNullException"/> and <see cref="ArgumentOutOfRangeException"/> both
    /// derive from it. A server fault of this type must still be a 500 that discloses nothing,
    /// rather than being mistaken for a client's bad keypad message.
    /// </remarks>
    public const string ArgumentFailurePath = "/test-only/unexpected-argument-failure";

    /// <summary>
    /// Included in the thrown exception so a test can assert the response does not repeat it.
    /// </summary>
    public const string LeakCanary = "SensitiveInternalDetail";

    /// <summary>
    /// Named inside the simulated <see cref="ArgumentException"/>'s message, standing in for the
    /// internal detail such a message usually carries, so a test can assert it is not disclosed.
    /// </summary>
    public const string InternalParameterName = "someInternalParameter";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            next(app);

            app.Use((HttpContext context, Func<Task> next) =>
            {
                if (context.Request.Path == Path)
                {
                    throw new InvalidOperationException($"Simulated fault exposing {LeakCanary}.");
                }

                if (context.Request.Path == ArgumentFailurePath)
                {
                    throw new ArgumentException(
                        $"Simulated fault exposing {LeakCanary}. (Parameter '{InternalParameterName}')");
                }

                return next();
            });
        };
}
