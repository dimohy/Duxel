using Duxel.Generated.Views;

namespace Duxel.Sample.FluentDsl;

internal static class NotificationActions
{
    internal static void Save(NotificationSettingsState state)
    {
        var email = state.Email.Value.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            state.Status.Value = "Enter an email address.";
            return;
        }

        state.Email.Value = email;
        state.Status.Value = state.EmailNotifications.Value
            ? $"Email notifications enabled for {email}."
            : "Email notifications are off.";
    }
}
