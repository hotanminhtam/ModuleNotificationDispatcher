using Microsoft.Extensions.DependencyInjection;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Infrastructure.Kafka;
using ModuleNotificationDispatcher.Infrastructure.Providers;

namespace ModuleNotificationDispatcher.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Infrastructure layer services (Kafka, Providers) to the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Notification providers
        services.AddSingleton<INotificationProvider, EmailNotificationProvider>();
        services.AddSingleton<INotificationProvider, SmsNotificationProvider>();

        // Kafka services
        services.AddTransient<KafkaConsumerService>();
        services.AddTransient<KafkaProducerHelper>();

        return services;
    }
}
