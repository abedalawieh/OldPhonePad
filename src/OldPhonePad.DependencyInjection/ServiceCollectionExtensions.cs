using IronSoftware.OldPhonePad;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Declared in the conventional namespace rather than the package's own, so that calling
// AddOldPhonePad() needs no extra using directive - the same convention ASP.NET Core follows
// for AddHttpContextAccessor and friends.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the old phone pad converter with a dependency injection container.
/// </summary>
public static class OldPhonePadServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOldPhonePadConverter"/> so it can be injected.
    /// </summary>
    /// <param name="services">The container to register with.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The converter is stateless and thread-safe, so a single instance serves every caller.
    /// </para>
    /// <para>
    /// Registration is conditional. Calling this twice registers one converter rather than two,
    /// and an application that has already registered its own implementation - a decorator that
    /// adds logging or caching, for example - keeps it. A library should add what is missing
    /// rather than override a decision its consumer has already made.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddOldPhonePad(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IOldPhonePadConverter, OldPhonePadConverter>();

        return services;
    }
}
