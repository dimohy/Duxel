namespace Duxel.Core;

public readonly record struct UiNavBadge(string Text, UiFluentBadgeOptions Options = default);
public readonly record struct UiNavItem(string Value, string Label, bool Enabled = true, UiMenuIcon Icon = default,
    UiNavBadge? Badge = null, UiNavSecondaryAction? SecondaryAction = null);
public readonly record struct UiNavGroup(string Value, string Label, ReadOnlyMemory<UiNavItem> Items,
    UiMenuIcon Icon = default, string Description = "", bool Collapsible = true, bool Enabled = true, bool IsSectionHeader = false)
{
    /// <summary>A visual section label, without a destination, disclosure or keyboard stop.</summary>
    public static UiNavGroup SectionHeader(string value, string label) =>
        new(value, label, ReadOnlyMemory<UiNavItem>.Empty, Collapsible : false, IsSectionHeader : true);
}
public readonly record struct UiNavOptions(float Width = 260, bool RightToLeft = false)
{
    public UiNavOptions() : this(260, false) { }
}
public readonly record struct UiNavRowContent(string Label, UiMenuIcon Icon, UiNavBadge? Badge, bool Category, string Description = "", bool Disclosure = true, bool HasSecondaryAction = false, bool SectionHeader = false);
public readonly record struct UiNavRowLayout(UiVector2 Size, UiControlTextBlock Text, UiVector2 BadgeSize, UiControlTextBlock Description);
public interface IUiNavPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static virtual UiRect SecondaryActionBounds(UiRect row, UiNavOptions options) =>
        throw new NotSupportedException("This navigation presentation must arrange secondary actions.");
    static virtual void PaintSecondaryAction(UiControlContext context, UiNavSecondaryAction action, UiRect bounds,
        UiButtonInteraction interaction, UiNavOptions options) =>
        throw new NotSupportedException("This navigation presentation must paint secondary actions.");
    static abstract UiNavRowLayout Measure(UiControlContext context, UiNavRowContent content, float width);
    static abstract void Paint(UiControlContext context, UiNavRowContent content, UiNavRowLayout layout,
        UiRect bounds, bool current, bool expanded, UiButtonInteraction interaction, UiNavOptions options);
}
internal readonly record struct UiNavRow(int Group, int Item, UiNavRowContent Content, UiNavRowLayout Layout, UiRect Bounds);
internal readonly record struct UiNavLayout(UiVector2 Size, UiNavRow[] Rows);

/// <summary>One-level navigation. Owns copied menu data and expansion state, and borrows page bodies and icon painters.</summary>
public class UiNav<TPresentation> : UiControlWidget where TPresentation : IUiNavPresentation
{
    private UiNavGroup[] _groups;
    private UiBinding<string?>? _value;
    private bool[] _expanded;
    private UiNavOptions _options;
    private readonly UiRetainedLayout<UiNavLayout> _layout = new();
    private UiImmediateContext? _headerContext, _pageContext;
    private int _headerFrame, _pageFrame;
    private readonly string _pageIdentity = NewPassiveAccessibilityIdentity();
    private bool _pageRendered;
    internal readonly Dictionary<string, UiButtonInteraction> Interactions = new(StringComparer.Ordinal);
    internal readonly UiNavSecondaryState Secondary = new();
    internal string? RootId;
    internal string[][] ItemIds;
    internal string[] GroupIds;
    internal string[] GroupItemsIds;
    internal string[][] ItemNames;
    internal string[] GroupNames;
    public string Label { get; }
    public bool IsContextMenuOpen => Secondary.Owner.Menu.Open;
    public UiBinding<string?> Value => _value ?? throw new ObjectDisposedException(nameof(UiNav<TPresentation>));
    public ReadOnlySpan<UiNavGroup> Groups => _groups;
    public UiNavOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ValidateOptions(value);
            if (_options == value) return;
            _options = value;
            RequestRebuild();
        }
    }
    public UiNav(string label, UiBinding<string?> value, ReadOnlyMemory<UiNavGroup> groups,
        UiNavOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        Label = label;
        _value = value;
        _options = options ?? new();
        ValidateOptions(_options);
        _groups = groups.ToArray();
        _expanded = new bool[_groups.Length];
        Array.Fill(_expanded, true);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var g = 0; g < _groups.Length; g++)
        {
            var group = _groups[g];
            ValidateEntry(group.Value, group.Label, keys);
            ArgumentNullException.ThrowIfNull(group.Description);
            if (group.IsSectionHeader && (group.Collapsible || !group.Items.IsEmpty || !group.Icon.IsEmpty || group.Description.Length != 0))
                throw new ArgumentException("A navigation section header cannot contain destinations, an icon, a description or a disclosure.", nameof(groups));
            var items = group.Items.ToArray();
            foreach (var item in items)
            {
                ValidateEntry(item.Value, item.Label, keys);
                item.SecondaryAction?.Validate();
                if (item.Badge is { } badge)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(badge.Text);
                    badge.Options.Validate();
                }
            }
            _groups[g] = group with { Items = items };
        }
        ValidateSelection();
        GroupNames = _groups.Select((_, g) => "##nav-group-" + g).ToArray();
        ItemNames = _groups.Select((group, g) => group.Items.ToArray().Select((_, i) => "##nav-item-" + g + "-" + i).ToArray()).ToArray();
        GroupIds = new string[_groups.Length];
        GroupItemsIds = new string[_groups.Length];
        ItemIds = _groups.Select(group => new string[group.Items.Length]).ToArray();
        Secondary.Initialize(_groups);
    }
    private static void ValidateOptions(UiNavOptions options)
    {
        if (!float.IsFinite(options.Width) || options.Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
    }
    private static void ValidateEntry(string value, string label, HashSet<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!keys.Add(value)) throw new ArgumentException("Navigation keys must be globally unique.");
    }
    internal bool ContainsSelection(int group)
    {
        foreach (var item in _groups[group].Items.Span)
            if (item.Value == Value.Value) return true;
        return false;
    }
    internal void ValidateSelection()
    {
        var selected = Value.Value;
        if (selected is null) return;
        for (var g = 0; g < _groups.Length; g++)
            if (ContainsSelection(g)) return;
        throw new ArgumentException("Navigation selection is not a declared destination : " + selected);
    }
    public bool IsExpanded(string group)
    {
        ThrowIfDisposed();
        return IsGroupExpanded(FindGroup(group));
    }
    internal bool IsGroupExpanded(int index) => _expanded[index];
    private int FindGroup(string key)
    {
        for (var index = 0; index < _groups.Length; index++)
            if (_groups[index].Value == key)
                return index;
        throw new ArgumentException("Navigation category is not declared : " + key, nameof(key));
    }
    public void SetExpanded(string group, bool expanded)
    {
        ThrowIfDisposed();
        var index = FindGroup(group);
        if (!_groups[index].Collapsible && !expanded)
            throw new InvalidOperationException("This navigation category cannot collapse.");
        if (_expanded[index] != expanded)
        {
            _expanded[index] = expanded;
            _layout.Clear();
            RequestRebuild();
        }
    }
    internal UiNavOptions EffectiveOptions(bool rightToLeft) =>
        _options with { RightToLeft = _options.RightToLeft || rightToLeft };

    internal UiNavLayout Prepare(UiControlContext context, bool render = false)
    {
        var retain = TPresentation.CanRetainMeasurement && context.CanRetainLayoutMeasurement;
        if (_layout.TryGet(context, render, out var found, retain)) return found;
        var attempt = _layout.Begin(context);
        var width = context.LayoutConstraints.Constrain(new(_options.Width, 0)).X;
        var rows = new List<UiNavRow>();
        var y = 0f;
        for (var g = 0; g < _groups.Length; g++)
        {
            var group = _groups[g];
            Add(-1, new(group.Label, group.Icon, null, true, group.Description, group.Collapsible, SectionHeader : group.IsSectionHeader));
            if (IsGroupExpanded(g))
                for (var i = 0; i < group.Items.Length; i++)
                {
                    var item = group.Items.Span[i];
                    Add(i, new(item.Label, item.Icon, item.Badge, false, HasSecondaryAction : item.SecondaryAction is not null));
                }
            if (!group.IsSectionHeader) y += 8;
            void Add(int item, UiNavRowContent content)
            {
                var measured = TPresentation.Measure(context, content, width);
                if (!float.IsFinite(measured.Size.X) || !float.IsFinite(measured.Size.Y) || measured.Size.X < 0 || measured.Size.Y <= 0)
                    throw new InvalidOperationException("Navigation presentation returned invalid extents.");
                rows.Add(new(g, item, content, measured, new(0, y, width, measured.Size.Y)));
                y += measured.Size.Y;
            }
        }
        var trailingGap = _groups.Length > 0 && !_groups[^1].IsSectionHeader ? 8 : 0;
        var size = new UiVector2(width, MathF.Max(0, y - trailingGap));
        var result = new UiNavLayout(size, rows.ToArray());
        _layout.Commit(context, attempt, result, size, retain);
        return result;
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    internal override void InvalidateRetainedMeasurement() => _layout.Invalidate();
    protected override UiVector2 MeasureContent(UiControlContext context) => Prepare(context).Size;
    protected virtual void OnChanged(string value) => Value.Value = value;
    protected override void RenderControl(UiImmediateContext ui)
    {
        ValidateSelection();
        var invoked = ui.RenderNav(this, PrimaryInputId(Label), out var secondaryAction);
        _headerContext = ui;
        _headerFrame = ui.GetFrameCount();
        if (invoked is not null) OnChanged(invoked);
        secondaryAction?.Invoke();
    }
    public bool RenderPage(UiImmediateContext ui, string value, Action<UiImmediateContext> render, bool focusable = true, bool navigationVisible = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(render);
        if (navigationVisible && (_headerContext != ui || _headerFrame != ui.GetFrameCount()))
            throw new InvalidOperationException("Render navigation in this context and frame before its page.");
        var g = -1;
        var i = -1;
        for (var group = 0; group < _groups.Length; group++)
            for (var item = 0; item < _groups[group].Items.Length; item++)
                if (_groups[group].Items.Span[item].Value == value) { g = group; i = item; }
        if (g < 0) throw new ArgumentException("Navigation destination is not declared : " + value, nameof(value));
        if (Value.Value != value) return false;
        if (_pageContext != ui || _pageFrame != ui.GetFrameCount())
        {
            _pageContext = ui;
            _pageFrame = ui.GetFrameCount();
            _pageRendered = false;
        }
        if (_pageRendered) throw new InvalidOperationException("Navigation may render its current page only once per frame.");
        _pageRendered = true;
        var label = _groups[g].Items.Span[i].Label;
        ui.RenderNavigationPage(ui.GetID(_pageIdentity), navigationVisible ? IsGroupExpanded(g) ? ItemIds[g][i] : GroupIds[g] : null,
            label, render, focusable);
        return true;
    }
    private protected override void ReleaseOwnedResources()
    {
        _layout.Clear();
        Secondary.Dispose();
        Interactions.Clear();
        _expanded = [];
        _groups = [];
        _value = null;
        _headerContext = _pageContext = null;
        RootId = null;
        GroupNames = GroupIds = GroupItemsIds = [];
        ItemNames = ItemIds = [];
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiNav<>), typeof(UiFluentNavPresentation))]
[UiComponent("Nav", UiComponentExposure.Fluent)]
public partial class UiFluentNav;
