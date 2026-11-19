namespace Duxel.Core;

public enum UiAnnouncementPriority
{
    Polite = 1,
    Assertive = 2
}

/// <summary>An explicit announcement, independent of focus and visual tree changes.</summary>
public readonly record struct UiAccessibilityAnnouncement(string Message, string ActivityId,
    UiAnnouncementPriority Priority = UiAnnouncementPriority.Polite)
{
    public void Validate()
    {
        ValidateText(Message, nameof(Message));
        ValidateText(ActivityId, nameof(ActivityId));
        if (Priority is not (UiAnnouncementPriority.Polite or UiAnnouncementPriority.Assertive))
            throw new ArgumentOutOfRangeException(nameof(Priority));
    }

    private static void ValidateText(string text, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, parameter);
        for (var index = 0; index < text.Length; index++)
        {
            var value = text[index];
            if (value == '\0')
                throw new ArgumentException("Announcement text cannot contain NUL.", parameter);
            if (!char.IsSurrogate(value))
                continue;
            if (!char.IsHighSurrogate(value) || index + 1 == text.Length || !char.IsLowSurrogate(text[index + 1]))
                throw new ArgumentException("Announcement text must contain well-formed UTF-16.", parameter);
            index++;
        }
    }
}

public sealed partial class UiAccessibilityTree
{
    private List<UiAccessibilityAnnouncement>? _announcements;
    public event Action<UiAccessibilityAnnouncement>? Announcement;

    internal void QueueAnnouncement(UiAccessibilityAnnouncement announcement)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        announcement.Validate();
        if (!_building || _notifying || _writerThread != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Announcements require the active accessibility render writer.");
        (_announcements ??= []).Add(announcement);
    }

    private void ClearAnnouncements()
    {
        if (_announcements?.Capacity > 256)
            _announcements = null;
        else
            _announcements?.Clear();
    }

    private void PublishAnnouncements()
    {
        try
        {
            NotifyPropertyChanges(Announcement, _announcements);
        }
        finally { ClearAnnouncements(); }
    }
}

public sealed partial class UiImmediateContext
{
    /// <summary>Queues a frame-local announcement. True means queued, not that an assistive client spoke it.</summary>
    public bool Announce(string message, string activityId, UiAnnouncementPriority priority = UiAnnouncementPriority.Polite)
    {
        EnsureNotMeasuring();
        var announcement = new UiAccessibilityAnnouncement(message, activityId, priority);
        announcement.Validate();
        if (_accessibility is not { } tree)
            return false;
        tree.QueueAnnouncement(announcement with
        {
            ActivityId = ResolveId(activityId)
        });
        return true;
    }
}

public readonly partial struct UiControlContext
{
    public bool Announce(string message, string activityId, UiAnnouncementPriority priority = UiAnnouncementPriority.Polite) =>
        _ui.Announce(message, activityId, priority);
}
