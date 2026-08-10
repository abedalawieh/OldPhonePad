namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// The four worked examples given in the challenge. They are the stated contract for this
/// library, so each one is a dedicated test: if any of these fails, the library is wrong
/// regardless of what every other test says.
/// </summary>
public class OfficialChallengeExampleTests
{
    [Fact]
    public void Convert_WithOfficialExample_33_ReturnsE()
    {
        string result = OldPhonePadConverter.Convert("33#");

        Assert.Equal("E", result);
    }

    [Fact]
    public void Convert_WithOfficialExample_227Backspace_ReturnsB()
    {
        string result = OldPhonePadConverter.Convert("227*#");

        Assert.Equal("B", result);
    }

    [Fact]
    public void Convert_WithOfficialExample_Hello_ReturnsHello()
    {
        string result = OldPhonePadConverter.Convert("4433555 555666#");

        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Convert_WithOfficialExample_Turing_ReturnsTuring()
    {
        string result = OldPhonePadConverter.Convert("8 88777444666*664#");

        Assert.Equal("TURING", result);
    }
}
