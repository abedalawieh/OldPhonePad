using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace IronSoftware.OldPhonePad;

/// <summary>
/// Decodes old mobile phone multi-tap keypad input into the text it represents.
/// </summary>
/// <remarks>
/// <para>
/// On an old phone keypad each numeric key carries several characters, and one is chosen by
/// pressing the key repeatedly: <c>2</c> gives <c>A</c>, <c>22</c> gives <c>B</c>, <c>222</c>
/// gives <c>C</c>. Presses cycle, so <c>2222</c> returns to <c>A</c>. Two characters on the same
/// key are separated by a pause, written as a space. <c>*</c> is backspace and <c>#</c> sends
/// the message.
/// </para>
/// <para>
/// This type is stateless and thread-safe. Callers that do not use dependency injection can call
/// the static <see cref="Convert(string)"/> directly; those that do can resolve
/// <see cref="IOldPhonePadConverter"/> and register this type as a singleton.
/// </para>
/// </remarks>
public sealed class OldPhonePadConverter : IOldPhonePadConverter
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
    /// digits <c>0</c>-<c>9</c>, a space for a pause, <c>*</c> for backspace and a single trailing <c>#</c>.
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
    /// <c>#</c>, input containing characters after the <c>#</c>, and characters that are not on the
    /// keypad. The exception message identifies the offending character and its position.
    /// </exception>
    public static string Convert(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryDecode(input, out string output, out string? errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(input));
        }

        return output;
    }

    /// <summary>
    /// Decodes a complete old phone keypad message into text, reporting invalid input by
    /// returning <see langword="false"/> rather than by throwing.
    /// </summary>
    /// <param name="input">The key presses to decode, terminated by the send key <c>#</c>.</param>
    /// <param name="output">
    /// The decoded text when this method returns <see langword="true"/>; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <param name="errorMessage">
    /// An explanation of why the input was rejected when this method returns
    /// <see langword="false"/>; otherwise <see langword="null"/>. The explanation names the
    /// offending character and its position, and is suitable for showing to the caller.
    /// </param>
    /// <returns><see langword="true"/> if the input was decoded; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// Prefer this overload where invalid input is ordinary traffic rather than a fault - a value
    /// typed by a user, or read from a request - because a rejection is then an expected result
    /// instead of an exception. It never throws, including for <see langword="null"/> input.
    /// </para>
    /// <para>
    /// <see cref="Convert(string)"/> remains for callers who want a rejection to be a failure they
    /// cannot ignore. Both run exactly the same decoding.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (OldPhonePadConverter.TryConvert(userInput, out string? text, out string? errorMessage))
    /// {
    ///     Console.WriteLine(text);
    /// }
    /// else
    /// {
    ///     Console.Error.WriteLine(errorMessage);
    /// }
    /// </code>
    /// </example>
    public static bool TryConvert(
        string? input,
        [NotNullWhen(true)] out string? output,
        [NotNullWhen(false)] out string? errorMessage)
    {
        if (input is null)
        {
            output = null;
            errorMessage = "Input must not be null.";
            return false;
        }

        if (!TryDecode(input, out string decoded, out errorMessage))
        {
            output = null;
            return false;
        }

        output = decoded;
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// The decoder. Returns <see langword="false"/> with an explanation rather than throwing, so
    /// that both public entry points share one implementation and one set of messages.
    /// </summary>
    private static bool TryDecode(
        string input,
        out string output,
        [NotNullWhen(false)] out string? errorMessage)
    {
        output = "";

        if (!TryValidateTermination(input, out errorMessage))
        {
            return false;
        }

        var text = new StringBuilder(input.Length);

        // State of the key press run currently being accumulated.
        int currentKey = NoKeyPressed;
        int pressCount = 0;

        // The final character is the send key (guaranteed by ValidateTermination), so the loop
        // below only ever sees digits, pauses and backspaces.
        for (int index = 0; index < input.Length - 1; index++)
        {
            char character = input[index];

            // Another press of the same key extends the current run. The run is resolved when it
            // ends, so there is nothing to validate here: the specification says presses cycle
            // through the key's letters, and a cycle has no upper bound.
            if (character == currentKey)
            {
                pressCount++;
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
                        errorMessage = string.Create(
                            CultureInfo.InvariantCulture,
                            $"Unsupported character {Describe(character)} at position {index}. Valid input " +
                            $"consists of the digits 0-9, a space for a pause, '{BackspaceKey}' for backspace " +
                            $"and a trailing '{SendKey}' to send.");

                        return false;
                    }

                    currentKey = character;
                    pressCount = 1;
                    break;
            }
        }

        // The send key ends the final run.
        AppendRun(text, currentKey, pressCount);

        output = text.ToString();
        errorMessage = null;
        return true;
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
    /// Implements <see cref="IOldPhonePadConverter.Convert(string)"/> for callers that resolve the
    /// converter from a dependency injection container.
    /// </summary>
    /// <remarks>
    /// The implementation is explicit because a type cannot declare both a static and an instance
    /// method with the same signature. Making it explicit keeps <see cref="Convert(string)"/> the
    /// one way to call this type directly, while still satisfying the interface. It delegates, so
    /// there is exactly one implementation of the algorithm however it is reached.
    /// </remarks>
    /// <param name="input">The key presses to decode, terminated by the send key <c>#</c>.</param>
    /// <returns>The decoded text in upper case.</returns>
    string IOldPhonePadConverter.Convert(string input) => Convert(input);

    /// <summary>
    /// Implements <see cref="IOldPhonePadConverter.TryConvert"/>. Explicit for the same reason as
    /// <see cref="IOldPhonePadConverter.Convert"/> above, and it delegates to the static method.
    /// </summary>
    /// <param name="input">The key presses to decode, terminated by the send key <c>#</c>.</param>
    /// <param name="output">The decoded text, or <see langword="null"/> when the input was rejected.</param>
    /// <param name="errorMessage">Why the input was rejected, or <see langword="null"/> when it was not.</param>
    /// <returns><see langword="true"/> if the input was decoded; otherwise <see langword="false"/>.</returns>
    bool IOldPhonePadConverter.TryConvert(
        string? input,
        [NotNullWhen(true)] out string? output,
        [NotNullWhen(false)] out string? errorMessage) => TryConvert(input, out output, out errorMessage);

    /// <summary>
    /// Describes a rejected character in a form a reader can act on.
    /// </summary>
    /// <remarks>
    /// A character that leaves no mark on screen cannot be quoted usefully: a tab reported as
    /// <c>' '</c> tells the reader nothing, and is easily mistaken for the space that is a valid
    /// pause. Anything invisible is therefore named by its code point, and the few that occur in
    /// practice - a tab or a line ending pasted in with the input, or a non-breaking space from a
    /// word processor - are named outright.
    /// </remarks>
    private static string Describe(char character) => character switch
    {
        '\t' => "U+0009 (a tab)",
        '\n' => "U+000A (a line feed)",
        '\r' => "U+000D (a carriage return)",
        '\u00A0' => "U+00A0 (a non-breaking space)",
        _ when char.IsControl(character) || char.IsWhiteSpace(character) =>
            string.Create(CultureInfo.InvariantCulture, $"U+{(int)character:X4} (an invisible character)"),
        _ => string.Create(CultureInfo.InvariantCulture, $"'{character}'"),
    };

    /// <summary>
    /// Resolves a completed run of key presses to the character it selects and appends it.
    /// Does nothing when no run is pending.
    /// </summary>
    /// <remarks>
    /// This method is total. Presses cycle through the key's characters, as the specification
    /// describes, so any press count selects a character and there is no failure case here.
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

        string characters = Keypad.CharactersFor((char)key);

        // Pressing a key more times than it has characters wraps back to the first, so four
        // presses of "ABC" select 'A' again.
        text.Append(characters[(pressCount - 1) % characters.Length]);
    }

    /// <summary>
    /// Checks that the message is terminated by exactly one send key, in final position.
    /// </summary>
    /// <remarks>
    /// Running this check up front means the decode loop can treat the last character as known
    /// and never has to consider the send key or an unterminated message.
    /// </remarks>
    private static bool TryValidateTermination(string input, [NotNullWhen(false)] out string? errorMessage)
    {
        int sendKeyIndex = input.IndexOf(SendKey, StringComparison.Ordinal);

        if (sendKeyIndex < 0)
        {
            errorMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"Input must be terminated with the send key '{SendKey}'. Received \"{input}\".");

            return false;
        }

        if (sendKeyIndex != input.Length - 1)
        {
            errorMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"The send key '{SendKey}' must be the final character because it ends the message, but it " +
                $"was found at position {sendKeyIndex} of {input.Length}. A single call decodes exactly one " +
                $"complete message.");

            return false;
        }

        errorMessage = null;
        return true;
    }
}
