using Microsoft.Extensions.DependencyInjection;

namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// The registration helper shipped in the companion dependency injection package.
/// </summary>
/// <remarks>
/// These assert the behaviour a consuming application relies on: that the converter resolves,
/// that it is shared, and that calling the helper never overrides a decision the application has
/// already made for itself.
/// </remarks>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOldPhonePad_RegistersTheConverter()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddOldPhonePad()
            .BuildServiceProvider();

        var converter = provider.GetRequiredService<IOldPhonePadConverter>();

        Assert.Equal("HELLO", converter.Convert("4433555 555666#"));
    }

    [Fact]
    public void AddOldPhonePad_RegistersTheConverterAsASingleton()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddOldPhonePad()
            .BuildServiceProvider();

        var first = provider.GetRequiredService<IOldPhonePadConverter>();
        var second = provider.GetRequiredService<IOldPhonePadConverter>();

        // The converter is stateless, so one instance is enough for the whole application.
        Assert.Same(first, second);
    }

    [Fact]
    public void AddOldPhonePad_CalledTwice_RegistersOneConverter()
    {
        var services = new ServiceCollection();

        services.AddOldPhonePad();
        services.AddOldPhonePad();

        // Registering twice would mean two entries, and resolving IEnumerable<T> would surprise
        // the caller. A library's registration helper is expected to be safe to call again.
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOldPhonePadConverter));
    }

    [Fact]
    public void AddOldPhonePad_WhenTheApplicationHasAlreadyRegisteredOne_KeepsTheApplicationsChoice()
    {
        var services = new ServiceCollection();
        var applicationsOwn = new StubConverter();

        services.AddSingleton<IOldPhonePadConverter>(applicationsOwn);
        services.AddOldPhonePad();

        using ServiceProvider provider = services.BuildServiceProvider();

        // An application that wraps the converter - to add logging or caching, say - must not
        // have that quietly replaced by a library it calls afterwards.
        Assert.Same(applicationsOwn, provider.GetRequiredService<IOldPhonePadConverter>());
    }

    [Fact]
    public void AddOldPhonePad_WithNullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddOldPhonePad());
    }

    /// <summary>Stands in for an application's own implementation.</summary>
    private sealed class StubConverter : IOldPhonePadConverter
    {
        public string Convert(string input) => "STUBBED";
    }
}
