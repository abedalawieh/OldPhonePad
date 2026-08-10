namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// <see cref="OldPhonePadConverter.OldPhonePad(string)"/> exists so that code written against
/// the original challenge signature compiles unchanged. These tests exist to prove it stays a
/// pure delegation and never becomes a second implementation that can drift.
/// </summary>
public class ChallengeCompatibilityApiTests
{
    [Theory]
    [InlineData("33#", "E")]
    [InlineData("227*#", "B")]
    [InlineData("4433555 555666#", "HELLO")]
    [InlineData("8 88777444666*664#", "TURING")]
    public void OldPhonePad_WithOfficialChallengeExamples_ReturnsTheDocumentedResults(
        string input,
        string expected)
    {
        Assert.Equal(expected, OldPhonePadConverter.OldPhonePad(input));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("2 2#")]
    [InlineData("234**#")]
    [InlineData("9 99 999 9999#")]
    public void OldPhonePad_ForValidInput_ReturnsTheSameResultAsConvert(string input)
    {
        Assert.Equal(OldPhonePadConverter.Convert(input), OldPhonePadConverter.OldPhonePad(input));
    }

    [Fact]
    public void OldPhonePad_WithNullInput_ThrowsTheSameExceptionAsConvert()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => OldPhonePadConverter.OldPhonePad(null!));

        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [InlineData("2222#")]
    [InlineData("0#")]
    [InlineData("33")]
    [InlineData("33#999")]
    public void OldPhonePad_WithInvalidInput_ThrowsTheSameExceptionAsConvert(string input)
    {
        var fromConvert = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
        var fromCompatibilityMethod = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.OldPhonePad(input));

        Assert.Equal(fromConvert.Message, fromCompatibilityMethod.Message);
    }
}
