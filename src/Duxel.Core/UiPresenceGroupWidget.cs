namespace Duxel.Core;

public sealed record UiPresenceGroupOptions(float Gap = 0, bool AnimateEnter = false, bool ReducedMotion = false)
{
    public UiTransitionOptions Fade { get; init; } = new(.25, UiAnimationEasing.Linear);
    public UiTransitionOptions Reflow { get; init; } = new(.25, UiAnimationEasing.OutCubic);
    internal bool CollapseExitSpace { get; init; }
    internal void Validate()
    {
        if (!float.IsFinite(Gap) || Gap < 0)
            throw new ArgumentOutOfRangeException(nameof(Gap));
        Fade.Validate();
        Reflow.Validate();
    }
}

/// <summary>Owns real children, controlled presence, frozen exit paint and finite vertical reflow.</summary>
[UiComponent("PresenceGroup", UiComponentExposure.Infrastructure)]
public class UiPresenceGroupWidget : UiArrangedChildrenWidget
{
    private sealed class Slot(UiKeyedWidget item, bool initialContent) : IDisposable
    {
        internal readonly UiKeyedWidget Item = item;
        internal readonly bool InitialContent = initialContent;
        internal bool Requested = true;
        internal bool Visible = true;
        internal bool Live;
        internal bool Initialized;
        internal bool PreviousVisible;
        internal UiFloatTransition Opacity;
        internal UiFloatTransition Y;
        internal UiFloatTransition Offset;
        internal float OffsetValue;
        internal UiVector2 Natural;
        internal UiPresencePaint Paint = new();
        internal float Alpha;
        public void Dispose() => Paint.Dispose();
    }
    private Slot[] _slots;
    private readonly Dictionary<string, Slot> _owned = new(StringComparer.Ordinal);
    private UiKeyedWidget[] _items;
    private HashSet<string> _requestedKeys;
    private bool _structureDirty;
    private bool _hasRendered;
    private int _preparedFrame = int.MinValue;
    private UiPresenceGroupOptions _options;
    private double? _nextFrame;
    internal UiRect[] LastBounds
    {
        get; private set;
    }
    internal float OpacityAt(int index) => _slots[index].Alpha;
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainPreparedArrangement => _nextFrame is null;

    public UiPresenceGroupWidget(UiWidget[] children, UiPresenceGroupOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance, string[]? keys = null) : base(Validate(children, options), identity, keys)
    {
        _options = options ?? new();
        _items = children.Select((child, index) => new UiKeyedWidget(
            keys is null ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : keys[index], child)).ToArray();
        _slots = _items.Select(item => new Slot(item, initialContent: true)).ToArray();
        foreach (var slot in _slots)
            _owned.Add(slot.Item.Key, slot);
        _requestedKeys = new(_owned.Keys, StringComparer.Ordinal);
        LastBounds = new UiRect[children.Length];
    }

    /// <summary>The requested live collection; exiting paint may still occupy the physical traversal snapshot.</summary>
    public ReadOnlySpan<UiKeyedWidget> Items
    {
        get
        {
            ThrowIfDisposed();
            return _items;
        }
    }

    protected virtual void ValidateItem(UiWidget widget)
    {
    }

    /// <summary>Adopts a validated collection now and applies its traversal at the next unprepared frame.</summary>
    public void SetItems(ReadOnlySpan<UiKeyedWidget> items)
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var widgets = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance);
        var same = items.Length == _items.Length;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            ArgumentException.ThrowIfNullOrEmpty(item.Key);
            ArgumentNullException.ThrowIfNull(item.Widget);
            if (!keys.Add(item.Key) || !widgets.Add(item.Widget))
                throw new ArgumentException("Presence keys and widget instances must be unique.", nameof(items));
            ValidateItem(item.Widget);
            ValidateOwnedChild(item.Key, item.Widget);
            same &= i < _items.Length && _items[i].Key == item.Key && ReferenceEquals(_items[i].Widget, item.Widget);
        }
        if (same)
            return;
        var next = items.ToArray();
        foreach (var item in next)
        {
            if (_owned.ContainsKey(item.Key))
                continue;
            OwnChild(item.Key, item.Widget);
            _owned.Add(item.Key, new(item, initialContent: !_hasRendered));
        }
        _items = next;
        _requestedKeys = keys;
        _structureDirty = true;
        RequestRebuild();
    }

    protected override void PrepareChildren(UiControlContext context)
    {
        if (_preparedFrame == context.LayoutFrame)
            return;
        _preparedFrame = context.LayoutFrame;
        if (!_structureDirty)
            return;
        var next = _items.Select(item => _owned[item.Key]).ToList();
        // Exits retain their relative anchor before the next surviving old sibling.
        for (var i = 0; i < _slots.Length; i++)
        {
            var exit = _slots[i];
            if (_requestedKeys.Contains(exit.Item.Key))
                continue;
            var insertion = next.Count;
            for (var j = i + 1; j < _slots.Length; j++)
                if (_requestedKeys.Contains(_slots[j].Item.Key))
                {
                    insertion = next.FindIndex(slot => ReferenceEquals(slot, _slots[j]));
                    break;
                }
            next.Insert(insertion, exit);
        }
        foreach (var slot in _owned.Values)
            if (!next.Contains(slot))
                next.Add(slot);
        foreach (var slot in next)
            slot.Requested = _requestedKeys.Contains(slot.Item.Key);
        _slots = next.ToArray();
        SetOwnedChildSnapshot(_slots.Select(slot => slot.Item).ToArray());
        LastBounds = new UiRect[_slots.Length];
        _structureDirty = false;
    }
    private static UiWidget[] Validate(UiWidget[] children, UiPresenceGroupOptions? options)
    {
        ArgumentNullException.ThrowIfNull(children);
        options?.Validate();
        return children;
    }
    public UiPresenceGroupOptions Options
    {
        get
        {
            ThrowIfDisposed();
            return _options;
        }
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public bool IsVisible(int index)
    {
        ThrowIfDisposed();
        return IsVisible(_items[index].Key);
    }
    public bool IsVisible(string key)
    {
        ThrowIfDisposed();
        return RequestedSlot(key).Visible;
    }
    public void SetVisible(int index, bool visible)
    {
        ThrowIfDisposed();
        SetVisible(_items[index].Key, visible);
    }
    private Slot RequestedSlot(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!_requestedKeys.Contains(key))
            throw new KeyNotFoundException("The key is not in the requested presence collection.");
        return _owned[key];
    }
    public void SetVisible(string key, bool visible)
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        var slot = RequestedSlot(key);
        if (slot.Visible == visible)
            return;
        slot.Visible = visible;
        RequestRebuild();
    }
    private void Schedule(double? next)
    {
        if (next is { } time)
            _nextFrame = _nextFrame is { } previous ? Math.Min(previous, time) : time;
    }
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        _nextFrame = null;
        var fade = _options.Fade with
        {
            ReducedMotion = _options.ReducedMotion || _options.Fade.ReducedMotion
        };
        var reflow = _options.Reflow with
        {
            ReducedMotion = _options.ReducedMotion || _options.Reflow.ReducedMotion
        };
        var width = 0f;
        var height = 0f;
        var targetY = 0f;
        var count = 0;
        for (var i = 0; i < Children.Length; i++)
        {
            var slot = _slots[i];
            var wasVisible = slot.PreviousVisible;
            var visible = slot.Visible && slot.Requested;
            slot.Live = visible;
            if (!slot.Initialized)
            {
                // Initial page content never acquires an entry animation.
                slot.Opacity.Reset(visible && (slot.InitialContent || !_options.AnimateEnter) ? 1 : 0);
                slot.Initialized = true;
            }
            else if (visible && !slot.PreviousVisible && !_options.AnimateEnter)
                slot.Opacity.Reset(1);
            var alpha = slot.Opacity.Update(visible ? 1 : 0, context.TimeSeconds, fade);
            slot.Alpha = alpha.Value;
            slot.PreviousVisible = visible;
            Schedule(alpha.NextFrameTimeSeconds);
            if (visible)
                slot.Natural = context.MeasureConstrained(Children[i], new(0, context.LayoutConstraints.MaxWidth, 0, float.PositiveInfinity));
            if (visible)
            {
                if (!wasVisible && _options.AnimateEnter && alpha.Value == 0)
                    slot.Offset.Reset(-slot.Natural.Y);
                var offset = slot.Offset.Update(0, context.TimeSeconds, fade);
                slot.OffsetValue = offset.Value;
                Schedule(offset.NextFrameTimeSeconds);
            }
            else if (wasVisible)
                slot.Offset.Reset(slot.OffsetValue);
            if (!visible && !alpha.Running)
            {
                bounds[i] = default;
                continue;
            }
            if (count++ > 0)
                targetY += _options.Gap;
            var y = slot.Y.Update(targetY, context.TimeSeconds, reflow);
            Schedule(y.NextFrameTimeSeconds);
            bounds[i] = new(0, y.Value + slot.OffsetValue, slot.Natural.X, slot.Natural.Y);
            width = MathF.Max(width, slot.Natural.X);
            var reservedHeight = !visible && _options.CollapseExitSpace ? slot.Natural.Y * slot.Alpha : slot.Natural.Y;
            height = MathF.Max(height, y.Value + reservedHeight);
            targetY += reservedHeight;
        }
        bounds.CopyTo(LastBounds);
        return context.LayoutConstraints.Constrain(new(width, height));
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        _hasRendered = true;
        for (var i = 0; i < Children.Length; i++)
        {
            var slot = _slots[i];
            var bounds = layout[i] with
            {
                X = root.X + layout[i].X,
                Y = root.Y + layout[i].Y
            };
            ui.RenderPresenceChild(Children[i], slot.Paint, bounds, root, slot.Live, slot.Alpha);
        }
        if (_nextFrame is { } next)
            ui.RequestFrameAt(next);
        RetireCompletedItems();
    }
    private void RetireCompletedItems()
    {
        List<Slot>? retired = null;
        foreach (var slot in _slots)
            if (!slot.Requested && !_requestedKeys.Contains(slot.Item.Key) && slot.Alpha == 0)
                (retired ??= []).Add(slot);
        if (retired is null)
            return;
        // No child is rendering here; current-frame paint and focus retirement are already recorded.
        var remaining = _slots.Where(slot => !retired.Contains(slot)).ToArray();
        var bounds = new UiRect[remaining.Length];
        var destination = 0;
        for (var i = 0; i < _slots.Length; i++)
            if (!retired.Contains(_slots[i]))
                bounds[destination++] = LastBounds[i];
        _slots = remaining;
        LastBounds = bounds;
        SetOwnedChildSnapshot(remaining.Select(slot => slot.Item).ToArray());
        List<Exception>? errors = null;
        foreach (var slot in retired)
        {
            _owned.Remove(slot.Item.Key);
            try
            {
                slot.Dispose();
            }
            catch (Exception error) { (errors ??= []).Add(error); }
            try
            {
                ReleaseOwnedChild(slot.Item.Key, slot.Item.Widget);
            }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        _structureDirty = true;
        RequestRebuild();
        if (errors is not null)
            throw new AggregateException("Presence child retirement failed.", errors);
    }
    private protected override void ReleaseOwnedResources()
    {
        foreach (var slot in _owned.Values)
            slot.Dispose();
        _owned.Clear();
        _items = [];
        _requestedKeys.Clear();
        _slots = [];
        LastBounds = [];
        base.ReleaseOwnedResources();
    }
}

internal sealed class UiPresencePaint : IDisposable
{
    internal readonly UiPaintGroup Group = new();
    internal readonly HashSet<string> Members = [];
    internal UiRect Root;
    internal bool Captured;
    public void Dispose()
    {
        Group.Dispose();
        Members.Clear();
    }
}
