using System.Globalization;
using System.Text;

namespace IronSoftware.OldPhonePad;

/// <summary>
/// Decodes old mobile phone multi-tap keypad input into the text it represents.
/// </summary>
/// <remarks>
/// <para>
/// On an old phone keypad each numeric key carries several letters, and a letter is chosen
/// by pressing the key repeatedly: <c>2</c> gives <c>A</c>, <c>22</c> gives <c>B</c>,
/// <c>222</c> gives <c>C</c>. Two letters on the same key are separated by a pause,
/// written as a space. <c>*</c> is backspace and <c>#</c> sends the message.
/// </para>
/// <para>
/// This type is stateless and thread-safe.
/// </para>
/// </remarks>
public static class OldPhonePadConverter
{
    /// <summary>The send key: ends the message.</summary>
    private const char SendKey = '#';

    /// <summary>The backspace key: removes the most recently produced character.</summary>
    private const char BackspaceKey = '*';

    /// <summary>The pause: separates two characters typed on the same key. Produces nothing itself.</summary>
    private const char PauseKey = ' ';

    /// <summary>Sentinel meaning "no key press is currently pending". Cannot collide with a real character.</summary>
    private const int NoKeyPressed = -1;

    /// <summary>
    /// Decodes a complete old phone keypad message into text.
    /// </summary>
    /// <param name="input">
    /// The key presses to decode, terminated by the send key <c>#</c>. Valid characters are the
    /// digits <c>2</c>-<c>9</c>, a space for a pause, <c>*</c> for backspace and a single trailing <c>#</c>.
    /// </param>
    /// <returns>
    /// The decoded text in upper case. Returns an empty string when the input is only the send key.
    /// </returns>
    /// <example>
    /// <code>
    /// OldPhonePadConverter.Convert("4433555 555666#"); // "HELLO"
    /// OldPhonePadConverter.Convert("227*#");           // "B"
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is not a valid message. This covers input that is not terminated by
    /// <c>#</c>, input containing characters after the <c>#</c>, unsupported characters, and a key
    /// pressed more times than it has characters mapped to it. The exception message identifies the
    /// offending key or character and its position.
    /// </exception>
    public static string Convert(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateTermination(input);

        var text = new StringBuilder(input.Length);

        // State of the key press run currently being accumulated.
        int currentKey = NoKeyPressed;
        int pressCount = 0;

        // The final character is the send key (guaranteed by ValidateTermination), so the loop
        // below only ever sees digits, pauses and backspaces.
        for (int index = 0; index < input.Length - 1; index++)
        {
            char character = input[index];

            // Another press of the same key extends the current run.
            if (character == currentKey)
            {
                pressCount++;

                // A run cannot select a character the key does not have. Checking here rather
                // than when the run ends reports the exact press that made the input invalid.
                string pressedCharacters = Keypad.CharactersFor(character);
                if (pressCount > pressedCharacters.Length)
                {
                    throw new ArgumentException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Key '{character}' was pressed {pressCount} times by position {index}, but only " +
                            $"{pressedCharacters.Length} characters are mapped to it (\"{pressedCharacters}\"). " +
                            $"Presses beyond the mapped characters are not supported."),
                        nameof(input));
                }

                continue;
            }

            // Anything else ends the run: resolve it to a character before handling what follows.
            AppendRun(text, currentKey, pressCount);
            currentKey = NoKeyPressed;
            pressCount = 0;

            switch (character)
            {
                case PauseKey:
                    // The pause has already done its job by ending the run.
                    break;

                case BackspaceKey:
                    // Backspace removes a produced character; pressing it with nothing
                    // typed does nothing, exactly as it would on the handset.
                    if (text.Length > 0)
                    {
                        text.Length--;
                    }

                    break;

                default:
                    if (!Keypad.IsKey(character))
                    {
                        throw new ArgumentException(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"Unsupported character '{character}' at position {index}. Valid input consists " +
                                $"of the digits 2-9, a space for a pause, '{BackspaceKey}' for backspace and a " +
                                $"trailing '{SendKey}' to send."),
                            nameof(input));
                    }

                    if (!Keypad.IsMappedKey(character))
                    {
                        throw new ArgumentException(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"Key '{character}' at position {index} has no characters mapped to it and is " +
                                $"not supported. The specification does not define the characters produced by " +
                                $"keys 0 and 1."),
                            nameof(input));
                    }

                    currentKey = character;
                    pressCount = 1;
                    break;
            }
        }

        // The send key ends the final run.
        AppendRun(text, currentKey, pressCount);

        return text.ToString();
    }

    /// <summary>
    /// Decodes a complete old phone keypad message into text.
    /// </summary>
    /// <remarks>
    /// This method exists so that code written against the original challenge signature
    /// <c>OldPhonePad(string)</c> compiles unchanged. It delegates to
    /// <see cref="Convert(string)"/>, which is the preferred entry point.
    /// </remarks>
    /// <param name="input">The key presses to decode, terminated by the send key <c>#</c>.</param>
    /// <returns>The decoded text in upper case.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> is not a valid message.</exception>
    public static string OldPhonePad(string input) => Convert(input);

    /// <summary>
    /// Resolves a completed run of key presses to the character it selects and appends it.
    /// Does nothing when no run is pending.
    /// </summary>
    /// <remarks>
    /// This method is total: the press count is validated as the run grows, so by the time a run
    /// is appended it is always in range and there is no failure case to handle here.
    /// </remarks>
    /// <param name="text">The text produced so far.</param>
    /// <param name="key">The key that was pressed, or <see cref="NoKeyPressed"/> when no run is pending.</param>
    /// <param name="pressCount">How many consecutive times the key was pressed.</param>
    private static void AppendRun(StringBuilder text, int key, int pressCount)
    {
        if (pressCount == 0)
        {
            return;
        }

        text.Append(Keypad.CharactersFor((char)key)[pressCount - 1]);
    }

    /// <summary>
    /// Verifies that the message is terminated by exactly one send key, in final position.
    /// </summary>
    /// <remarks>
    /// Running this check up front means the decode loop can treat the last character as known
    /// and never has to consider the send key or an unterminated message.
    /// </remarks>
    private static void ValidateTermination(string input)
    {
        int sendKeyIndex = input.IndexOf(SendKey);

        if (sendKeyIndex < 0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Input must be terminated with the send key '{SendKey}'. Received \"{input}\"."),
                nameof(input));
        }

        if (sendKeyIndex != input.Length - 1)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The send key '{SendKey}' must be the final character because it ends the message, but it " +
                    $"was found at position {sendKeyIndex} of {input.Length}. A single call decodes exactly one " +
                    $"complete message."),
                nameof(input));
        }
    }
}
