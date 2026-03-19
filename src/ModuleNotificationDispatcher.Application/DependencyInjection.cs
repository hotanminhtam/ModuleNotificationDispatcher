using Microsoft.Extensions.DependencyInjection;
using ModuleNotificationDispatcher.Application.Dispatcher;

namespace ModuleNotificationDispatcher.Application;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services (NotificationDispatcher) to the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<NotificationDispatcher>();
        return services;
    }
}
