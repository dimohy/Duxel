namespace Duxel.Core;

/// <summary>An atomic copy of one published accessibility revision, including its sibling hierarchy and focus.</summary>
public sealed partial class UiAccessibilitySnapshot
{
    internal UiAccessibilitySnapshot(long revision, string? first, string? last, string? focus, UiAccessibilityElement[] elements)
    {
        Revision = revision;
        First = first;
        Last = last;
        Focus = focus;
        Elements = Array.AsReadOnly(elements);
    }

    public long Revision
    {
        get;
    }
    public string? First
    {
        get;
    }
    public string? Last
    {
        get;
    }
    public string? Focus
    {
        get;
    }
    public IReadOnlyList<UiAccessibilityElement> Elements
    {
        get;
    }
}

public sealed partial class UiAccessibilityTree
{
    /// <summary>Raised once after a changed revision is fully published, outside reader locks. Unchanged frames do not notify.</summary>
    public event Action? Published;

    public UiAccessibilitySnapshot CaptureSnapshot()
    {
        _gate.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var elements = new UiAccessibilityElement[_published.Count];
            _published.Values.CopyTo(elements, 0);
            return new(Revision, _first, _last, _focus, elements);
        }
        finally { _gate.ExitReadLock(); }
    }

    private void NotifyPublication()
    {
        var observer = Published;
        if (observer is null)
            return;
        _notifying = true;
        try
        {
            observer();
        }
        finally { _notifying = false; }
    }
}
