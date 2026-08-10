using System.ComponentModel.DataAnnotations;

namespace OldPhonePad.Api.Contracts;

/// <summary>
/// A request to decode one complete old phone keypad message.
/// </summary>
public sealed record DecodeRequest
{
    /// <summary>
    /// The largest keypad message the API will accept, in characters.
    /// </summary>
    /// <remarks>
    /// Decoding is linear and allocates at most one character of output per key press, so a long
    /// message is cheap rather than dangerous. The limit exists so that a public demo endpoint has
    /// a predictable upper bound on the work a single request can ask for, and is set far above any
    /// realistic message: 1024 key presses is longer than several text messages typed end to end.
    /// </remarks>
    public const int MaxInputLength = 1024;

    /// <summary>
    /// The key presses to decode, terminated by the send key <c>#</c>.
    /// </summary>
    /// <example>4433555 555666#</example>
    [Required(AllowEmptyStrings = true, ErrorMessage = "The 'input' property is required.")]
    [MaxLength(MaxInputLength, ErrorMessage = "Input must be at most {1} characters.")]
    public string? Input { get; init; }
}
