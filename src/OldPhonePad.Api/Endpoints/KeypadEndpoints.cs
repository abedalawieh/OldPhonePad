using IronSoftware.OldPhonePad;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OldPhonePad.Api.Contracts;

namespace OldPhonePad.Api.Endpoints;

/// <summary>
/// The HTTP surface over the keypad library.
/// </summary>
/// <remarks>
/// This layer does no decoding of its own. It reads the request, hands the input to
/// <see cref="OldPhonePadConverter"/>, and shapes the answer as JSON. Every keypad rule lives in
/// the library, so a customer calling the library directly gets exactly the same behaviour as a
/// customer calling this API.
/// </remarks>
internal static class KeypadEndpoints
{
    /// <summary>Maps the keypad endpoints onto the application.</summary>
    internal static IEndpointRouteBuilder MapKeypadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder keypad = endpoints
            .MapGroup("/api/keypad")
            .WithTags("Keypad");

        keypad.MapPost("/decode", Decode)
            .WithName("DecodeKeypadInput")
            .WithSummary("Decodes an old phone keypad message into text.")
            .WithDescription(
                """
                Multi-tap keypad input, as typed on an old mobile phone.

                Press a key repeatedly to choose one of its characters: 2 gives A, 22 gives B, 222 gives C.
                Presses cycle, so 2222 returns to A.
                Separate two characters on the same key with a space, which represents a pause: '22 2' gives 'BA'.
                Key 0 types a space and key 1 types the punctuation &'(.
                '*' deletes the previous character and '#' sends the message.

                The message must end with '#', and '#' may not appear anywhere else.
                """)
            .Produces<DecodeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    /// <summary>
    /// Decodes one keypad message.
    /// </summary>
    /// <remarks>
    /// Invalid keypad input throws, and <c>KeypadInputExceptionHandler</c> turns that into a 400.
    /// Catching it here would repeat that translation at every endpoint.
    /// </remarks>
    /// <param name="request">The keypad message to decode.</param>
    /// <param name="converter">
    /// Resolved from the container. The endpoint depends on the library's contract rather than its
    /// concrete type, so the API is wired the way a consuming application would wire it.
    /// </param>
    private static Ok<DecodeResponse> Decode(
        [FromBody] DecodeRequest request,
        IOldPhonePadConverter converter)
    {
        // Input is non-null here: the [Required] attribute on the contract rejects a missing
        // property before the endpoint runs.
        string output = converter.Convert(request.Input!);

        return TypedResults.Ok(new DecodeResponse(output));
    }
}
