namespace IronSoftware.OldPhonePad;

/// <summary>
/// Decodes old mobile phone multi-tap keypad input into the text it represents.
/// </summary>
/// <remarks>
/// <para>
/// The decoding itself never varies, so this abstraction exists for the consumer's benefit rather
/// than the library's: it lets an application resolve the converter from a dependency injection
/// container, depend on the contract instead of the concrete type, and substitute a stand-in in
/// tests that should not exercise real decoding.
/// </para>
/// <para>
/// <see cref="OldPhonePadConverter"/> is the implementation. Callers that do not use dependency
/// injection can keep using its static <see cref="OldPhonePadConverter.Convert(string)"/> method,
/// which is the same code path.
/// </para>
/// <para>
/// Implementations are expected to be stateless and safe to register as a singleton.
/// </para>
/// </remarks>
public interface IOldPhonePadConverter
{
    /// <summary>
    /// Decodes a complete old phone keypad message into text.
    /// </summary>
    /// <param name="input">
    /// The key presses to decode, terminated by the send key <c>#</c>. Valid characters are the
    /// digits <c>0</c>-<c>9</c>, a space for a pause, <c>*</c> for backspace and a single trailing <c>#</c>.
    /// </param>
    /// <returns>
    /// The decoded text in upper case. Returns an empty string when the input is only the send key.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is not a valid message. This covers input that is not terminated by
    /// <c>#</c>, input containing characters after the <c>#</c>, and characters that are not on the
    /// keypad. The exception message identifies the offending character and its position.
    /// </exception>
    string Convert(string input);
}
