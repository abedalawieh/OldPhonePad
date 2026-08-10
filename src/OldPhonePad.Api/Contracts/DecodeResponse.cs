namespace OldPhonePad.Api.Contracts;

/// <summary>
/// The text a keypad message decoded to.
/// </summary>
/// <param name="Output">
/// The decoded text. Empty when the message contained no completed key presses, for example <c>"#"</c>.
/// </param>
public sealed record DecodeResponse(string Output);
