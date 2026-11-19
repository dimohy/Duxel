namespace Duxel.Core;

/// <summary>A stable ownership key and its actual widget instance.</summary>
public readonly record struct UiKeyedWidget(string Key, UiWidget Widget);

/// <summary>Shares validated exclusive ownership across physical multi-child layout widgets.</summary>
public abstract class UiMultiChildWidget : UiControlWidget
{
    private UiWidget[] _children = [];
    private protected uint ChildSnapshotRevision { get; private set; }
    protected UiMultiChildWidget(UiWidget[] children, UiWidgetIdentity identity = UiWidgetIdentity.Instance,
        string[]? childKeys = null) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (childKeys is not null && childKeys.Length != children.Length)
            throw new ArgumentException("Each child requires exactly one ownership key.", nameof(childKeys));
        var entries = new UiKeyedWidget[children.Length];
        for (var i = 0; i < children.Length; i++)
            entries[i] = new(childKeys is null ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : childKeys[i], children[i]);
        SetOwnedChildSnapshot(entries);
    }

    /// <summary>Changes physical traversal after validating all keys and ownership; retirement is explicit.</summary>
    protected void SetOwnedChildSnapshot(ReadOnlySpan<UiKeyedWidget> entries)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var widgets = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance);
        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Widget);
            if (!keys.Add(entry.Key) || !widgets.Add(entry.Widget))
                throw new ArgumentException("Child ownership keys and instances must be unique.", nameof(entries));
            ValidateOwnedChild(entry.Key, entry.Widget);
        }
        var children = new UiWidget[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            OwnChild(entries[i].Key, entries[i].Widget);
            children[i] = entries[i].Widget;
        }
        _children = children;
        unchecked
        {
            ChildSnapshotRevision++;
        }
    }
    public ReadOnlySpan<UiWidget> Children => _children;
    /// <summary>Opts into arrangement from constraints, configured typography and invalidated properties.</summary>
    protected virtual bool CanRetainArrangement => false;
    protected bool ChildrenRetainMeasurement
    {
        get
        {
            foreach (var child in _children)
            if (!child.RetainsMeasurement)
                return false;
            return true;
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _children = [];
        base.ReleaseOwnedResources();
    }
}
