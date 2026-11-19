using System.Globalization;

namespace Duxel.Core;

public enum UiToastIntent { Info, Success, Warning, Error }
public enum UiToastStatus { Queued, Entering, Visible, Exiting }
[Flags]
public enum UiToastPauseReason { None = 0, Hover = 1, Focus = 2, WindowInactive = 4, Explicit = 8 }

public readonly record struct UiToastProgress(float? Value, string? StatusText = null)
{
    internal void Validate()
    {
        if (Value is { } value && (!float.IsFinite(value) || value < 0 || value > 1)) throw new ArgumentOutOfRangeException(nameof(Value));
        if (StatusText is not null) new UiAccessibilityAnnouncement(StatusText, "toast-progress").Validate();
    }
}

public sealed record UiToastOptions(string Title, string? Body = null, UiToastIntent Intent = UiToastIntent.Info)
{
    public bool DisableAnnouncement { get; init; }
    public UiToastProgress? Progress { get; init; }
    public bool CanDismiss { get; init; }
    public string CloseLabel { get; init; } = "Close";
    public string? ActionLabel { get; init; }
    public Action? Action { get; init; }
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(7);
    public int Priority { get; init; }
    public bool PauseOnHover { get; init; } = true;
    public bool PauseOnWindowBlur { get; init; } = true;
    public UiAnnouncementPriority AnnouncementPriority => Intent == UiToastIntent.Info
        ? UiAnnouncementPriority.Polite : UiAnnouncementPriority.Assertive;
    internal void Validate()
    {
        Progress?.Validate();
        if (Progress is not null && Timeout is not null) throw new ArgumentException("Progress notifications require Timeout = null.");
        new UiAccessibilityAnnouncement(CloseLabel, "toast-close").Validate();
        if ((ActionLabel is null) != (Action is null)) throw new ArgumentException("Toast action label and callback must be supplied together.");
        if (ActionLabel is not null) new UiAccessibilityAnnouncement(ActionLabel, "toast-action").Validate();
        if ((uint)Intent > 3) throw new ArgumentOutOfRangeException(nameof(Intent));
        if (Timeout is { } timeout && timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Timeout));
        new UiAccessibilityAnnouncement(Title, "toast").Validate();
        if (!string.IsNullOrEmpty(Body)) new UiAccessibilityAnnouncement(Body, "toast-body").Validate();
    }
}

public sealed record UiToastSnapshot(string Id, UiToastOptions Options, UiToastStatus Status,
    UiToastPauseReason PauseReasons, ulong Revision);

/// <summary>UI-thread-owned notification lifecycle. The host supplies enter/exit completion and schedules NextTimeout.</summary>
public sealed partial class UiToastController
{
    private sealed class Entry(string id, UiToastOptions options, UiToastStatus status, long order)
    {
        internal readonly string Id = id;
        internal UiToastOptions Options = options;
        internal UiToastStatus Status = status;
        internal UiToastPauseReason Paused;
        internal ulong Revision;
        internal readonly long Order = order;
        internal decimal? Deadline;
        internal long? Remaining = options.Timeout?.Ticks;
        internal UiToastSnapshot Snapshot() => new(Id, Options, Status, Paused, Revision);
    }
    private readonly List<Entry> _entries = [];
    private readonly TimeProvider _clock;
    private readonly long _origin;
    private long _lastTime, _sequence;
    private bool _notifying;
    private int _viewportCapacity;
    private IReadOnlyList<UiToastSnapshot> _visible = Array.AsReadOnly(Array.Empty<UiToastSnapshot>());
    private IReadOnlyList<UiToastSnapshot> _queued = Array.AsReadOnly(Array.Empty<UiToastSnapshot>());
    public int Limit { get; }
    public IReadOnlyList<UiToastSnapshot> Visible => _visible;
    public IReadOnlyList<UiToastSnapshot> Queued => _queued;
    public event Action? Changed;
    public UiToastController(int limit = 4, TimeProvider? clock = null)
    {
        if (limit is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(limit));
        Limit = limit;
        _viewportCapacity = limit;
        _clock = clock ?? TimeProvider.System;
        _origin = _clock.GetTimestamp();
    }
    private long ReadTime()
    {
        var now = _clock.GetElapsedTime(_origin, _clock.GetTimestamp()).Ticks;
        if (now < _lastTime) throw new InvalidOperationException("The toast clock must be monotonic.");
        _lastTime = now;
        return now;
    }
    private long BeginChange()
    {
        if (_notifying) throw new InvalidOperationException("Toast mutations cannot reenter a Changed notification.");
        return ReadTime();
    }
    private void Publish()
    {
        _visible = Array.AsReadOnly(_entries.Where(e => e.Status != UiToastStatus.Queued).Reverse().Select(e => e.Snapshot()).ToArray());
        _queued = Array.AsReadOnly(_entries.Where(e => e.Status == UiToastStatus.Queued)
            .OrderByDescending(e => e.Options.Priority).ThenBy(e => e.Order).Select(e => e.Snapshot()).ToArray());
        _notifying = true;
        try { Changed?.Invoke(); }
        finally
        {
            _notifying = false;
            CompleteDeferredHostRelease();
        }
    }
    public string Dispatch(UiToastOptions options, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (id is not null) new UiAccessibilityAnnouncement("toast", id).Validate();
        BeginChange();
        if (id is not null && _entries.Any(e => e.Id == id)) throw new ArgumentException("Toast IDs must be distinct.", nameof(id));
        var order = checked(++_sequence);
        if (id is null)
        {
            id = "toast-" + order.ToString(CultureInfo.InvariantCulture);
            while (_entries.Any(e => e.Id == id))
                id = "toast-" + checked(++_sequence).ToString(CultureInfo.InvariantCulture);
        }
        var status = _visible.Count < _viewportCapacity ? UiToastStatus.Entering : UiToastStatus.Queued;
        _entries.Add(new(id, options, status, order));
        Publish();
        return id;
    }
    public bool MarkVisible(string id)
    {
        var now = BeginChange();
        var entry = _entries.Find(e => e.Id == id);
        if (entry is null || entry.Status is UiToastStatus.Exiting or UiToastStatus.Visible) return false;
        if (entry.Status != UiToastStatus.Entering) throw new InvalidOperationException("A queued toast cannot finish entering.");
        entry.Status = UiToastStatus.Visible;
        if (entry.Paused == UiToastPauseReason.None && entry.Remaining is { } remaining) entry.Deadline = (decimal)now + remaining;
        Publish();
        return true;
    }
    public bool Update(string id, UiToastOptions options, bool restartTimer = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var now = BeginChange();
        var entry = _entries.Find(e => e.Id == id);
        if (entry is null || entry.Status == UiToastStatus.Exiting) return false;
        if (entry.Options == options && !restartTimer) return false;
        var reset = restartTimer || entry.Options.Timeout != options.Timeout;
        var pause = entry.Paused;
        if (!options.PauseOnHover) pause &= ~UiToastPauseReason.Hover;
        if (!options.PauseOnWindowBlur) pause &= ~UiToastPauseReason.WindowInactive;
        if (entry.Deadline is { } deadline) entry.Remaining = (long)Math.Max(0, deadline - now);
        entry.Deadline = null;
        entry.Options = options;
        entry.Paused = pause;
        entry.Revision++;
        if (reset) entry.Remaining = options.Timeout?.Ticks;
        if (entry.Status == UiToastStatus.Visible && pause == UiToastPauseReason.None && entry.Remaining is { } remaining)
            entry.Deadline = (decimal)now + remaining;
        Publish();
        return true;
    }
    public bool SetPaused(string id, UiToastPauseReason reason, bool paused)
    {
        if (reason == UiToastPauseReason.None || (reason & ~(UiToastPauseReason.Hover | UiToastPauseReason.Focus
            | UiToastPauseReason.WindowInactive | UiToastPauseReason.Explicit)) != 0) throw new ArgumentOutOfRangeException(nameof(reason));
        var now = BeginChange();
        var entry = _entries.Find(e => e.Id == id);
        if (entry is null || entry.Status == UiToastStatus.Exiting) return false;
        if (paused && !entry.Options.PauseOnHover) reason &= ~UiToastPauseReason.Hover;
        if (paused && !entry.Options.PauseOnWindowBlur) reason &= ~UiToastPauseReason.WindowInactive;
        var next = paused ? entry.Paused | reason : entry.Paused & ~reason;
        if (next == entry.Paused) return false;
        if (entry.Deadline is { } deadline) entry.Remaining = (long)Math.Max(0, deadline - now);
        entry.Deadline = null;
        entry.Paused = next;
        if (entry.Status == UiToastStatus.Visible && next == UiToastPauseReason.None && entry.Remaining is { } remaining)
            entry.Deadline = (decimal)now + remaining;
        Publish();
        return true;
    }
    public TimeSpan? NextTimeout
    {
        get
        {
            var now = ReadTime();
            decimal? deadline = null;
            foreach (var entry in _entries)
                if (entry.Deadline is { } value && (deadline is null || value < deadline)) deadline = value;
            return deadline is { } next ? TimeSpan.FromTicks((long)Math.Max(0, next - now)) : null;
        }
    }
    public bool Advance()
    {
        var now = BeginChange();
        var changed = false;
        foreach (var entry in _entries)
            if (entry.Status == UiToastStatus.Visible && entry.Deadline is { } deadline && deadline <= now)
            {
                entry.Status = UiToastStatus.Exiting;
                entry.Deadline = null;
                changed = true;
            }
        if (changed) Publish();
        return changed;
    }
    public bool Dismiss(string id)
    {
        BeginChange();
        var entry = _entries.Find(e => e.Id == id);
        if (entry is null || entry.Status == UiToastStatus.Exiting) return false;
        if (entry.Status == UiToastStatus.Queued) _entries.Remove(entry);
        else { entry.Status = UiToastStatus.Exiting; entry.Deadline = null; }
        Publish();
        return true;
    }
    public bool Remove(string id)
    {
        BeginChange();
        var entry = _entries.Find(e => e.Id == id);
        if (entry is null) return false;
        if (entry.Status != UiToastStatus.Exiting) throw new InvalidOperationException("Only a completed exit can remove a toast.");
        _entries.Remove(entry);
        FillVisibleSlots();
        Publish();
        return true;
    }
    private void FillVisibleSlots()
    {
        while (_entries.Count(e => e.Status != UiToastStatus.Queued) < _viewportCapacity)
        {
            var next = _entries.Where(e => e.Status == UiToastStatus.Queued)
                .OrderByDescending(e => e.Options.Priority).ThenBy(e => e.Order).FirstOrDefault();
            if (next is null) break;
            next.Status = UiToastStatus.Entering;
        }
    }
    internal bool SetViewportCapacity(int capacity, string? keepVisible = null)
    {
        if (capacity < 0 || capacity > Limit) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (_viewportCapacity == capacity) return false;
        var now = BeginChange();
        _viewportCapacity = capacity;
        var visible = _entries.Where(entry => entry.Status != UiToastStatus.Queued)
            .OrderByDescending(entry => entry.Status == UiToastStatus.Exiting)
            .ThenByDescending(entry => entry.Id == keepVisible).ThenByDescending(entry => entry.Order).ToArray();
        foreach (var entry in visible.Skip(capacity))
        {
            // An exit retains its slot until its existing motion completes.
            if (entry.Status == UiToastStatus.Exiting) continue;
            if (entry.Deadline is { } deadline) entry.Remaining = (long)Math.Max(0, deadline - now);
            entry.Deadline = null;
            entry.Status = UiToastStatus.Queued;
            entry.Paused &= ~(UiToastPauseReason.Hover | UiToastPauseReason.Focus);
        }
        FillVisibleSlots();
        Publish();
        return true;
    }
    public bool DismissAll()
    {
        BeginChange();
        if (_entries.Count == 0 || _entries.All(e => e.Status == UiToastStatus.Exiting)) return false;
        _entries.RemoveAll(e => e.Status == UiToastStatus.Queued);
        foreach (var entry in _entries) { entry.Status = UiToastStatus.Exiting; entry.Deadline = null; }
        Publish();
        return true;
    }
}
