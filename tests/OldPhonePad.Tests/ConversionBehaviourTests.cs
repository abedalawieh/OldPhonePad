namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// Decoding behaviour for valid input: how key presses, pauses and backspace combine
/// to produce text.
/// </summary>
public class ConversionBehaviourTests
{
    [Theory]
    [InlineData("2#", "A")]
    [InlineData("3#", "D")]
    [InlineData("4#", "G")]
    [InlineData("5#", "J")]
    [InlineData("6#", "M")]
    [InlineData("7#", "P")]
    [InlineData("8#", "T")]
    [InlineData("9#", "W")]
    public void Convert_WithSinglePress_ReturnsFirstCharacterOfThatKey(string input, string expected)
    {
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Theory]
    [InlineData("7#", "P")]
    [InlineData("77#", "Q")]
    [InlineData("777#", "R")]
    [InlineData("7777#", "S")]
    public void Convert_WithRepeatedPresses_SelectsCharacterAtThatPosition(string input, string expected)
    {
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Theory]
    [InlineData("222#", "C")]      // last character of a three character key
    [InlineData("9999#", "Z")]     // last character of a four character key
    public void Convert_WithPressCountEqualToMappedCharacters_ReturnsLastCharacter(string input, string expected)
    {
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Fact]
    public void Convert_WithConsecutiveDifferentKeys_NeedsNoPauseBetweenThem()
    {
        // A different key ends the previous run on its own, so no pause is required.
        Assert.Equal("ADG", OldPhonePadConverter.Convert("234#"));
    }

    [Fact]
    public void Convert_WithPauseBetweenPressesOfTheSameKey_ProducesTwoCharacters()
    {
        Assert.Equal("BA", OldPhonePadConverter.Convert("22 2#"));
    }

    [Theory]
    [InlineData("2  2#", "AA")]        // several consecutive pauses
    [InlineData(" 2#", "A")]           // leading pause
    [InlineData("2 #", "A")]           // pause immediately before the send key
    [InlineData("  #", "")]            // pauses only
    public void Convert_WithPausesThatSeparateNothing_IgnoresThem(string input, string expected)
    {
        // A pause only ends the current run. It never produces a character of its own.
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }

    [Fact]
    public void Convert_WithBackspaceAfterText_RemovesTheLastCharacter()
    {
        Assert.Equal("AD", OldPhonePadConverter.Convert("234*#"));
    }

    [Fact]
    public void Convert_WithBackspaceFollowingAPause_RemovesTheCharacterCommittedByThePause()
    {
        // The pause commits 'A' before the backspace is read, so the backspace removes it.
        // This is the case that distinguishes "delete the committed character" from
        // "discard the pending key presses"; the two readings agree on every other input.
        Assert.Equal("", OldPhonePadConverter.Convert("2 *#"));
    }

    [Fact]
    public void Convert_WithBackspaceInterruptingAKeyPressRun_RemovesTheCharacterThatRunProduced()
    {
        // "22" produces 'B' when the backspace ends the run; the backspace then deletes it.
        Assert.Equal("", OldPhonePadConverter.Convert("22*#"));
    }

    [Fact]
    public void Convert_WithMultipleBackspaces_RemovesOneCharacterEach()
    {
        Assert.Equal("A", OldPhonePadConverter.Convert("234**#"));
    }

    [Theory]
    [InlineData("*#")]
    [InlineData("***#")]
    [InlineData("23***#")]     // more backspaces than characters produced
    public void Convert_WithBackspaceAndNothingToDelete_IsANoOperation(string input)
    {
        // Backspace on an empty display does nothing, exactly as it would on the handset.
        Assert.Equal("", OldPhonePadConverter.Convert(input));
    }

    [Fact]
    public void Convert_WithSendKeyOnly_ReturnsEmptyString()
    {
        Assert.Equal("", OldPhonePadConverter.Convert("#"));
    }

    [Theory]
    [InlineData("222 2 8#", "CAT")]
    [InlineData("44 444#", "HI")]
    [InlineData("84433 33#", "THEE")]
    [InlineData("2 22 222#", "ABC")]
    [InlineData("9 99 999 9999#", "WXYZ")]
    public void Convert_WithRealisticPhrases_DecodesThemCorrectly(string input, string expected)
    {
        Assert.Equal(expected, OldPhonePadConverter.Convert(input));
    }
}
