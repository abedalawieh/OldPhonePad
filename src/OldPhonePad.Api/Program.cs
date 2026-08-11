using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using OldPhonePad.Api.Endpoints;
using OldPhonePad.Api.ErrorHandling;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Generates the OpenAPI document that the interactive reference page renders. Without the
// transformer the document is titled after the assembly, which tells a customer nothing.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Old Phone Pad API",
            Version = "v1",
            Description =
                "A demonstration of the IronSoftware.OldPhonePad library exposed over HTTP. " +
                "All keypad behaviour lives in the library; this API only carries it over HTTP.",
        };

        return Task.CompletedTask;
    }));

// The keypad library. AddOldPhonePad comes from the companion package, which carries the
// dependency injection abstractions so the keypad library itself can keep none. Consumers
// with no container reference only the keypad package and call it directly.
builder.Services.AddOldPhonePad();

// Validates request contracts against their data annotations before an endpoint runs,
// producing a ValidationProblemDetails response naming the offending property.
builder.Services.AddValidation();

// Every error response in this API is a ProblemDetails document, including the ones the
// framework produces for 404, 405 and 415. Validation error keys are renamed to match the
// JSON contract, so a client finds them under the property name it sent.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ValidationProblemKeyNaming.UseJsonPropertyNames);

// Answers a request whose body could not be read with a 400. It recognises nothing else, so a
// genuine fault still becomes a generic 500 that discloses nothing.
builder.Services.AddExceptionHandler<MalformedRequestExceptionHandler>();

// Minimal APIs raise a binding failure as an exception in development but answer it directly in
// production, where the response is a bare 400 with no explanation. Enabling this everywhere sends
// both environments down the same path, so a deployed instance explains an unreadable body just as
// clearly as a local one - and the behaviour the tests describe is the behaviour customers get.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// This application is deployed behind a proxy that terminates TLS and forwards the request over
// plain HTTP. Without this the forwarded scheme is ignored, UseHttpsRedirection below sees http,
// and every request is answered with a 307 back to https - which the proxy forwards as http again.
// That is an infinite redirect loop, and it was reproduced before this was added.
//
// Only the scheme is honoured. Nothing here makes a decision based on the client's address, so
// there is no reason to accept a claim about it. The proxy's own address is not knowable ahead of
// deployment, so the known-network lists are cleared; that is the accepted trade-off for a
// host-managed proxy, and it is safe here because the app is only reachable through one.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

WebApplication app = builder.Build();

// Before anything that inspects the request, so the rest of the pipeline sees the scheme the
// client actually used rather than the one the proxy used to reach this process.
app.UseForwardedHeaders();

// Must come first among the error-handling middleware so it can catch exceptions thrown further
// along the pipeline.
app.UseExceptionHandler();

// Gives responses that have a status code but no body - 404 and 405, for example - a
// ProblemDetails body, so clients can parse every failure the same way.
app.UseStatusCodePages();

// Ahead of the static files below, so the demo page is upgraded on the same terms as the API.
// Serving files first would short-circuit the request and leave the page as the one thing on the
// site reachable over plain HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// The demo page at the site root. It is a single static file with no build step and no
// dependencies, served by this application so the demo deploys as one unit.
app.UseDefaultFiles();
app.UseStaticFiles();

// Exposed in every environment because this application exists to demonstrate the library:
// the interactive reference is the demo. A production service would gate this behind an
// environment check or authentication.
app.MapOpenApi();
app.MapScalarApiReference(options => options
    .WithTitle("Old Phone Pad API")
    .WithTheme(ScalarTheme.BluePlanet));

app.MapKeypadEndpoints();

await app.RunAsync();

/// <summary>
/// Exposed so the integration tests can drive the real pipeline through
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Declaring it here avoids making the API's
/// internals visible to the test assembly.
/// </summary>
public partial class Program;
