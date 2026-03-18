namespace ModuleNotificationDispatcher.Domain.Exceptions;

/// <summary>
/// Thrown when a notification fails to be delivered through its channel.
/// </summary>
public class NotificationDeliveryException : Exception
{
    /// <summary>
    /// Initializes a new instance with a descriptive message.
    /// </summary>
    /// <param name="message">The reason for the delivery failure.</param>
    public NotificationDeliveryException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with a descriptive message and inner exception.
    /// </summary>
    /// <param name="message">The reason for the delivery failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public NotificationDeliveryException(string message, Exception innerException)
        : base(message, innerException) { }
}
