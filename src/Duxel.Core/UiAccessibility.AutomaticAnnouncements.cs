using System.Text;

namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    private sealed class AutomaticAnnouncementState
    {
        internal UiAnnouncementPriority? Published;
        internal UiAnnouncementPriority? Pending;
        internal ulong Seen;
    }
    private Dictionary<string, AutomaticAnnouncementState>? _automaticAnnouncements;
    private List<string>? _automaticAnnouncementRemovals;
    internal int AutomaticAnnouncementOwners => _automaticAnnouncements?.Count ?? 0;

    internal bool PrepareAutomaticAnnouncement(string owner, UiAnnouncementPriority? priority)
    {
        if (!_building || _notifying || _writerThread != Environment.CurrentManagedThreadId || !TryGetStaged(owner, out _))
            throw new InvalidOperationException("Automatic announcements require their owner in the current accessibility capture.");
        if (priority is { } value && value is not (UiAnnouncementPriority.Polite or UiAnnouncementPriority.Assertive))
            throw new ArgumentOutOfRangeException(nameof(priority));
        AutomaticAnnouncementState? state = null;
        _automaticAnnouncements?.TryGetValue(owner, out state);
        if (state is null)
        {
            if (priority is null)
                return false;
            state = new();
            (_automaticAnnouncements ??= new(StringComparer.Ordinal)).Add(owner, state);
        }
        var announce = priority is not null && (!_entries[owner].HasPublished || state.Published != priority);
        state.Pending = priority;
        state.Seen = _generation;
        return announce;
    }

    private void CompleteAutomaticAnnouncements(bool commit)
    {
        if (_automaticAnnouncements is null)
            return;
        foreach (var pair in _automaticAnnouncements)
        {
            var state = pair.Value;
            var remove = commit ? state.Seen != _generation || state.Pending is null || !_entries.ContainsKey(pair.Key)
                : state.Seen == _generation && state.Published is null;
            if (remove)
                (_automaticAnnouncementRemovals ??= []).Add(pair.Key);
            else if (commit)
                state.Published = state.Pending;
            else if (state.Seen == _generation)
            {
                state.Pending = state.Published;
                state.Seen = 0;
            }
        }
        if (_automaticAnnouncementRemovals is { } removed)
        {
            foreach (var key in removed)
                _automaticAnnouncements.Remove(key);
            removed.Clear();
        }
        if (_automaticAnnouncements.Count == 0)
            ReleaseAutomaticAnnouncements();
    }

    private void ReleaseAutomaticAnnouncements()
    {
        _automaticAnnouncements = null;
        _automaticAnnouncementRemovals = null;
    }

    /// <summary>Reads prepared accessible leaf text, excluding aggregate names and editable values.</summary>
    internal string PreparedLeafText(int start, int end)
    {
        if (!_building || _writerThread != Environment.CurrentManagedThreadId || start < 0 || end < start || end > _order.Count)
            throw new InvalidOperationException("Accessible text capture requires a valid current-frame range.");
        var parents = new HashSet<string>(StringComparer.Ordinal);
        for (var i = start; i < end; i++)
            parents.Add(_entries[_order[i]].Staged.ParentId);
        var text = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            var node = _entries[_order[i]].Staged;
            if (parents.Contains(node.Id) || node.SupportsValue || node.Role is UiAccessibilityRole.Group or UiAccessibilityRole.Pane
                or UiAccessibilityRole.Dialog or UiAccessibilityRole.Card or UiAccessibilityRole.Toolbar
                || string.IsNullOrWhiteSpace(node.Name))
                continue;
            if (text.Length > 0)
                text.Append(' ');
            text.Append(node.Name);
        }
        return text.ToString();
    }
}
