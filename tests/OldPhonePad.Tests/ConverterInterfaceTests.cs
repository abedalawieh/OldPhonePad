namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// <see cref="IOldPhonePadConverter"/> exists so a consuming application can depend on the
/// contract rather than the concrete type. These tests prove it stays a pure delegation to
/// <see cref="OldPhonePadConverter.Convert(string)"/> and never becomes a second implementation
/// that can drift.
/// </summary>
/// <remarks>
/// Every converter here is deliberately typed as the interface. That is the surface a consuming
/// application binds to, and it is the only way to reach the implementation, which is explicit.
/// </remarks>
public class ConverterInterfaceTests
{
    [Theory]
    [InlineData("33#", "E")]
    [InlineData("227*#", "B")]
    [InlineData("4433555 555666#", "HELLO")]
    [InlineData("8 88777444666*664#", "TURING")]
    public void Convert_ViaTheInterface_ReturnsTheDocumentedResults(string input, string expected)
    {
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        Assert.Equal(expected, converter.Convert(input));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("2 2#")]
    [InlineData("234**#")]
    [InlineData("9 99 999 9999#")]
    public void Convert_ViaTheInterface_ReturnsTheSameResultAsTheStaticMethod(string input)
    {
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        Assert.Equal(OldPhonePadConverter.Convert(input), converter.Convert(input));
    }

    [Theory]
    [InlineData("2A#")]
    [InlineData("2\t2#")]
    [InlineData("33")]
    [InlineData("33#999")]
    public void Convert_ViaTheInterface_ThrowsTheSameExceptionAsTheStaticMethod(string input)
    {
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        var fromStatic = Assert.Throws<ArgumentException>(() => OldPhonePadConverter.Convert(input));
        var fromInterface = Assert.Throws<ArgumentException>(() => converter.Convert(input));

        Assert.Equal(fromStatic.Message, fromInterface.Message);
    }

    [Fact]
    public void Convert_ViaTheInterface_WithNullInput_ThrowsArgumentNullException()
    {
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        var exception = Assert.Throws<ArgumentNullException>(() => converter.Convert(null!));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void Converter_ReusedAcrossCalls_CarriesNoStateBetweenThem()
    {
        // The API registers this type as a singleton, so one instance serves every request.
        // Decoding a message must not be affected by whatever was decoded before it.
        IOldPhonePadConverter converter = new OldPhonePadConverter();

        Assert.Equal("HELLO", converter.Convert("4433555 555666#"));
        Assert.Equal("E", converter.Convert("33#"));
        Assert.Throws<ArgumentException>(() => converter.Convert("2A#"));
        Assert.Equal("HELLO", converter.Convert("4433555 555666#"));
    }
}
