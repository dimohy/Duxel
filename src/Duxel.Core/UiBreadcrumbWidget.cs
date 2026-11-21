using System.Globalization;

namespace Duxel.Core;

public enum UiBreadcrumbSize
{
    Medium, Small, Large
}
public enum UiBreadcrumbFocusMode
{
    Tab, Arrow
}
public readonly record struct UiBreadcrumbOption(string Value, string Label, bool Enabled = true, bool DisabledFocusable = true)
{
    public UiMenuIcon Icon
    {
        get; init;
    }
}
public readonly record struct UiBreadcrumbOptions(UiBreadcrumbSize Size = UiBreadcrumbSize.Medium,
    UiBreadcrumbFocusMode FocusMode = UiBreadcrumbFocusMode.Tab, bool RightToLeft = false,
    int MaximumCharacters = 30, int TooltipMaximumCharacters = 80,
    string OverflowLabel = "More levels", string CurrentLabel = "Current location")
{
    public UiBreadcrumbOptions() : this(UiBreadcrumbSize.Medium, UiBreadcrumbFocusMode.Tab, false, 30, 80,
        "More levels", "Current location")
    {
    }
    internal UiBreadcrumbOptions Validate()
    {
        if ((uint)Size > 2 || (uint)FocusMode > 1 || MaximumCharacters < 1 || TooltipMaximumCharacters < 1)
            throw new ArgumentOutOfRangeException(nameof(UiBreadcrumbOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(OverflowLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(CurrentLabel);
        return this;
    }
}
public readonly record struct UiBreadcrumbItemContent(string Text, UiMenuIcon Icon, bool Current);

public enum UiBreadcrumbFocusCommand
{
    None, Previous, Next, First, Last
}

/// <summary>Selects the visible levels' pointer activation and focus navigation commands.</summary>
public interface IUiBreadcrumbBehavior
{
    static abstract UiButtonActivation ItemActivation { get; }
    static abstract UiBreadcrumbFocusCommand ResolveFocusCommand(UiKey key, bool rightToLeft);
}

public readonly struct UiDefaultBreadcrumbBehavior : IUiBreadcrumbBehavior
{
    public static UiButtonActivation ItemActivation => UiButtonActivation.Release;
    public static UiBreadcrumbFocusCommand ResolveFocusCommand(UiKey key, bool rightToLeft) => key switch
    {
        UiKey.LeftArrow => rightToLeft ? UiBreadcrumbFocusCommand.Next : UiBreadcrumbFocusCommand.Previous,
        UiKey.RightArrow => rightToLeft ? UiBreadcrumbFocusCommand.Previous : UiBreadcrumbFocusCommand.Next,
        UiKey.Home => UiBreadcrumbFocusCommand.First,
        UiKey.End => UiBreadcrumbFocusCommand.Last,
        _ => UiBreadcrumbFocusCommand.None
    };
}

public interface IUiBreadcrumbPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float Height(UiBreadcrumbOptions options);
    static abstract float DividerWidth(UiBreadcrumbOptions options);
    static abstract UiVector2 MeasureItem(UiControlContext context, UiBreadcrumbItemContent item, UiBreadcrumbOptions options);
    static abstract void PaintItem(UiControlContext context, UiBreadcrumbItemContent item, UiRect bounds,
        UiButtonInteraction interaction, UiBreadcrumbOptions options);
    static abstract void PaintDivider(UiControlContext context, UiRect bounds, UiBreadcrumbOptions options);
}

internal readonly record struct UiBreadcrumbLayout(UiVector2 Size, int[] Indices, UiRect[] Bounds,
    UiBreadcrumbItemContent[] Content, string[] TooltipText, UiMenuOption[] Overflow, string OverflowTooltip, UiBreadcrumbOptions Options);

/// <summary>Owns a hierarchical path, responsive overflow and stable input identities.</summary>
public class UiBreadcrumb<TBehavior, TPresentation> : UiControlWidget
    where TBehavior : IUiBreadcrumbBehavior
    where TPresentation : IUiBreadcrumbPresentation
{
    private UiBreadcrumbOption[] _items;
    private UiBreadcrumbOptions _options;
    private UiRetainedLayout<UiBreadcrumbLayout>? _retained;
    private Action<string>? _navigated;
    private readonly UiBreadcrumbInputState _input;
    public string Label
    {
        get;
    }
    public ReadOnlySpan<UiBreadcrumbOption> Items => _items;
    public bool IsOverflowOpen => _input.Menu.Open;
    public UiBreadcrumbOptions Options
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
    public UiBreadcrumb(string label, UiBreadcrumbOption[] items, Action<string>? navigated = null,
        UiBreadcrumbOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length < 2)
            throw new ArgumentException("A breadcrumb path requires at least two levels.", nameof(items));
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
            if (string.IsNullOrWhiteSpace(item.Value) || string.IsNullOrWhiteSpace(item.Label)
                || UiAccessibilityTree.ContainsControl(item.Label, false) || !values.Add(item.Value))
                throw new ArgumentException("Breadcrumb levels require unique nonempty values and single-line labels.", nameof(items));
        _options = (options ?? new()).Validate();
        _items = items.ToArray();
        _navigated = navigated;
        Label = label;
        _input = new(_items);
    }
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    private UiBreadcrumbLayout MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = TPresentation.CanRetainMeasurement && context.CanRetainLayoutMeasurement;
        if (_retained is { } prior && prior.TryGet(context, render, out var found, allowed))
            return found;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var options = _options with { RightToLeft = _options.RightToLeft || context.RightToLeft };
        var constraints = context.LayoutConstraints.Validate();
        var content = new UiBreadcrumbItemContent[_items.Length];
        var tooltips = new string[_items.Length];
        var widths = new float[_items.Length];
        var height = TPresentation.Height(options);
        var divider = TPresentation.DividerWidth(options);
        if (!float.IsFinite(height) || height <= 0 || !float.IsFinite(divider) || divider < 0)
            throw new InvalidOperationException("Breadcrumb presentation returned invalid metrics.");
        double natural = divider * (double)(_items.Length - 1);
        for (var i = 0; i < _items.Length; i++)
        {
            content[i] = new(Truncate(_items[i].Label, options.MaximumCharacters), _items[i].Icon, i == _items.Length - 1);
            tooltips[i] = content[i].Text == _items[i].Label ? string.Empty : Truncate(_items[i].Label, options.TooltipMaximumCharacters);
            var size = TPresentation.MeasureItem(context, content[i], options);
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
                throw new InvalidOperationException("Breadcrumb presentation returned invalid item extents.");
            widths[i] = size.X;
            height = MathF.Max(height, size.Y);
            natural += size.X;
        }
        var overflowWidth = height;
        var hidden = 0;
        var desired = natural;
        while (desired > constraints.MaxWidth && hidden < _items.Length - 2)
        {
            hidden++;
            desired -= widths[hidden];
            desired += hidden == 1 ? overflowWidth : -divider;
        }
        var currentOnly = desired > constraints.MaxWidth;
        if (currentOnly)
            desired = Math.Min(widths[^1], constraints.MaxWidth);
        if (desired > float.MaxValue)
            throw new InvalidOperationException("Breadcrumb exceeds finite layout capacity.");
        int[] indices = currentOnly ? [_items.Length - 1]
            : hidden == 0 ? Enumerable.Range(0, _items.Length).ToArray()
            : new[] { 0, -1 }.Concat(Enumerable.Range(hidden + 1, _items.Length - hidden - 1)).ToArray();
        UiMenuOption[] overflow = hidden == 0 || currentOnly ? [] : _items.AsSpan(1, hidden).ToArray()
            .Select(item => new UiMenuOption(item.Value, item.Label, item.Enabled) { Icon = item.Icon }).ToArray();
        var sizeResult = constraints.Constrain(new((float)desired, height));
        var rects = new UiRect[indices.Length];
        var cursor = 0f;
        for (var i = 0; i < indices.Length; i++)
        {
            var width = indices[i] < 0 ? overflowWidth : currentOnly ? (float)desired : widths[indices[i]];
            var x = options.RightToLeft ? sizeResult.X - cursor - width : cursor;
            rects[i] = new(x, (sizeResult.Y - height) / 2, width, height);
            cursor += width + divider;
        }
        var overflowTooltip = overflow.Length == 0 ? string.Empty
            : Truncate(string.Join(" / ", overflow.Select(value => value.Label)), options.TooltipMaximumCharacters);
        var result = new UiBreadcrumbLayout(sizeResult, indices, rects, content, tooltips, overflow, overflowTooltip, options);
        cache.Commit(context, attempt, result, sizeResult, allowed);
        return result;
    }
    internal static string Truncate(string text, int maximum)
    {
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        return boundaries.Length <= maximum ? text : maximum == 1 ? "…" : text[..boundaries[maximum - 1]] + "…";
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context).Size;
    protected virtual void OnNavigated(string value) => _navigated?.Invoke(value);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var layout = MeasureLayout(new(ui), true);
        _input.Attach(ui);
        var value = ui.RenderBreadcrumb<TBehavior, TPresentation>(PrimaryInputId(Label), Label, _items, layout, _input);
        if (value is not null)
            OnNavigated(value);
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Dispose();
        _items = [];
        _retained = null;
        _navigated = null;
        base.ReleaseOwnedResources();
    }
}

public class UiBreadcrumb<TPresentation>(string label, UiBreadcrumbOption[] items, Action<string>? navigated = null,
    UiBreadcrumbOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
    : UiBreadcrumb<UiDefaultBreadcrumbBehavior, TPresentation>(label, items, navigated, options, identity)
    where TPresentation : IUiBreadcrumbPresentation;

[UiWidgetStyle(typeof(UiBreadcrumb<>), typeof(UiFluentBreadcrumbPresentation))]
[UiComponent("Breadcrumb", UiComponentExposure.Fluent)]
public partial class UiFluentBreadcrumb;

internal sealed class UiBreadcrumbInputState : IDisposable
{
    private readonly UiOwnedMenuState _owner = new();
    internal UiMenuInputState Menu => _owner.Menu;
    internal readonly string[] ItemNames;
    internal readonly UiControlTooltip[] Tooltips;
    internal string? Root;
    internal string[] ItemIds = [];
    internal string[] ScopedNames = [];
    internal readonly Dictionary<string, int> IndexById = new(StringComparer.Ordinal);
    internal UiBreadcrumbInputState(UiBreadcrumbOption[] items)
    {
        ItemNames = items.Select(item => "##level-" + Convert.ToHexString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(item.Value.AsSpan()))).ToArray();
        Tooltips = Enumerable.Range(0, items.Length + 1).Select(_ => new UiControlTooltip()).ToArray();
    }
    internal void Attach(UiImmediateContext ui)
    {
        if (!_owner.Attach(ui))
            return;
        foreach (var tooltip in Tooltips)
            tooltip.Attach(ui);
    }
    public void Dispose()
    {
        _owner.Dispose();
        foreach (var tooltip in Tooltips)
            tooltip.Dispose();
        ItemIds = [];
        ScopedNames = [];
        IndexById.Clear();
        Root = null;
    }
}
