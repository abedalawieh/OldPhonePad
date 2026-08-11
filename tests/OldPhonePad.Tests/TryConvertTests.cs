namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// The non-throwing entry point.
/// </summary>
/// <remarks>
/// Invalid keypad input is ordinary traffic wherever a person types it, so a caller should be able
/// to ask for a decode without an exception being raised on the expected path. These tests pin the
/// two properties that make it useful: it never throws, and it explains itself.
/// </remarks>
public class TryConvertTests
{
    [Theory]
    [InlineData("33#", "E")]
    [InlineData("227*#", "B")]
    [InlineData("4433555 555666#", "HELLO")]
    [InlineData("8 88777444666*664#", "TURING")]
    [InlineData("222 2 22#", "CAB")]
    [InlineData("#", "")]
    public void TryConvert_WithValidInput_ReturnsTrueAndTheDecodedText(string input, string expected)
    {
        bool decoded = OldPhonePadConverter.TryConvert(input, out string? output, out string? errorMessage);

        Assert.True(decoded);
        Assert.Equal(expected, output);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("33", "no send key")]
    [InlineData("2222#0", "content after the send key")]
    [InlineData("2A#", "a character that is not on the keypad")]
    [InlineData("2\t2#", "whitespace that is not the pause")]
    [InlineData("", "nothing at all")]
    [InlineData(null, "null")]
    public void TryConvert_WithInvalidInput_ReturnsFalseAndExplainsWhy(string? input, string reason)
    {
        bool decoded = OldPhonePadConverter.TryConvert(input, out string? output, out string? errorMessage);

        Assert.False(decoded);
        Assert.Null(output);

        // The caller can show this to whoever typed the input, so it has to say something.
        Assert.False(string.IsNullOrWhiteSpace(errorMessage), $"No explanation given for {reason}.");
    }

    [Theory]
    [InlineData("33")]
    [InlineData("2222#0")]
    [InlineData("2A#")]
    [InlineData("")]
    [InlineData(null)]
    public void TryConvert_WithInvalidInput_DoesNotThrow(string? input)
    {
        // The whole point: a rejection is a result, not a failure. Nothing here should raise.
        Exception? raised = Record.Exception(
            () => OldPhonePadConverter.TryConvert(input, out _, out _));

        Assert.Null(raised);
    }

    [Theory]
    [InlineData("33")]
    [InlineData("2222#0")]
    [InlineData("2A#")]
    [InlineData("")]
    public void TryConvert_AndConvert_AgreeOnWhyInputWasRejected(string input)
    {
        // One decoder, one set of messages. If these ever diverge, the two entry points have
        // grown separate implementations.
        var thrown = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
        OldPhonePadConverter.TryConvert(input, out _, out string? errorMessage);

        Assert.StartsWith(errorMessage, thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("33#", "E")]
    [InlineData("4433555 555666#", "HELLO")]
    public void TryConvert_AndConvert_AgreeOnValidInput(string input, string expected)
    {
        OldPhonePadConverter.TryConvert(input, out string? output, out _);

        Assert.Equal(expected, output);
        Assert.Equal(OldPhonePadConverter.Convert(input), output);
    }

    [Fact]
    public void TryConvert_ViaTheInterface_BehavesTheSameWay()
    {
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        Assert.True(converter.TryConvert("33#", out string? output, out _));
        Assert.Equal("E", output);

        Assert.False(converter.TryConvert("33", out _, out string? errorMessage));
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }
}
