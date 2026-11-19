using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseAnnouncement(UiAccessibilityAnnouncement announcement)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
            return;
        var pointer = UiaNative.Pointer<IUiaSimple>(Root);
        nint message = 0, activity = 0;
        try
        {
            // These parameters are BSTRs, not zero-terminated LPWSTR arguments.
            message = Marshal.StringToBSTR(announcement.Message);
            activity = Marshal.StringToBSTR(announcement.ActivityId);
            var processing = announcement.Priority switch
            {
                UiAnnouncementPriority.Polite => 2, // NotificationProcessing_All
                UiAnnouncementPriority.Assertive => 0, // NotificationProcessing_ImportantAll
                _ => throw new ArgumentOutOfRangeException(nameof(announcement))
            };
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseNotificationEvent(pointer, 4, processing, message, activity));
        }
        finally
        {
            Marshal.FreeBSTR(message);
            Marshal.FreeBSTR(activity);
            Marshal.Release(pointer);
        }
    }
}

internal static partial class UiaNative
{
    [LibraryImport("UIAutomationCore.dll")]
    internal static partial int UiaRaiseNotificationEvent(nint provider, int kind, int processing,
        nint displayString, nint activityId);
}
