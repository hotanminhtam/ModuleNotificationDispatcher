using System.Text.RegularExpressions;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Application.Validation;

/// <summary>
/// Provides validation logic for notifications.
/// </summary>
public static partial class NotificationValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?[1-9]\d{1,14}$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    public static (bool IsValid, string? ErrorMessage) Validate(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Destination))
            return (false, "Destination is required.");

        if (string.IsNullOrWhiteSpace(notification.Message))
            return (false, "Message content is required.");

        if (notification.Type == NotificationType.Email)
        {
            if (!EmailRegex().IsMatch(notification.Destination))
                return (false, "Invalid email format.");
        }
        else if (notification.Type == NotificationType.Sms)
        {
            if (!PhoneRegex().IsMatch(notification.Destination))
                return (false, "Invalid phone number format (E.164 recommended).");
        }

        return (true, null);
    }
}
