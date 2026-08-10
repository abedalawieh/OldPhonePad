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

// Turns the library's rejection of invalid keypad input into a 400 instead of a 500.
builder.Services.AddExceptionHandler<KeypadInputExceptionHandler>();

WebApplication app = builder.Build();

// Must come first so it can catch exceptions thrown further along the pipeline.
app.UseExceptionHandler();

// Gives responses that have a status code but no body - 404 and 405, for example - a
// ProblemDetails body, so clients can parse every failure the same way.
app.UseStatusCodePages();

// The demo page at the site root. It is a single static file with no build step and no
// dependencies, served by this application so the demo deploys as one unit.
app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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
