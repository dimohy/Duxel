namespace Duxel.Core;

/// <summary>Owned row content; ancestry and selection belong to the containing tree.</summary>
public sealed class UiTreeItem : UiControlWidget
{
    private UiWidget? _content;
    private Action<string>? _invoked;
    internal UiControlTooltip? LabelTooltip;
    internal UiTreeLabelSlot? PlainLabel => _content as UiTreeLabelSlot;
    internal UiTreeViewState? Owner;
    internal int Index;
    internal UiFloatTransition ExpansionMotion;
    public string Value { get; }
    public string? ParentValue { get; }
    public string Label { get; }
    public Action<string>? Invoked
    {
        get => _invoked;
        set
        {
            ThrowIfDisposed();
            if (_invoked == value) return;
            ThrowIfBuilding();
            _invoked = value;
            RequestRebuild();
        }
    }
    public UiWidget Content => _content ?? throw new ObjectDisposedException(nameof(UiTreeItem));
    public UiTreeItem(string value, string? parentValue, string label)
        : this(value, parentValue, label, new UiTreeLabelSlot(label)) { }

    public UiTreeItem(string value, string? parentValue, string label, UiWidget content, Action<string>? invoked = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        Value = value; ParentValue = parentValue; Label = label; _invoked = invoked;
        OwnChild("content", content); _content = content;
    }
    protected override bool CanRetainMeasurement => Owner?.RetainMeasurement == true && Content.RetainsMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var owner = Owner ?? throw new InvalidOperationException("A TreeItem requires its owning Tree.");
        var constraints = context.LayoutConstraints;
        var prefix = MathF.Min(owner.Prefix(Index), constraints.MaxWidth);
        var content = context.MeasureConstrained(Content, new(0, MathF.Max(0, constraints.MaxWidth - prefix - owner.Metrics.ContentPadding * 2), 0, float.PositiveInfinity));
        return constraints.Constrain(new(prefix + owner.Metrics.ContentPadding * 2 + content.X, MathF.Max(owner.Metrics.MinimumRowHeight, content.Y)));
    }
    protected override void RenderControl(UiImmediateContext ui)
        => ui.RenderTreeItem(Owner ?? throw new InvalidOperationException("A TreeItem requires its owning Tree."), this);
    private protected override void ReleaseOwnedResources()
    {
        Owner = null;
        _content = null;
        _invoked = null;
        LabelTooltip?.Dispose();
        LabelTooltip = null;
        base.ReleaseOwnedResources();
    }
}

internal abstract class UiTreeViewState
{
    internal UiTreeItem[] Items;
    private UiTreeModel? _model;
    internal UiTreeModel Model => _model ?? throw new ObjectDisposedException(nameof(UiTreeWidget));
    internal UiTreeSelection? Multiple;
    internal UiOptionSelection? Single;
    internal UiTreeOptions Options;
    internal UiTreeMetrics Metrics;
    internal abstract bool RetainMeasurement { get; }
    internal abstract UiTreeMetrics MeasureMetrics(UiControlContext context);
    internal abstract void PaintRow(UiImmediateContext ui, UiTreeItem item, UiControlContext context, UiRect bounds, UiRect chevron, UiButtonInteraction interaction,
        bool branch, bool expanded, bool selected);
    internal abstract void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool? value);
    internal abstract void PaintFocus(UiControlContext context, UiRect bounds);
    internal readonly UiTreeAutomation Automation = new();
    internal readonly UiIncrementalSearch Search = new();
    internal readonly Dictionary<int, List<UiCompositeMember>> ActionMembers = [];
    internal readonly List<UiKeyEvent> ActionKeys = [];
    internal int PublishedGridRows;
    internal UiAccessibilityRole RowRole => Options.RowActions ? UiAccessibilityRole.TreeGridRow : UiAccessibilityRole.TreeItem;
    internal readonly string?[] RowIds;
    internal readonly UiRect[] RowBounds;
    internal readonly int[] RowFrames;
    internal readonly UiListCheckmarkPaint[] Indicators;
    internal Action? Invalidate;
    internal string? Root;
    private WeakReference<UiImmediateContext>? _context;
    internal bool Selectable => Single is not null || Multiple is not null;
    protected UiTreeViewState(string label, UiBinding<string[]> expanded, UiTreeItem[] items,
        UiBinding<string?>? single, UiBinding<string[]>? multiple, UiTreeOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        options.Validate();
        ArgumentNullException.ThrowIfNull(items);
        if (UiAccessibilityTree.ContainsControl(label, false) || items.Any(item => item is null || item.Owner is not null)
            || single is not null && multiple is not null)
            throw new ArgumentException("Tree requires unowned rows, a single-line label and one selection authority.");
        Items = items.ToArray(); Options = options;
        var hierarchy = new UiTreeHierarchy(items.Select(item => new UiTreeEntry(item.Value, item.ParentValue, item.Label)).ToArray());
        _model = new(hierarchy, expanded);
        if (multiple is not null) Multiple = new(hierarchy, multiple);
        if (single is not null) Single = new(items.Select(item => new UiSelectOption(item.Value, item.Label)).ToArray(), single);
        RowIds = new string?[items.Length]; RowBounds = new UiRect[items.Length]; RowFrames = new int[items.Length];
        Array.Fill(RowFrames, int.MinValue); Indicators = new UiListCheckmarkPaint[items.Length];
    }
    internal float Indent(int index) => (Model.Hierarchy.Depth[index] - 1) * Metrics.Indentation;
    internal float Prefix(int index) => Indent(index) + Metrics.ChevronWidth + (Selectable ? Metrics.SelectorWidth : 0);
    internal void Synchronize() { Model.Synchronize(); Single?.Synchronize(); Multiple?.Synchronize(); }
    internal bool? Checked(int index) => Multiple is not null ? Multiple.Checked(index) : Single?.Selected[index];
    internal bool Check(int index, bool value)
    {
        var before = Multiple?.Revision ?? Single?.Revision ?? 0;
        var accepted = false;
        if (Multiple is not null) accepted = Multiple.SetChecked(index, value);
        else if (Single is not null)
        {
            if (value) Single.Select(index, false); else Single.Deselect(index);
            accepted = Single.Selected[index] == value;
        }
        if ((Multiple?.Revision ?? Single?.Revision ?? 0) != before) Invalidate?.Invoke();
        return accepted;
    }
    internal bool Expand(int index, bool value)
    {
        var before = Model.VisibilityRevision;
        var result = Model.SetExpanded(index, value);
        if (Model.VisibilityRevision != before) Invalidate?.Invoke();
        return result;
    }
    internal int Find(string? id) => id is null ? -1 : Array.FindIndex(RowIds, candidate => candidate == id);
    internal int FindFocusRow(string? id)
    {
        var row = Find(id);
        if (row >= 0 || id is null)
            return row;
        foreach (var (index, members) in ActionMembers)
            foreach (var member in members)
                if (member.Id == id)
                    return index;
        return -1;
    }
    internal void Bind(UiImmediateContext context, string root)
    {
        if (_context is not null && _context.TryGetTarget(out var prior) && ReferenceEquals(prior, context) && Root == root) return;
        foreach (var item in Items)
            item.ExpansionMotion = default;
        _context = new(context); Automation.Reset(); Search.Reset(); ActionMembers.Clear(); Array.Clear(RowIds); Array.Fill(RowFrames, int.MinValue); Root = root;
    }
    internal void Clear()
    {
        foreach (var item in Items) item.Owner = null;
        ActionMembers.Clear();
        ActionKeys.Clear();
        Automation.Reset(); Search.Reset(); Items = []; Single = null; Multiple = null; _model = null; Invalidate = null; _context = null; Root = null;
    }
}

/// <summary>Owned hierarchical rows with controlled expansion and optional single or cascading terminal selection.</summary>
public class UiTree<TPresentation> : UiArrangedChildrenWidget where TPresentation : IUiTreePresentation
{
    private sealed class PresentationState : UiTreeViewState
    {
        internal PresentationState(string label, UiBinding<string[]> expanded, UiTreeItem[] items,
            UiBinding<string?>? single, UiBinding<string[]>? multiple, UiTreeOptions options)
            : base(label, expanded, items, single, multiple, options)
        {
            var slots = Items.Select(item => item.PlainLabel).Where(slot => slot is not null).ToArray();
            var labels = new UiTreeLabel[slots.Length];
            try
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    labels[i] = TPresentation.CreateLabel(slots[i]!.Text, () => Options)
                        ?? throw new InvalidOperationException("A Tree presentation returned a null label.");
                    if (labels[i].Text != slots[i]!.Text)
                        throw new InvalidOperationException("A Tree presentation must preserve the original label text.");
                }
                if (labels.Distinct(ReferenceEqualityComparer.Instance).Count() != labels.Length)
                    throw new InvalidOperationException("A Tree presentation cannot share owned labels between rows.");
            }
            catch
            {
                foreach (var created in labels)
                    created?.Dispose();
                throw;
            }
            for (var i = 0; i < slots.Length; i++)
                slots[i]!.Initialize(labels[i]);
        }
        internal override bool RetainMeasurement => TPresentation.CanRetainMeasurement;
        internal override UiTreeMetrics MeasureMetrics(UiControlContext context) =>
            TPresentation.Measure(context, Options.WithInheritedDirection(context.RightToLeft));
        internal override void PaintRow(UiImmediateContext ui, UiTreeItem item, UiControlContext context, UiRect bounds,
            UiRect chevron, UiButtonInteraction interaction, bool branch, bool expanded, bool selected)
        {
            var options = Options.WithInheritedDirection(context.RightToLeft);
            var duration = TPresentation.ExpansionDurationSeconds;
            var expansion = expanded ? 1f : 0f;
            if (branch && duration != 0)
                expansion = ui.AnimateFloat(ref item.ExpansionMotion, expansion, UiDisclosureMotion.Timing(duration, options.ReducedMotion));
            if (!TPresentation.PaintAnimatedRow(context, bounds, chevron, interaction, branch, expanded, selected, options, expansion))
                TPresentation.PaintRow(context, bounds, chevron, interaction, branch, expanded, selected, options);
        }
        internal override void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool? value)
            => TPresentation.PaintSelector(context, bounds, interaction, value, Multiple is not null, Options.WithInheritedDirection(context.RightToLeft));
        internal override void PaintFocus(UiControlContext context, UiRect bounds) =>
            TPresentation.PaintFocus(context, bounds, Options.WithInheritedDirection(context.RightToLeft));
    }
    internal UiTreeViewState State { get; }
    public string Label { get; }
    public UiTreeOptions Options
    {
        get => State.Options;
        set
        {
            ThrowIfDisposed(); value.Validate();
            if (State.Options == value) return;
            ThrowIfBuilding();
            State.Options = value;
            foreach (var item in State.Items) item.InvalidateMeasurementChain();
            RequestRebuild();
        }
    }
    private long _visibilityRevision = -1;
    public UiTree(string label, UiBinding<string[]> expanded, UiTreeItem[] items, UiTreeOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, expanded, items, null, null, options, identity) { }
    public UiTree(string label, UiBinding<string[]> expanded, UiBinding<string?> selected, UiTreeItem[] items,
        UiTreeOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, expanded, items, selected ?? throw new ArgumentNullException(nameof(selected)), null, options, identity) { }
    /// <summary>Multiple selection binds terminal item keys; branch checked/mixed state derives from those leaves.</summary>
    public UiTree(string label, UiBinding<string[]> expanded, UiBinding<string[]> selectedLeaves, UiTreeItem[] items,
        UiTreeOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, expanded, items, null, selectedLeaves ?? throw new ArgumentNullException(nameof(selectedLeaves)), options, identity) { }
    protected UiTree(string label, UiBinding<string[]> expanded, UiTreeItem[] items,
        UiBinding<string?>? single, UiBinding<string[]>? multiple, UiTreeOptions options, UiWidgetIdentity identity)
        : this(label, new PresentationState(label, expanded, items, single, multiple, options), identity) { }
    private UiTree(string label, UiTreeViewState state, UiWidgetIdentity identity) : base(state.Items, identity)
    {
        Label = label; State = state; State.Invalidate = RequestRebuild;
        for (var i = 0; i < state.Items.Length; i++) { state.Items[i].Owner = state; state.Items[i].Index = i; }
    }
    protected override bool CanRetainArrangement => TPresentation.CanRetainMeasurement;
    protected override void PrepareChildren(UiControlContext context)
    {
        State.Synchronize();
        var metrics = State.MeasureMetrics(context); metrics.Validate();
        var metricsChanged = State.Metrics != metrics;
        State.Metrics = metrics;
        if (!metricsChanged && _visibilityRevision == State.Model.VisibilityRevision) return;
        _visibilityRevision = State.Model.VisibilityRevision;
        InvalidateRetainedMeasurement();
    }
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        bounds.Clear();
        var constraints = context.LayoutConstraints;
        var width = State.Options.Width ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 0);
        if (width == 0)
            foreach (var index in State.Model.Visible)
                width = MathF.Max(width, context.MeasureConstrained(State.Items[index], new(0, float.PositiveInfinity, 0, float.PositiveInfinity)).X);
        width = Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth);
        var y = 0f;
        foreach (var index in State.Model.Visible)
        {
            var size = context.MeasureConstrained(State.Items[index], new(width, width, 0, float.PositiveInfinity));
            bounds[index] = new(0, y, width, size.Y);
            y = UiLayoutDistribution.Finite((double)y + size.Y);
        }
        return constraints.Constrain(new(width, y));
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        var keys = ui.TreeInputKeys;
        try
        {
            ui.UpdateTreeInput(State);
            base.RenderControl(ui);
        }
        finally
        {
            ui.TreeInputKeys = keys;
        }
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        var scope = ui.BeginTree(Label, root, State);
        try
        {
            foreach (var index in State.Model.Visible)
            {
                if (!State.Model.IsVisible(index)) continue;
                var bounds = layout[index] with { X = root.X + layout[index].X, Y = root.Y + layout[index].Y };
                ui.RenderParentDataChild(State.Items[index], null, bounds, root);
            }
        }
        finally { ui.EndTree(scope, State); }
    }
    private protected override void ReleaseOwnedResources() { State.Clear(); base.ReleaseOwnedResources(); }
}

// Existing internal probes use the default presentation while factory/Gallery qualification remains open.
internal sealed class UiTreeWidget(string label, UiBinding<string[]> expanded, UiTreeItem[] items,
    UiBinding<string?>? single = null, UiBinding<string[]>? multiple = null, UiTreeOptions options = default)
    : UiTree<UiFluentTreePresentation>(label, expanded, items, single, multiple, options, UiWidgetIdentity.Instance);

[UiWidgetStyle(typeof(UiTree<>), typeof(UiFluentTreePresentation))]
[UiComponent("Tree", UiComponentExposure.Fluent)]
public partial class UiFluentTree;
