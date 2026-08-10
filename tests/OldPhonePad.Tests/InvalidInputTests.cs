using System.Globalization;

namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// Input the library rejects. Every case here is a deliberate decision to fail rather than
/// guess at the caller's intent.
/// </summary>
/// <remarks>
/// Exception messages are asserted on the facts they must contain - the offending key or
/// character and its position - rather than on whole sentences. The diagnostic information is
/// part of the contract; the exact wording is not, and asserting it would make these tests
/// break on harmless rewording.
/// </remarks>
public class InvalidInputTests
{
    [Fact]
    public void Convert_WithNullInput_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => OldPhonePadConverter.Convert(null!));

        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [InlineData("")]                // nothing at all
    [InlineData("33")]              // no send key
    [InlineData("4433555 555666")]  // a whole message with the send key forgotten
    public void Convert_WithoutSendKey_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
    }

    [Theory]
    [InlineData("33#999")]   // content after the send key
    [InlineData("33##")]     // duplicate send key
    [InlineData("#33#")]     // send key first as well as last
    [InlineData("#33")]      // send key first only
    [InlineData("2#2#2#")]   // several messages in one call
    public void Convert_WithSendKeyThatIsNotTheFinalCharacter_ThrowsArgumentException(string input)
    {
        // A single call decodes exactly one complete message. Silently discarding whatever
        // follows the send key would hide a caller's bug.
        Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
    }

    [Theory]
    [InlineData("0#", '0')]
    [InlineData("1#", '1')]
    [InlineData("4433550 555666#", '0')]
    public void Convert_WithKeyThatHasNoMappedCharacters_ThrowsArgumentExceptionNamingTheKey(
        string input,
        char unsupportedKey)
    {
        // The specification does not define what keys 0 and 1 produce, so they are rejected
        // rather than assigned characters borrowed from a particular handset.
        var exception = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));

        Assert.Contains(unsupportedKey.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [InlineData("2A#", "A")]      // letter
    [InlineData("2a#", "a")]      // lower case letter
    [InlineData("2-#", "-")]      // punctuation
    [InlineData("2.#", ".")]      // punctuation
    [InlineData("2+#", "+")]      // symbol
    [InlineData("2é#", "é")]   // non-ASCII letter
    public void Convert_WithUnsupportedCharacter_ThrowsArgumentExceptionNamingIt(
        string input,
        string unsupportedCharacter)
    {
        var exception = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));

        Assert.Contains(unsupportedCharacter, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2\t2#")]    // tab
    [InlineData("2\n2#")]    // line feed
    [InlineData("2\r2#")]    // carriage return
    [InlineData("2\u00A02#")]  // non-breaking space
    public void Convert_WithWhitespaceOtherThanAsciiSpace_ThrowsArgumentException(string input)
    {
        // Only U+0020 is the keypad pause. Treating other whitespace as a pause would mean
        // guessing that a stray tab or a line ending was intended as one.
        Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
    }

    [Theory]
    [InlineData("2222#", '2', 4)]        // a three character key pressed four times
    [InlineData("77777#", '7', 5)]       // a four character key pressed five times
    [InlineData("22 2222#", '2', 4)]     // over-pressed after a valid run
    [InlineData("2222*#", '2', 4)]       // rejected even though a backspace follows
    public void Convert_WithMorePressesThanTheKeyHasCharacters_ThrowsArgumentExceptionNamingTheKey(
        string input,
        char key,
        int pressCount)
    {
        // Cycling back to the first character is behaviour of particular handsets, not
        // something the specification defines, so excess presses are rejected instead.
        var exception = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));

        Assert.Contains(key.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(pressCount.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [InlineData("2A#")]
    [InlineData("2222#")]
    [InlineData("33")]
    [InlineData("0#")]
    public void Convert_WithInvalidInput_AlwaysNamesTheInputParameter(string input)
    {
        // Callers should be able to attribute the failure to the argument they passed.
        var exception = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));

        Assert.Equal("input", exception.ParamName);
    }
}
