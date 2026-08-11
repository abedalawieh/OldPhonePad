using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OldPhonePad.Api.ErrorHandling;

/// <summary>
/// Turns a request whose body could not be read into a 400 ProblemDetails response.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core answers an unreadable body itself in production, but raises
/// <see cref="BadHttpRequestException"/> in development. A malformed request is the client's
/// mistake in either environment, so this handler makes the two environments agree and keeps the
/// framework's own message - which quotes the endpoint's parameter name and type - out of the
/// response.
/// </para>
/// <para>
/// This handler deliberately recognises nothing else. Invalid keypad input never reaches it,
/// because the endpoint asks the library for a result rather than letting it throw. Anything else
/// is passed on, and the framework's exception handler produces a generic 500 whose body carries
/// no message, type name or stack trace.
/// </para>
/// <para>
/// Widening this to a common exception type would be a mistake worth naming: an
/// <see cref="ArgumentException"/> raised anywhere in the pipeline would then be reported to the
/// caller as their fault, and its message - written for a developer reading a log - would be
/// copied into the response body.
/// </para>
/// </remarks>
internal sealed class MalformedRequestExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    /// <summary>The title used whenever a request body cannot be read.</summary>
    internal const string MalformedRequestTitle = "Malformed request";

    /// <summary>
    /// Said instead of the framework's own message, which names the endpoint's parameter and the
    /// CLR type it binds to. Neither means anything to an HTTP client.
    /// </summary>
    /// <remarks>
    /// This one sentence has to be true of every way a body can fail to bind, and there are two:
    /// JSON that does not parse, and JSON that parses but does not fit the contract - most often
    /// <c>{"input": 123}</c>, an unquoted value from a client that forgot the type. Saying the body
    /// "could not be read" would be plainly false of the second, so the wording names the shape
    /// required rather than describing what went wrong.
    /// </remarks>
    private const string MalformedRequestDetail =
        "The request body could not be read as the expected JSON. It must be a JSON object with an " +
        "'input' property whose value is a string, for example {\"input\": \"4433555 555666#\"}.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException malformedRequest)
        {
            // Not ours: let the pipeline fall through to the generic 500 handler.
            return false;
        }

        httpContext.Response.StatusCode = malformedRequest.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = malformedRequest.StatusCode,
                Title = MalformedRequestTitle,
                Detail = MalformedRequestDetail,
            },
        });
    }
}
