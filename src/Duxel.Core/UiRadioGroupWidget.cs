using System.Runtime.InteropServices;

namespace Duxel.Core;

public readonly record struct UiRadioOption(string Value, string Label, bool Enabled = true, string? Description = null);
public readonly record struct UiRadioOptionLayout(UiRect Bounds, UiRect Indicator, UiRect LabelBounds, UiRect DescriptionBounds,
    string Label, string? Description)
{
    public UiControlTextBlock LabelBlock
    {
        get; init;
    }
    public UiControlTextBlock DescriptionBlock
    {
        get; init;
    }
}
public readonly record struct UiRadioGroupLayout(UiVector2 Size, UiRect LabelBounds, string Label)
{
    public UiControlTextBlock LabelBlock
    {
        get; init;
    }
}
public readonly record struct UiRadioGroupInteraction(UiButtonInteraction Control, int HoveredIndex, int HeldIndex, int FocusedIndex);

/// <summary>Reusable event state; intermediate keyboard selections retain their callback order.</summary>
public sealed class UiRadioGroupInputState
{
    private readonly List<string> _changes = [];
    private int _smallFrames;
    public string? Value
    {
        get; private set;
    }
    public string? PressedValue
    {
        get; set;
    }
    internal string? AccessibilityFocusedValue;
    private string? _accessibilityRoot;
    private string[]? _accessibilityIds;
    internal string AccessibilityId(string root, int index, ReadOnlySpan<UiRadioOption> items)
    {
        if (_accessibilityRoot != root || _accessibilityIds?.Length != items.Length)
        {
            _accessibilityIds = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
                _accessibilityIds[i] = root + "\u001fradio:" + items[i].Value;
            _accessibilityRoot = root;
        }
        return _accessibilityIds![index];
    }
    public ReadOnlySpan<string> Changes => CollectionsMarshal.AsSpan(_changes);
    internal void Reconcile(string? accepted) => Value = accepted;
    internal int RetainedChangeCapacity => _changes.Capacity;
    public void Begin(string? selected)
    {
        var small = _changes.Count <= 16;
        _changes.Clear();
        Value = selected;
        if (_changes.Capacity > 64 && small)
        {
            if (++_smallFrames >= 120)
            {
                _changes.Capacity = 16;
                _smallFrames = 0;
            }
        }
        else
            _smallFrames = 0;
    }
    public void Select(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.Equals(Value, value, StringComparison.Ordinal))
            return;
        Value = value;
        _changes.Add(value);
        if (_changes.Count > 16)
            _smallFrames = 0;
    }
    internal void Release()
    {
        _changes.Clear();
        _changes.Capacity = 0;
        Value = PressedValue = AccessibilityFocusedValue = null;
        _accessibilityRoot = null;
        _accessibilityIds = null;
    }
}
public interface IUiRadioGroupBehavior
{
    static abstract UiRadioGroupInteraction Process(UiControlContext context, string id, UiRect bounds,
        ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts, string? selected, UiRadioGroupInputState state);
}
public readonly struct UiFluentRadioGroupBehavior : IUiRadioGroupBehavior
{
    public static UiRadioGroupInteraction Process(UiControlContext context, string id, UiRect bounds,
        ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts, string? selected, UiRadioGroupInputState state) =>
        context.FluentRadioGroupInput(id, bounds, items, layouts, selected, state);
}
public interface IUiRadioGroupPresentation
{
    /// <summary>Promises measurement depends on supplied width, items, label and typography, not the parent's allocated height.</summary>
    static virtual bool ReuseLayout => false;
    static virtual UiControlTypographyScope BeginText(UiControlContext context) => default;
    static abstract UiRadioGroupLayout Measure(UiControlContext context, string label, ReadOnlySpan<UiRadioOption> items, Span<UiRadioOptionLayout> layouts, float width);
    static virtual UiRadioGroupLayout Arrange(UiControlContext context, in UiRadioGroupLayout natural, UiVector2 allocatedSize)
        => throw new NotSupportedException("This RadioGroup presentation must implement Arrange to accept a different parent allocation.");
    static abstract void PaintLabel(UiControlContext context, UiRadioGroupLayout layout, UiRect bounds);
    static abstract void PaintOption(UiControlContext context, UiRadioOptionLayout layout, bool selected, UiButtonInteraction interaction);
}

/// <summary>Owned options and scratch layout, reusable by widgets and direct calls.</summary>
public sealed class UiRadioGroupContent<TPresentation> : IDisposable where TPresentation : IUiRadioGroupPresentation
{
    private UiRadioOption[] _items;
    internal UiRadioOptionLayout[] Layouts;
    internal readonly UiRadioGroupInputState Input = new();
    private readonly WeakReference<UiImmediateContext> _context = new(null!);
    private UiControlTextLayoutKey _key;
    private string? _label;
    private float _width;
    private bool _rightToLeft;
    private bool _valid, _disposed;
    internal string AccessibilityId(string root, int index)
        => Input.AccessibilityId(root, index, _items);
    internal UiRadioGroupLayout Layout;
    internal int LayoutBuildCount
    {
        get; private set;
    }
    internal long DerivedTextCharacters
    {
        get
        {
            if (!_valid)
                return 0;
            long count = (ReferenceEquals(_label, Layout.Label) ? 0 : Layout.Label.Length) + Layout.LabelBlock.RetainedCharacters;
            for (var i = 0; i < Layouts.Length; i++)
            {
                if (!ReferenceEquals(Layouts[i].Label, _items[i].Label))
                    count += Layouts[i].Label.Length;
                if (!ReferenceEquals(Layouts[i].Description, _items[i].Description))
                    count += Layouts[i].Description?.Length ?? 0;
                count += Layouts[i].LabelBlock.RetainedCharacters + Layouts[i].DescriptionBlock.RetainedCharacters;
            }
            return count;
        }
    }
    public UiRadioGroupContent(ReadOnlyMemory<UiRadioOption> items)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Span)
            if (item.Value is null || item.Label is null || !values.Add(item.Value))
                throw new ArgumentException("Radio options require distinct nonnull values and labels.", nameof(items));
        _items = items.ToArray();
        Layouts = new UiRadioOptionLayout[_items.Length];
    }
    public ReadOnlySpan<UiRadioOption> Items
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _items;
        }
    }
    internal void Measure(UiImmediateContext ui, UiControlContext context, string label, float width, in UiControlTextLayoutKey key, bool cacheable)
    {
        if (cacheable && TPresentation.ReuseLayout && _valid && _width == width && _label == label && _key == key
            && _rightToLeft == context.RightToLeft && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, ui))
            return;
        _valid = false;
        _label = null;
        _key = default;
        _context.SetTarget(null!);
        try
        {
            Layout = TPresentation.Measure(context, label, _items, Layouts, width);
            LayoutBuildCount++;
            if (!float.IsFinite(Layout.Size.X) || !float.IsFinite(Layout.Size.Y) || Layout.Size.X < 0 || Layout.Size.Y < 0)
                throw new InvalidOperationException("Radio group presentation returned invalid extents.");
            long extra = (ReferenceEquals(label, Layout.Label) ? 0 : Layout.Label.Length) + Layout.LabelBlock.RetainedCharacters;
            long lineSlots = Layout.LabelBlock.RetainedLineCount;
            for (var i = 0; i < Layouts.Length; i++)
            {
                if (!ReferenceEquals(Layouts[i].Label, _items[i].Label))
                    extra += Layouts[i].Label.Length;
                if (!ReferenceEquals(Layouts[i].Description, _items[i].Description))
                    extra += Layouts[i].Description?.Length ?? 0;
                extra += Layouts[i].LabelBlock.RetainedCharacters + Layouts[i].DescriptionBlock.RetainedCharacters;
                lineSlots += Layouts[i].LabelBlock.RetainedLineCount + Layouts[i].DescriptionBlock.RetainedLineCount;
            }
            _valid = TPresentation.ReuseLayout && cacheable && extra <= 4096 && lineSlots <= 256 && label.Length <= 1024;
            if (_valid)
            {
                _width = width;
                _label = label;
                _key = key;
                _rightToLeft = context.RightToLeft;
                _context.SetTarget(ui);
            }
            else
            {
                _label = null;
                _key = default;
                _context.SetTarget(null!);
            }
        }
        catch
        {
            Array.Clear(Layouts);
            Layout = default;
            throw;
        }
    }
    internal UiRadioGroupLayout ResolveLayout(UiControlContext context, bool constrainLayout)
    {
        if (!constrainLayout)
            return Layout;
        var size = context.ConstrainToLayout(Layout.Size);
        if (size == Layout.Size)
            return Layout;
        var arranged = TPresentation.Arrange(context, Layout, size);
        if (arranged.Size != size)
            throw new InvalidOperationException("RadioGroup presentation did not honor the parent allocation.");
        return arranged;
    }
    internal void EndFrame()
    {
        if (!_valid)
        {
            Array.Clear(Layouts);
            Layout = default;
        }
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _items = [];
        Layouts = [];
        Layout = default;
        _valid = false;
        _label = null;
        _key = default;
        _context.SetTarget(null!);
        Input.Release();
    }
}
public class UiRadioGroup<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiRadioGroupBehavior where TPresentation : IUiRadioGroupPresentation
{
    private string _label;
    private UiBinding<string?>? _value;
    private UiRadioGroupContent<TPresentation>? _content;
    private float _width;
    public UiRadioGroup(string label, UiBinding<string?> value, ReadOnlyMemory<UiRadioOption> items, float width = 0,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        ValidateWidth(width);
        _label = label;
        _value = value;
        _width = width;
        _content = new(items);
    }
    private static void ValidateWidth(float width)
    {
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
    }
    public string Label
    {
        get => _label; set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_label == value)
                return;
            _label = value;
            RequestRebuild();
        }
    }
    public float Width
    {
        get => _width; set
        {
            ThrowIfDisposed();
            ValidateWidth(value);
            if (_width == value)
                return;
            _width = value;
            RequestRebuild();
        }
    }
    public UiBinding<string?> Value => _value ?? throw new ObjectDisposedException(nameof(UiRadioGroup<TBehavior, TPresentation>));
    public ReadOnlySpan<UiRadioOption> Items => (_content ?? throw new ObjectDisposedException(nameof(UiRadioGroup<TBehavior, TPresentation>))).Items;
    public void SetItems(ReadOnlyMemory<UiRadioOption> items)
    {
        ThrowIfDisposed();
        if (Items.SequenceEqual(items.Span))
            return;
        var content = new UiRadioGroupContent<TPresentation>(items);
        var previous = _content!;
        foreach (var item in content.Items)
            if (item.Enabled && item.Value == previous.Input.PressedValue)
            {
                content.Input.PressedValue = item.Value;
                break;
            }
        _content = content;
        previous.Dispose();
        RequestRebuild();
    }
    protected virtual void OnChanged(string value) => Value.Value = value;
    protected override bool CanRetainMeasurement => TPresentation.ReuseLayout;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureRadioGroup(Label, Width, _content!);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var content = _content!;
        var value = Value.Value;
        ui.RadioGroup<TBehavior, TPresentation>(PrimaryInputId(Label), Label, content, ref value, Width, constrainLayout: true);
        foreach (var change in content.Input.Changes)
        {
            OnChanged(change);
            if (!ReferenceEquals(_content, content))
                break;
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _content?.Dispose();
        _content = null;
        _value = null;
        _label = string.Empty;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiRadioGroup<,>), typeof(UiFluentRadioGroupBehavior), typeof(UiFluentRadioGroupPresentation<UiRadioVerticalLayout>))]
[UiComponent("RadioGroup", UiComponentExposure.Fluent)]
public partial class UiFluentRadioGroup;
