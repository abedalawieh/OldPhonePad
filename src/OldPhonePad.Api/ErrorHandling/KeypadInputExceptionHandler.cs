using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OldPhonePad.Api.ErrorHandling;

/// <summary>
/// Translates the keypad library's rejection of invalid input into a 400 ProblemDetails response.
/// </summary>
/// <remarks>
/// <para>
/// The library signals invalid keypad input with <see cref="ArgumentException"/>. That is a client
/// mistake rather than a server fault, so it must not become a 500. Handling it centrally keeps the
/// endpoint free of try/catch blocks and guarantees every endpoint added later behaves the same way.
/// </para>
/// <para>
/// Anything this handler does not recognise is passed on. The framework's exception handler then
/// produces a generic 500 ProblemDetails, so unexpected faults never leak their message or stack.
/// </para>
/// <para>
/// Rejected input is deliberately not logged. It is ordinary traffic for this endpoint rather than
/// a fault, the client is already told exactly what was wrong, and the message quotes the input,
/// which does not belong in server logs. Unhandled exceptions are a different matter, and ASP.NET
/// Core's exception handler middleware logs those itself.
/// </para>
/// </remarks>
internal sealed class KeypadInputExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    /// <summary>Marks the end of an <see cref="ArgumentException"/> message, which .NET appends the parameter name to.</summary>
    private const string ParameterNameSuffix = " (Parameter '";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ClientError? clientError = Classify(exception);

        if (clientError is null)
        {
            // Not ours: let the pipeline fall through to the generic 500 handler.
            return false;
        }

        httpContext.Response.StatusCode = clientError.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = clientError.Status,
                Title = clientError.Title,
                Detail = clientError.Detail,
            },
        });
    }

    /// <summary>
    /// Decides whether an exception represents a mistake by the client, and if so how to describe
    /// it. Returns <see langword="null"/> for anything that is a fault on our side.
    /// </summary>
    private static ClientError? Classify(Exception exception) => exception switch
    {
        // The keypad library rejected the input.
        ArgumentException invalidInput => new ClientError(
            StatusCodes.Status400BadRequest,
            "Invalid keypad input",
            DescribeWithoutParameterName(invalidInput)),

        // The request never became a DecodeRequest: unreadable JSON, or a body that was absent
        // when one was required. ASP.NET Core answers this itself in production but throws in
        // development, and a malformed request is the client's mistake in either environment.
        // The exception's own message names the endpoint's parameter and type, so a fixed
        // description is used instead.
        BadHttpRequestException malformedRequest => new ClientError(
            malformedRequest.StatusCode,
            "Malformed request",
            "The request body could not be read. It must be a JSON object with an 'input' property."),

        _ => null,
    };

    /// <summary>A client mistake, described in the terms the response will use.</summary>
    private sealed record ClientError(int Status, string Title, string Detail);

    /// <summary>
    /// Returns the explanation of why the input was rejected, without the parameter name that
    /// .NET appends to <see cref="ArgumentException.Message"/>.
    /// </summary>
    /// <remarks>
    /// "input" is the name of a method parameter inside the library. To an HTTP client that is an
    /// implementation detail, and the JSON property it corresponds to is already named in the
    /// request contract, so the suffix is dropped rather than shown.
    /// </remarks>
    private static string DescribeWithoutParameterName(ArgumentException exception)
    {
        int suffixStart = exception.Message.IndexOf(ParameterNameSuffix, StringComparison.Ordinal);

        return suffixStart < 0 ? exception.Message : exception.Message[..suffixStart];
    }
}
