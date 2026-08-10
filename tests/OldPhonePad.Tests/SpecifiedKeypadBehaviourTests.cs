namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// Behaviour taken directly from the challenge specification: the keypad it pictures, and its
/// statement that pressing a key repeatedly cycles through the characters on it.
/// </summary>
public class SpecifiedKeypadBehaviourTests
{
    [Fact]
    public void Convert_WithTheSpecificationsCabExample_ReturnsCab()
    {
        // The specification gives this example in prose rather than in its example list:
        // "You must pause for a second in order to type two characters from the same button
        // after each other: '222 2 22' -> 'CAB'".
        Assert.Equal("CAB", OldPhonePadConverter.Convert("222 2 22#"));
    }

    [Theory]
    [InlineData("0#", " ")]
    [InlineData("1#", "&")]
    [InlineData("11#", "'")]
    [InlineData("111#", "(")]
    public void Convert_WithTheKeysPicturedOnTheSpecificationsKeypad_ReturnsTheirCharacters(
        string input,
        string expected)
    {
        // The keypad in the specification maps 0 to a space and 1 to the punctuation "&'(".
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Fact]
    public void Convert_WithKeyZero_ProducesASpaceBetweenWords()
    {
        // Key 0 types a space; a space in the input is a pause and types nothing. The two roles
        // are distinct, and this is the case that would expose confusing them.
        Assert.Equal("HI ALL", OldPhonePadConverter.Convert("44 44402555 555#"));
    }

    [Theory]
    [InlineData("2222#", "A")]      // three characters, pressed four times: back to the first
    [InlineData("22222#", "B")]     // and on to the second
    [InlineData("222222#", "C")]    // a full second cycle
    [InlineData("2222222#", "A")]   // and round again
    [InlineData("77777#", "P")]     // four characters, pressed five times
    [InlineData("00#", " ")]        // a single character key always yields that character
    public void Convert_WithMorePressesThanTheKeyHasCharacters_CyclesBackToTheFirst(
        string input,
        string expected)
    {
        // "pressing a button multiple times will cycle through the letters on it" - the
        // specification. A cycle has no upper bound, so this is never an error.
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Fact]
    public void Convert_WithCyclingFollowedByBackspace_RemovesTheCycledCharacter()
    {
        // Cycling produces an ordinary character, so backspace treats it like any other.
        Assert.Equal("", OldPhonePadConverter.Convert("2222*#"));
    }

    [Fact]
    public void Convert_WithCyclingAfterAPause_TreatsTheRunsSeparately()
    {
        // The pause ends the first run, so the second run's press count starts again at one.
        Assert.Equal("BA", OldPhonePadConverter.Convert("22 2222#"));
    }
}
