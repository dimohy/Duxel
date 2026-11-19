using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private List<UiAccessibilityAnnouncement>? _pendingAnnouncements;

    private void QueueAnnouncement(UiAccessibilityAnnouncement announcement)
    {
        lock (_gate)
        {
            if (_stopping)
                return;
            (_pendingAnnouncements ??= []).Add(announcement);
        }
        RequestRefresh();
    }

    private void PublishAnnouncements()
    {
        List<UiAccessibilityAnnouncement>? batch;
        lock (_gate)
        {
            batch = _pendingAnnouncements;
            _pendingAnnouncements = null;
        }
        if (batch is null)
            return;
        foreach (var announcement in batch)
            Event(WindowPath, "Announcement", string.Empty, (int)announcement.Priority,
                GVariant.String(announcement.Message));
    }
}
