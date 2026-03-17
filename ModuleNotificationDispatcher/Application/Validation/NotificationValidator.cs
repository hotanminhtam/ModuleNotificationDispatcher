using System.Text.RegularExpressions;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Application.Validation;

/// <summary>
/// Provides validation logic for notifications.
/// </summary>
public static class NotificationValidator
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\+?[1-9]\d{1,14}$", RegexOptions.Compiled);

    public static (bool IsValid, string? ErrorMessage) Validate(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Destination))
            return (false, "Destination is required.");

        if (string.IsNullOrWhiteSpace(notification.Message))
            return (false, "Message content is required.");

        if (notification.Type == NotificationType.Email)
        {
            if (!EmailRegex.IsMatch(notification.Destination))
                return (false, "Invalid email format.");
        }
        else if (notification.Type == NotificationType.Sms)
        {
            if (!PhoneRegex.IsMatch(notification.Destination))
                return (false, "Invalid phone number format (E.164 recommended).");
        }

        return (true, null);
    }
}
