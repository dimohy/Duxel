using System.Runtime.InteropServices;

namespace Duxel.Core;

public enum UiTabListSize
{
    Medium, Small, Large
}
public enum UiTabListAppearance
{
    Transparent, Subtle, SubtleCircular, FilledCircular
}
public readonly record struct UiTabBadge(string Text, UiFluentBadgeOptions Options = default);
public readonly record struct UiTabOption(string Value, string Label, bool Enabled = true,
    UiTabIcon Icon = default, bool IconOnly = false)
{
    public UiTabBadge? Badge
    {
        get; init;
    }
}
public readonly record struct UiTabListOptions(UiTabListSize Size = UiTabListSize.Medium,
    bool Vertical = false, bool SelectOnFocus = false, bool RightToLeft = false,
    bool ReserveSelectedSpace = true, UiTabListAppearance Appearance = UiTabListAppearance.Transparent,
    UiTabListOverflowOptions? Overflow = null)
{
    public UiTabListScrollOptions? Scroll
    {
        get; init;
    }
    public bool ReducedMotion { get; init; }
    internal bool RequireSelection { get; init; }
    public UiTabListOptions() : this(UiTabListSize.Medium, false, false, false, true, UiTabListAppearance.Transparent) { }
    public UiTabListOptions Validate()
    {
        if ((uint)Size > 2 || (uint)Appearance > 3)
            throw new ArgumentOutOfRangeException(nameof(UiTabListOptions));
        Overflow?.Validate();
        Scroll?.Validate();
        if (Scroll is not null && (Overflow is not null || Vertical))
            throw new ArgumentException("Scrollable tabs require a horizontal header without overflow-menu allocation.");
        return this;
    }
    internal bool Circular => Appearance is UiTabListAppearance.SubtleCircular or UiTabListAppearance.FilledCircular;
    internal float Gap => Circular ? Size == UiTabListSize.Small ? 6 : 8 : 0;
}

public sealed class UiTabListInputState
{
    internal UiTabListInputState()
    {
    }
    private readonly List<string?> _changes = [];
    public string? FocusedValue
    {
        get; set;
    }
    public string? PressedValue
    {
        get; set;
    }
    public string? Value
    {
        get; private set;
    }
    public ReadOnlySpan<string?> Changes => CollectionsMarshal.AsSpan(_changes);
    internal bool WasFocused;
    internal bool KeyboardPressed;
    internal bool[]? Visibility;
    internal bool DeferPublication;
    internal bool RequireSelection;
    internal bool OverflowFocused;
    internal bool SuppressPointerActivation;
    internal UiRect? HeaderClip;
    internal bool IsVisible(int index) => Visibility is null || Visibility[index];
    private string? _id;
    private string[] _suffixes = [];
    private string[] _ids = [];
    private string[] _tabIds = [], _panelIds = [];
    private UiImmediateContext? _panelContext;
    private string? _root;
    private int _panelFrame;
    private bool _panelRendered;
    internal void SetItems(ReadOnlySpan<UiTabOption> items)
    {
        _suffixes = new string[items.Length];
        for (var i = 0; i < items.Length; i++)
            _suffixes[i] = "##tab-" + Convert.ToHexString(MemoryMarshal.AsBytes(items[i].Value.AsSpan()));
        _ids = new string[items.Length];
        _id = null;
        _root = null;
    }
    internal void CapturePanelContext(UiImmediateContext ui, string root)
    {
        if (_root != root)
        {
            _tabIds = new string[_suffixes.Length];
            _panelIds = new string[_suffixes.Length];
            for (var i = 0; i < _suffixes.Length; i++)
            {
                _tabIds[i] = root + _suffixes[i];
                _panelIds[i] = _tabIds[i] + "##panel";
            }
            _root = root;
        }
        _panelContext = ui;
        _panelFrame = ui.GetFrameCount();
        _panelRendered = false;
    }
    internal void RequirePanelContext(UiImmediateContext ui)
    {
        if (_panelContext != ui || _panelFrame != ui.GetFrameCount())
            throw new InvalidOperationException("Render the tab header in this context and frame before its panel.");
    }
    internal bool RenderPanel(UiImmediateContext ui, int index, UiTabOption item,
        UiWidget? content, Action<UiImmediateContext>? render, bool focusable)
    {
        if (Value != item.Value)
            return false;
        if (_panelRendered)
            throw new InvalidOperationException("A tab list may render its selected panel only once per frame.");
        _panelRendered = true;
        ui.RenderTabPanel(_panelIds[index], _tabIds[index], item.Label, content, render, focusable);
        return true;
    }
    internal string ItemId(string id, int index)
    {
        if (_id != id)
        {
            for (var i = 0; i < _ids.Length; i++)
                _ids[i] = id + _suffixes[i];
            _id = id;
        }
        return _ids[index];
    }
    public void Begin(string? value)
    {
        _changes.Clear();
        Value = value;
    }
    public void Select(string? value)
    {
        if (Value == value)
            return;
        Value = value;
        _changes.Add(value);
    }
    internal void Release()
    {
        _changes.Clear();
        _changes.Capacity = 0;
        FocusedValue = PressedValue = Value = _id = _root = null;
        _suffixes = _ids = _tabIds = _panelIds = [];
        _panelContext = null;
    }
}

public readonly record struct UiTabListInteraction(UiButtonInteraction Control, int HoveredIndex, int HeldIndex, int FocusedIndex);
public interface IUiTabListBehavior
{
    static abstract UiTabListInteraction Process(UiControlContext context, string id, UiRect bounds,
        ReadOnlySpan<UiTabOption> items, ReadOnlySpan<UiRect> layouts, string? selected,
        UiTabListOptions options, UiTabListInputState state);
}
public readonly struct UiFluentTabListBehavior : IUiTabListBehavior
{
    public static UiTabListInteraction Process(UiControlContext context, string id, UiRect bounds,
        ReadOnlySpan<UiTabOption> items, ReadOnlySpan<UiRect> layouts, string? selected,
        UiTabListOptions options, UiTabListInputState state) => context.TabListInput(id, bounds, items, layouts, selected, options, state);
}
public interface IUiTabListPresentation
{
    /// <summary>Promises intrinsic tab sizes independent of host constraints, with selection-independent metrics when ReserveSelectedSpace is enabled.</summary>
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Returns the selected indicator geometry for presentations that paint it as a separate overlay.</summary>
    static virtual (UiRect Bounds, UiColor Color)? SelectionIndicator(UiControlContext context,
        UiRect bounds, UiTabListOptions options, UiButtonVisualState state) => null;
    static virtual UiVector2 MeasureOverflow(UiControlContext context, UiTabListOptions options) =>
        throw new NotSupportedException("This tab presentation does not support overflow menus.");
    static virtual void PaintOverflow(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiTabListOptions options) =>
        throw new NotSupportedException("This tab presentation does not support overflow menus.");
    static abstract UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options);
    static abstract void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected,
        UiButtonInteraction interaction, UiTabListOptions options);
    static virtual UiVector2 MeasureTab(UiControlContext context, UiTabOption item, bool selected, UiTabListOptions options) =>
        throw new NotSupportedException("This tab presentation does not support icon or badge content.");
    static virtual void PaintTab(UiControlContext context, UiTabOption item, UiRect bounds, bool selected,
        UiButtonInteraction interaction, UiTabListOptions options) =>
        throw new NotSupportedException("This tab presentation does not support icon or badge content.");
}

/// <summary>Owns stable tab values, selection binding and independent roving focus.</summary>
public partial class UiTabList<TBehavior, TPresentation> : UiControlWidget
    where TBehavior : IUiTabListBehavior where TPresentation : IUiTabListPresentation
{
    private UiTabOption[] _items;
    private UiTabOption[] _renderedItems = [];
    private UiRect[] _layouts;
    private UiBinding<string?>? _value;
    private UiTabListOptions _options;
    private readonly UiTabListInputState _input = new();
    private UiFloatTransition _indicatorProgress;
    private UiRect _indicatorStart, _indicatorLast;
    private string? _indicatorSelection;
    private (bool Vertical, bool RightToLeft) _indicatorDirection;
    private bool _indicatorRunning;
    public UiTabList(string label, UiBinding<string?> value, ReadOnlyMemory<UiTabOption> items,
        UiTabListOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        _options = (options ?? new()).Validate();
        _items = ValidateItems(items);
        _layouts = new UiRect[_items.Length];
        _input.SetItems(_items);
        Label = label;
        AccessibleName = label;
        _value = value;
    }
    public string Label
    {
        get;
    }
    public UiBinding<string?> Value => _value ?? throw new ObjectDisposedException(nameof(UiTabList<TBehavior, TPresentation>));
    public ReadOnlySpan<UiTabOption> Items => _items;
    public UiTabListOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }

    private UiTabListOptions EffectiveOptions(UiControlContext context) =>
        Options with { RightToLeft = Options.RightToLeft || context.RightToLeft };
    private static UiTabOption[] ValidateItems(ReadOnlyMemory<UiTabOption> items)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Span)
        {
            if (item.Value is null || item.Label is null || !values.Add(item.Value))
                throw new ArgumentException("Tabs require unique nonnull values and labels.", nameof(items));
            if (item.IconOnly && item.Icon.IsEmpty || !item.Icon.IsEmpty && string.IsNullOrWhiteSpace(item.Label))
                throw new ArgumentException("Icon tabs require an icon and a nonempty accessible label.", nameof(items));
            if (item.Badge is { } badge)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(badge.Text);
                if (UiAccessibilityTree.ContainsControl(badge.Text, false))
                    throw new ArgumentException("Tab badge text must be single-line text.", nameof(items));
                badge.Options.Validate();
            }
        }
        return items.ToArray();
    }
    public void SetItems(ReadOnlyMemory<UiTabOption> items)
    {
        ThrowIfDisposed();
        if (_items.AsSpan().SequenceEqual(items.Span))
            return;
        var next = ValidateItems(items);
        ThrowIfBuilding();
        _items = next;
        _layouts = new UiRect[next.Length];
        _input.SetItems(next);
        _input.PressedValue = null;
        RequestRebuild();
    }
    /// <summary>Renders a borrowed body only when its key was selected by this frame's header.</summary>
    public bool RenderPanel(UiImmediateContext ui, string value, UiWidget content, bool focusable = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        return RenderPanelCore(ui, value, content, null, focusable);
    }
    public bool RenderPanel(UiImmediateContext ui, string value, Action<UiImmediateContext> render, bool focusable = true)
    {
        ArgumentNullException.ThrowIfNull(render);
        return RenderPanelCore(ui, value, null, render, focusable);
    }
    private bool RenderPanelCore(UiImmediateContext ui, string value, UiWidget? content,
        Action<UiImmediateContext>? render, bool focusable)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(value);
        _input.RequirePanelContext(ui);
        for (var i = 0; i < _renderedItems.Length; i++)
            if (_renderedItems[i].Value == value)
                return _input.RenderPanel(ui, i, _renderedItems[i], content, render, focusable);
        throw new ArgumentException("The panel key must identify a tab rendered in this frame.", nameof(value));
    }
    private UiVector2 MeasureTabs(UiControlContext context, string? selected)
    {
        var cache = PrepareMeasurementCache(context, selected, out var reuse);
        var options = EffectiveOptions(context);
        var key = context.LayoutMeasurementKey;
        var revision = cache?.Revision ?? 0;
        var main = 0f;
        var cross = 0f;
        for (var i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            var size = reuse ? cache!.Sizes[i]
                : item.Icon.IsEmpty && item.Badge is null ? TPresentation.MeasureTab(context, item.Label, item.Value == selected, options)
                : TPresentation.MeasureTab(context, item, item.Value == selected, options);
            if (!reuse && cache is not null)
                cache.Sizes[i] = size;
            if (i != 0)
                main += options.Gap;
            _layouts[i] = options.Vertical ? new(0, main, size.X, size.Y) : new(main, 0, size.X, size.Y);
            main += options.Vertical ? size.Y : size.X;
            cross = MathF.Max(cross, options.Vertical ? size.X : size.Y);
        }
        if (!float.IsFinite(main) || !float.IsFinite(cross))
            throw new InvalidOperationException("Tab geometry exceeds finite layout capacity.");
        if (!reuse && cache is not null && ReferenceEquals(cache, _measurements) && cache.Revision == revision
            && context.CanRetainLayoutMeasurement && context.LayoutMeasurementKey == key)
            cache.Commit(key, options, options.ReserveSelectedSpace ? null : selected);
        for (var i = 0; i < _layouts.Length; i++)
            _layouts[i] = options.Vertical ? _layouts[i] with
            {
                Width = cross
            }
                : _layouts[i] with
                {
                    X = options.RightToLeft ? main - _layouts[i].X - _layouts[i].Width : _layouts[i].X,
                    Height = cross
                };
        return options.Vertical ? new(cross, main) : new(main, cross);
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement && Options.ReserveSelectedSpace && Options.Overflow is null;
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureTabControl(context, Options.ReserveSelectedSpace ? null : Value.Value);
    protected virtual void OnChanged(string? value) => Value.Value = value;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var options = EffectiveOptions(context);
        if (options.Scroll is { } scrollOptions)
        {
            RenderScrollingTabs(ui, scrollOptions);
            return;
        }
        _input.SuppressPointerActivation = false;
        ui.CancelMenuPopup(_scrollPreviousMenu, PrimaryInputId(Label));
        ui.CancelMenuPopup(_scrollNextMenu, PrimaryInputId(Label));
        var selected = Value.Value;
        var size = context.ConstrainToLayout(MeasureTabControl(context, selected));
        if (options.Overflow is not null)
            size = context.LayoutConstraints.Constrain(size);
        if (options.Vertical && options.Overflow is null)
            for (var i = 0; i < _layouts.Length; i++)
                _layouts[i] = _layouts[i] with
                {
                    Width = size.X
                };
        ArrangeOverflow(context, size, selected);
        _input.DeferPublication = options.Overflow is not null;
        var origin = ui.AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var id = PrimaryInputId(Label);
        if (OverflowResult is not { IsOverflowing: true })
            ui.CancelMenuPopup(_overflowMenu, id);
        UiTabListInteraction interaction;
        var header = _headerBounds with
        {
            X = origin.X + _headerBounds.X,
            Y = origin.Y + _headerBounds.Y
        };
        _input.HeaderClip = options.Overflow is not null ? header : null;
        using (context.Clip(bounds))
            interaction = TBehavior.Process(context, id, bounds, _items, _layouts, selected, options, _input);
        UiButtonInteraction overflowInteraction = default;
        var overflowId = id + "##overflow";
        var overflowLabel = options.Overflow is { } overflowOptions ? $"{OverflowResult?.HiddenCount} {overflowOptions.Label}" : string.Empty;
        if (OverflowResult is { IsOverflowing: true })
        {
            overflowLabel = $"{OverflowResult.Value.HiddenCount} {options.Overflow!.Value.Label}";
            var trigger = _overflowBounds with
            {
                X = origin.X + _overflowBounds.X,
                Y = origin.Y + _overflowBounds.Y
            };
            using (context.Clip(bounds))
                overflowInteraction = ui.ProcessMenuTrigger(overflowId, overflowLabel, _overflowChoices, _overflowMenu, trigger,
                    ui.GetID(id), tab: true, publish: false, focusOwner: ui.GetID(id), ownerFocused: _input.OverflowFocused);
            if (overflowInteraction.Focused)
                _input.OverflowFocused = _input.WasFocused = true;
            var invoked = ui.RenderMenuPopup<UiFluentMenuPresentation>(overflowId, overflowLabel, _overflowChoices,
                UiFluentMenuPresentation.DefaultOptions with
                {
                    RightToLeft = options.RightToLeft
                }, _overflowMenu, trigger, overflowInteraction);
            if (invoked is not null)
                _input.Select(invoked);
            if (_overflowMenu.Open)
                _input.OverflowFocused = true;
            if (!_overflowMenu.Open && _input.OverflowFocused && ui.IsLogicalControlFocused(id))
                _input.WasFocused = true;
        }
        if (options.Overflow is not null)
        {
            MeasureTabControl(context, _input.Value);
            ArrangeOverflow(context, size, _input.Value);
            _input.HeaderClip = _headerBounds with
            {
                X = origin.X + _headerBounds.X,
                Y = origin.Y + _headerBounds.Y
            };
            ui.CommitTabList(id, bounds, _items, _layouts, _input, interaction);
        }
        header = _headerBounds with
        {
            X = origin.X + _headerBounds.X,
            Y = origin.Y + _headerBounds.Y
        };
        using (context.Clip(header))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (!_input.IsVisible(i))
                    continue;
                var rect = _layouts[i] with
                {
                    X = bounds.X + _layouts[i].X,
                    Y = bounds.Y + _layouts[i].Y
                };
                ui.BeginDisabled(!_items[i].Enabled);
                try
                {
                    var item = _items[i];
                    var visual = new UiButtonInteraction(false, i == interaction.HoveredIndex, i == interaction.HeldIndex)
                    {
                        Focused = interaction.Control.Focused && i == interaction.FocusedIndex,
                        FocusVisible = interaction.Control.FocusVisible && i == interaction.FocusedIndex
                    };
                    PaintTab(context, item, rect, visual);
                }
                finally { ui.EndDisabled(); }
            }
            PaintSelectionIndicator(ui, context, origin, interaction, 0);
        }
        if (OverflowResult is { IsOverflowing: true })
        {
            var trigger = _overflowBounds with
            {
                X = origin.X + _overflowBounds.X,
                Y = origin.Y + _overflowBounds.Y
            };
            using var clip = context.Clip(bounds);
            overflowLabel = $"{OverflowResult.Value.HiddenCount} {options.Overflow!.Value.Label}";
            ui.PublishMenuTabTrigger(overflowId, overflowLabel, trigger, ui.GetID(id), overflowInteraction,
                _input.OverflowFocused ? ui.GetID(id) : null);
            TPresentation.PaintOverflow(context, trigger, overflowInteraction, options);
        }
        ui.FinishLayoutBox(origin, size);
        CompleteTabFrame();
    }

    private void PaintTab(UiControlContext context, UiTabOption item, UiRect rect, UiButtonInteraction visual)
    {
        var options = EffectiveOptions(context);
        var selected = item.Value == _input.Value;
        context = context with { SeparateTabSelectionIndicator = selected };
        if (item.Icon.IsEmpty && item.Badge is null)
            TPresentation.PaintTab(context, item.Label, rect, selected, visual, options);
        else
            TPresentation.PaintTab(context, item, rect, selected, visual, options);
    }

    private void PaintSelectionIndicator(UiImmediateContext ui, UiControlContext context, UiVector2 origin,
        UiTabListInteraction interaction, float scrollOffset)
    {
        var options = EffectiveOptions(context);
        var selected = _input.Value;
        var index = 0;
        while (index < _items.Length && _items[index].Value != selected)
            index++;
        if (index == _items.Length || !_input.IsVisible(index) && options.Scroll is null)
        {
            _indicatorSelection = null;
            return;
        }
        var local = _layouts[index] with { X = _layouts[index].X + scrollOffset };
        var state = !context.IsEnabled || !_items[index].Enabled ? UiButtonVisualState.Disabled
            : interaction.HeldIndex == index && (interaction.HoveredIndex == index || interaction.FocusedIndex == index && interaction.Control.Focused)
                ? UiButtonVisualState.Pressed
                : interaction.HoveredIndex == index ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        if (TPresentation.SelectionIndicator(context, local, options, state) is not { } indicator)
        {
            _indicatorSelection = null;
            return;
        }
        var direction = (options.Vertical, options.RightToLeft);
        if (_indicatorSelection is null || _indicatorDirection != direction || options.ReducedMotion)
            _indicatorRunning = false;
        else if (_indicatorSelection != selected)
        {
            _indicatorStart = _indicatorLast;
            _indicatorProgress.Reset(0);
            _indicatorRunning = true;
        }
        _indicatorSelection = selected;
        _indicatorDirection = direction;
        var progress = _indicatorRunning ? ui.AnimateFloat(ref _indicatorProgress, 1, UiDisclosureMotion.Timing()) : 1;
        _indicatorRunning = progress < 1;
        var target = indicator.Bounds;
        float Blend(float from, float to) => from + (to - from) * progress;
        _indicatorLast = progress == 1 ? target : new(Blend(_indicatorStart.X, target.X), Blend(_indicatorStart.Y, target.Y),
            Blend(_indicatorStart.Width, target.Width), Blend(_indicatorStart.Height, target.Height));
        var bounds = _indicatorLast with { X = origin.X + _indicatorLast.X - scrollOffset, Y = origin.Y + _indicatorLast.Y };
        var thickness = options.Vertical ? bounds.Width : bounds.Height;
        context.Frame(bounds, indicator.Color, default, thickness / 2, 0);
    }
    private void CompleteTabFrame()
    {
        var renderedItems = _items;
        _renderedItems = renderedItems;
        foreach (var change in _input.Changes)
        {
            OnChanged(change);
            if (_value is null || !ReferenceEquals(_items, renderedItems))
                break;
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _measurements = null;
        ReleaseOverflow();
        ReleaseScrollingMenus();
        _items = _renderedItems = [];
        _layouts = [];
        _value = null;
        _input.Release();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiTabList<,>), typeof(UiFluentTabListBehavior), typeof(UiFluentTabListPresentation))]
[UiComponent("TabList", UiComponentExposure.Fluent)]
public partial class UiFluentTabList;
