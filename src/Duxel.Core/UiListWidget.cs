namespace Duxel.Core;

public enum UiListNavigation
{
    Off, Items, Composite
}
public enum UiListActionKind
{
    Pointer, Enter, Space, Accessibility
}
public sealed class UiListItemAction(string value, UiListActionKind kind)
{
    public string Value { get; } = value;
    public UiListActionKind Kind { get; } = kind;
    public bool PreventSelection
    {
        get; set;
    }
}
public readonly record struct UiListOptions(UiListNavigation Navigation = UiListNavigation.Off,
    float Spacing = 0, float? Width = null, bool RightToLeft = false)
{
    internal void Validate()
    {
        if ((uint)Navigation > 2 || !float.IsFinite(Spacing) || Spacing < 0
            || Width is { } width && (!float.IsFinite(width) || width <= 0))
            throw new ArgumentOutOfRangeException(nameof(UiListOptions));
    }
}
public interface IUiListPresentation
{
    /// <summary>Promises measurement from options and the declared text environment only.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract float CheckmarkExtent(UiControlContext context);
    static abstract void PaintCheckmark(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool selected);
    static abstract void PaintFocus(UiControlContext context, UiRect bounds);
}
public readonly struct UiFluentListPresentation : IUiListPresentation
{
    public static bool CanRetainMeasurement => true;
    public static float CheckmarkExtent(UiControlContext context) => 32;
    public static void PaintCheckmark(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool selected) =>
        PaintCheckbox(context, bounds, interaction, selected);
    internal static void PaintCheckbox(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool? selected)
    {
        using var text = UiFluentCheckboxPresentation.BeginText(context, false);
        var local = UiFluentCheckboxPresentation.Measure(context, default, false);
        local = UiFluentCheckboxPresentation.Arrange(context, local, new(bounds.Width, bounds.Height));
        local = local with
        {
            Indicator = local.Indicator with
            {
                X = local.Indicator.X + (bounds.Width - local.Size.X) / 2
            }
        };
        UiFluentCheckboxPresentation.Paint(context, string.Empty, default, bounds, local, interaction, selected);
    }
    public static void PaintFocus(UiControlContext context, UiRect bounds) => context.FocusOutline(bounds, 4);
}

/// <summary>Owns arbitrary row content; selection and keyboard navigation belong to its containing List.</summary>
public sealed class UiListItem : UiControlWidget
{
    private UiWidget? _content;
    private Action<UiListItemAction>? _action;
    internal UiListState? Owner;
    internal int Index;
    internal float CheckExtent;
    internal void SetCheckExtent(float value)
    {
        if (CheckExtent == value)
            return;
        CheckExtent = value;
        RequestRebuild();
    }
    public string Value
    {
        get;
    }
    public string Label
    {
        get;
    }
    public string SelectionLabel
    {
        get;
    }
    public bool DisabledSelection
    {
        get;
    }
    public Action<UiListItemAction>? Action
    {
        get => _action;
        set
        {
            ThrowIfDisposed();
            if (_action == value)
                return;
            ThrowIfBuilding();
            _action = value;
            RequestRebuild();
        }
    }
    public UiWidget Content => _content ?? throw new ObjectDisposedException(nameof(UiListItem));
    public UiListItem(string value, string label, UiWidget content, Action<UiListItemAction>? action = null,
        bool disabledSelection = false, string selectionLabel = "Select item", UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionLabel);
        ArgumentNullException.ThrowIfNull(content);
        if (UiAccessibilityTree.ContainsControl(label, false) || UiAccessibilityTree.ContainsControl(selectionLabel, false))
            throw new ArgumentException("List item names must be single-line text.");
        Value = value;
        Label = label;
        SelectionLabel = selectionLabel;
        _action = action;
        DisabledSelection = disabledSelection;
        OwnChild("content", content);
        _content = content;
    }
    protected override bool CanRetainMeasurement => Owner?.RetainMeasurement == true && Content.RetainsMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        _ = Owner ?? throw new InvalidOperationException("A ListItem requires its owning List.");
        var constraints = context.LayoutConstraints;
        var child = context.MeasureConstrained(Content, new(0, MathF.Max(0, constraints.MaxWidth - CheckExtent), 0, constraints.MaxHeight));
        return constraints.Constrain(new(child.X + CheckExtent, MathF.Max(child.Y, CheckExtent)));
    }
    protected override void RenderControl(UiImmediateContext ui)
        => (Owner ?? throw new InvalidOperationException("A ListItem requires its owning List.")).RenderItem(ui, this);
    private protected override void ReleaseOwnedResources()
    {
        Owner = null;
        _action = null;
        _content = null;
        base.ReleaseOwnedResources();
    }
}

internal abstract class UiListState
{
    internal abstract bool RetainMeasurement
    {
        get;
    }
    internal abstract void PaintCheckmark(UiImmediateContext ui, UiListCheckmarkPaint paint, bool selected);
    internal abstract void PaintFocus(UiImmediateContext ui, UiListFocusPaint paint);
    internal UiListItem[] Items;
    internal UiOptionSelection? Selection;
    internal UiListOptions Options;
    internal readonly UiListNavigationState Input;
    internal bool Selectable => Selection is not null;
    internal UiListNavigation Navigation => Options.Navigation == UiListNavigation.Off && Selectable ? UiListNavigation.Items : Options.Navigation;
    protected UiListState(UiListItem[] items, UiOptionSelection? selection, UiListOptions options)
    {
        Items = items;
        Selection = selection;
        Options = options;
        Input = new(items.Length);
    }
    internal bool Selected(int index) => Selection?.Selected[index] == true;
    internal void SetSelected(int index, bool selected, bool add = true)
    {
        var selection = Selection ?? throw new InvalidOperationException("List selection is not enabled.");
        if (selected)
            selection.Select(index, add);
        else
            selection.Deselect(index);
    }
    internal void Toggle(int index) => SetSelected(index, !Selected(index));
    internal void Activate(int index, UiListActionKind kind)
    {
        var item = Items[index];
        if (Selectable && kind == UiListActionKind.Space)
        {
            if (!item.DisabledSelection)
                Toggle(index);
            return;
        }
        var action = new UiListItemAction(item.Value, kind);
        item.Action?.Invoke(action);
        Selection?.Synchronize();
        if (Selectable && !item.DisabledSelection && !action.PreventSelection)
            Toggle(index);
    }
    internal abstract void RenderItem(UiImmediateContext ui, UiListItem item);
    internal void Clear()
    {
        Selection = null;
        Items = [];
        Input.Clear();
    }
}

/// <summary>Persistent rows, controlled nullable/single/multiple selection, and item/composite navigation.</summary>
public class UiList<TPresentation> : UiArrangedChildrenWidget where TPresentation : IUiListPresentation
{
    private sealed class State(UiListItem[] items, UiOptionSelection? selection, UiListOptions options) : UiListState(items, selection, options)
    {
        internal override bool RetainMeasurement => TPresentation.CanRetainMeasurement;
        internal override void PaintCheckmark(UiImmediateContext ui, UiListCheckmarkPaint paint, bool selected)
            => ui.PaintListCheckmark<TPresentation>(paint, selected);
        internal override void PaintFocus(UiImmediateContext ui, UiListFocusPaint paint)
            => ui.PaintListFocus<TPresentation>(paint);
        internal override void RenderItem(UiImmediateContext ui, UiListItem item) => ui.RenderListItem(this, item);
    }
    private readonly State _state;
    public string Label
    {
        get;
    }
    public UiListOptions Options
    {
        get => _state.Options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _state.Options)
                return;
            ThrowIfBuilding();
            _state.Options = value;
            RequestRebuild();
        }
    }
    public UiList(string label, UiListItem[] items, UiListOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, items, (UiOptionSelection?)null, options, identity) { }
    public UiList(string label, UiBinding<string?> selected, UiListItem[] items, UiListOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, items, new UiOptionSelection(SelectOptions(items), selected), options, identity) { }
    public UiList(string label, UiBinding<string[]> selected, UiListItem[] items, UiListOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, items, new UiOptionSelection(SelectOptions(items), selected), options, identity) { }
    private UiList(string label, UiListItem[] items, UiOptionSelection? selection, UiListOptions options, UiWidgetIdentity identity)
        : base(Validate(label, items, options), identity)
    {
        Label = label;
        _state = new(items.ToArray(), selection, options);
        for (var i = 0; i < items.Length; i++)
        {
            items[i].Owner = _state;
            items[i].Index = i;
        }
    }
    private static UiSelectOption[] SelectOptions(UiListItem[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Select(item => item is null ? throw new ArgumentException("Null ListItem.") : new UiSelectOption(item.Value, item.Label, !item.DisabledSelection)).ToArray();
    }
    private static UiWidget[] Validate(string label, UiListItem[] items, UiListOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(items);
        options.Validate();
        if (UiAccessibilityTree.ContainsControl(label, false) || items.Any(item => item is null || item.Owner is not null)
            || items.Select(item => item.Value).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("List requires uniquely valued, unowned nonnull rows and a single-line name.");
        return items;
    }
    protected override bool CanRetainArrangement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var constraints = context.LayoutConstraints;
        var check = _state.Selectable ? TPresentation.CheckmarkExtent(context) : 0;
        if (!float.IsFinite(check) || check < 0)
            throw new InvalidOperationException("List checkmark extent must be finite and nonnegative.");
        var width = 0f;
        for (var i = 0; i < _state.Items.Length; i++)
        {
            _state.Items[i].SetCheckExtent(check);
            var size = context.MeasureConstrained(_state.Items[i], new(0, constraints.MaxWidth, 0, float.PositiveInfinity));
            width = MathF.Max(width, size.X);
        }
        width = Math.Clamp(Options.Width ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : width), constraints.MinWidth, constraints.MaxWidth);
        var y = 0f;
        for (var i = 0; i < _state.Items.Length; i++)
        {
            var size = context.MeasureConstrained(_state.Items[i], new(width, width, 0, float.PositiveInfinity));
            bounds[i] = new(0, y, width, size.Y);
            y = UiLayoutDistribution.Finite((double)y + size.Y + (i + 1 < bounds.Length ? Options.Spacing : 0));
        }
        return constraints.Constrain(new(width, y));
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        var scope = ui.BeginList(Label, root, _state);
        try
        {
            base.RenderArrangedChildren(ui, layout, root);
        }
        finally { ui.EndList(scope, _state); }
    }
    private protected override void ReleaseOwnedResources()
    {
        _state.Clear();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiList<>), typeof(UiFluentListPresentation))]
[UiComponent("List", UiComponentExposure.Fluent)]
public partial class UiFluentList;

internal readonly record struct UiListCheckmarkPaint(UiRect Bounds, UiRect Clip, UiButtonInteraction Interaction, bool Enabled);
internal readonly record struct UiListFocusPaint(UiRect Bounds, UiRect Clip);

internal sealed class UiListNavigationState(int count)
{
    internal readonly string?[] Rows = new string?[count];
    internal readonly string?[] CheckIds = new string?[count];
    internal readonly string?[] ContentCellIds = new string?[count];
    internal readonly string?[] CheckCellIds = new string?[count];
    internal readonly UiListCheckmarkPaint[] Checkmarks = new UiListCheckmarkPaint[count];
    internal readonly UiListFocusPaint[] FocusPaints = new UiListFocusPaint[count];
    internal readonly List<UiCompositeMember>[] Children = Enumerable.Range(0, count).Select(_ => new List<UiCompositeMember>()).ToArray();
    internal readonly int[] Frames = Enumerable.Repeat(int.MinValue, count).ToArray();
    internal readonly bool[] Available = new bool[count];
    internal readonly List<UiKeyEvent> Keys = [];
    internal string? Remembered;
    private WeakReference<UiImmediateContext>? _context;
    internal void Bind(UiImmediateContext context)
    {
        if (_context is null || !_context.TryGetTarget(out var old) || !ReferenceEquals(old, context))
        {
            Clear();
            _context = new(context);
        }
    }
    internal int Find(string? id)
    {
        if (id is null)
            return -1;
        for (var i = 0; i < Rows.Length; i++)
        {
            if (Rows[i] == id)
                return i;
            foreach (var member in Children[i])
                if (member.Id == id)
                    return i;
        }
        return -1;
    }
    internal void Clear()
    {
        Array.Clear(Rows);
        Array.Clear(CheckIds);
        Array.Clear(ContentCellIds);
        Array.Clear(CheckCellIds);
        Array.Clear(Checkmarks);
        Array.Clear(FocusPaints);
        Array.Clear(Available);
        Array.Fill(Frames, int.MinValue);
        foreach (var children in Children)
            children.Clear();
        Keys.Clear();
        Remembered = null;
        _context = null;
    }
}
