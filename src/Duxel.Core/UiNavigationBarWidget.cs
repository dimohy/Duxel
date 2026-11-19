namespace Duxel.Core;

public enum UiNavigationBarAppearance
{
    Neutral, Transparent, Brand
}

public readonly record struct UiNavigationBarOptions(float? Width = null, bool RightToLeft = false,
    UiNavigationBarAppearance Appearance = UiNavigationBarAppearance.Neutral)
{
    internal void Validate()
    {
        if (Width is { } width && (!float.IsFinite(width) || width <= 0) || (uint)Appearance > 2)
            throw new ArgumentOutOfRangeException(nameof(UiNavigationBarOptions));
    }
}

/// <summary>A contextual header that owns its page actions and optional secondary control.</summary>
public class UiNavigationBarWidget : UiControlWidget
{
    private const float Spacing = 8;
    private const float HorizontalPadding = 12;
    private const float VerticalPadding = 8;
    private const float MinimumHeight = 48;
    private readonly UiRetainedLayout<Layout> _layout = new();
    private UiFluentSubtitle2 _title;
    private UiWidget? _leading, _trailing, _secondary;
    private UiNavigationBarOptions _options;

    public UiNavigationBarWidget(string label, string? title = null, UiWidget? leading = null,
        UiWidget? trailing = null, UiWidget? secondary = null, UiNavigationBarOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        options.Validate();
        if (leading is not null && (ReferenceEquals(leading, trailing) || ReferenceEquals(leading, secondary))
            || trailing is not null && ReferenceEquals(trailing, secondary))
            throw new ArgumentException("Navigation bar slots require distinct widgets.");
        if (leading is not null) ValidateOwnedChild("leading", leading);
        if (trailing is not null) ValidateOwnedChild("trailing", trailing);
        if (secondary is not null) ValidateOwnedChild("secondary", secondary);
        Label = label;
        _options = options;
        _title = Fluent.Subtitle2(title ?? string.Empty);
        _leading = leading;
        _trailing = trailing;
        _secondary = secondary;
        OwnChild("title", _title);
        if (_leading is not null) OwnChild("leading", _leading);
        if (_trailing is not null) OwnChild("trailing", _trailing);
        if (_secondary is not null) OwnChild("secondary", _secondary);
    }

    public string Label { get; }
    public string Title
    {
        get { ThrowIfDisposed(); return _title.Source; }
        set { ThrowIfDisposed(); _title.Source = value; }
    }
    public UiNavigationBarOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            _options = value;
            RequestRebuild();
        }
    }

    private UiFluentProviderTheme? ScopedTheme =>
        _options.Appearance == UiNavigationBarAppearance.Brand ? UiFluentProviderTheme.Dark : null;

    protected override bool CanRetainMeasurement => _layout.IsValid;
    internal override void InvalidateRetainedMeasurement() => _layout.Invalidate();
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        if (ScopedTheme is not { } theme)
            return Arrange(context, false).Size;
        using var scope = context.BeginFluentProvider(theme);
        return Arrange(context, false).Size;
    }

    private Layout Arrange(UiControlContext context, bool render)
    {
        var allowed = context.CanRetainLayoutMeasurement;
        if (_layout.TryGet(context, render, out var cached, allowed))
            return cached;
        var attempt = _layout.Begin(context);
        var layout = MeasureSlots(context);
        _layout.Commit(context, attempt, layout, layout.Size, allowed && _title.RetainsMeasurement
            && _leading?.RetainsMeasurement != false && _trailing?.RetainsMeasurement != false
            && _secondary?.RetainsMeasurement != false);
        return layout;
    }

    private Layout MeasureSlots(UiControlContext context)
    {
        var constraints = context.LayoutConstraints;
        var rightToLeft = _options.RightToLeft || context.RightToLeft;
        var boundedWidth = MathF.Min(_options.Width ?? float.PositiveInfinity, constraints.MaxWidth);
        var available = MathF.Max(0, boundedWidth - 2 * HorizontalPadding);
        UiVector2 Measure(UiWidget? child) => child is null ? default
            : context.MeasureConstrained(child, new(0, available, 0, float.PositiveInfinity));
        var leading = Measure(_leading);
        var trailing = Measure(_trailing);
        var title = _title.Source.Length == 0 ? default : Measure(_title);
        var secondary = Measure(_secondary);
        var sideSpacing = leading.X > 0 && trailing.X > 0 ? Spacing : 0;
        var sideWidth = leading.X + trailing.X + sideSpacing;
        var centeredWidth = title.X == 0 ? sideWidth : MathF.Max(sideWidth,
            title.X + 2 * (MathF.Max(leading.X, trailing.X) + (sideWidth > 0 ? Spacing : 0)));
        var natural = MathF.Max(centeredWidth, secondary.X) + 2 * HorizontalPadding;
        var width = Math.Clamp(float.IsFinite(boundedWidth) ? boundedWidth : natural, constraints.MinWidth, constraints.MaxWidth);
        var padding = MathF.Min(HorizontalPadding, width / 2);
        var innerWidth = width - 2 * padding;
        var titleOnMainRow = centeredWidth <= innerWidth;
        var actionsShareRow = sideWidth <= innerWidth;
        var mainHeight = MathF.Max(MathF.Max(leading.Y, actionsShareRow ? trailing.Y : 0),
            titleOnMainRow ? title.Y : 0);
        var leadingBounds = Side(leading, VerticalPadding + (mainHeight - leading.Y) / 2, leadingSide: true);
        var trailingBounds = Side(trailing, actionsShareRow
            ? VerticalPadding + (mainHeight - trailing.Y) / 2
            : VerticalPadding + mainHeight + Spacing, leadingSide: false);
        var bottom = VerticalPadding + mainHeight + (actionsShareRow ? 0 : Spacing + trailing.Y);
        var titleBounds = Center(title, titleOnMainRow ? VerticalPadding + (mainHeight - title.Y) / 2 : bottom + Spacing);
        if (!titleOnMainRow && title.Y > 0)
            bottom = titleBounds.Y + titleBounds.Height;
        var secondaryBounds = Center(secondary, bottom + (bottom > VerticalPadding ? Spacing : 0));
        if (secondary.Y > 0)
            bottom = secondaryBounds.Y + secondaryBounds.Height;
        var size = constraints.Constrain(new(width, MathF.Max(MinimumHeight, bottom + VerticalPadding)));
        return new(size, leadingBounds, titleBounds, trailingBounds, secondaryBounds);

        UiRect Side(UiVector2 child, float y, bool leadingSide) =>
            new(leadingSide != rightToLeft ? padding : width - padding - child.X, y, child.X, child.Y);
        UiRect Center(UiVector2 child, float y) => new((width - child.X) / 2, y, child.X, child.Y);
    }

    protected override void RenderControl(UiImmediateContext ui)
    {
        if (ScopedTheme is { } theme)
        {
            using var scope = ui.BeginFluentProvider(theme);
            RenderBar(ui);
        }
        else
            RenderBar(ui);
    }

    private void RenderBar(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var layout = Arrange(context, true);
        var origin = ui.AllocateLayoutBox(layout.Size);
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        using var clip = context.Clip(bounds);
        using var scope = ui.BeginNavigationBar(PrimaryInputId(Label), Label, bounds, _options.Appearance);
        Render(_leading, layout.Leading);
        Render(_title, layout.Title);
        Render(_trailing, layout.Trailing);
        Render(_secondary, layout.Secondary);
        ui.FinishLayoutBox(origin, layout.Size);

        void Render(UiWidget? child, UiRect rect)
        {
            if (child is not null && rect.Width > 0 && rect.Height > 0 && bounds.Height > 0)
                ui.RenderLayoutChild(child, rect with { X = origin.X + rect.X, Y = origin.Y + rect.Y }, bounds);
        }
    }

    private readonly record struct Layout(UiVector2 Size, UiRect Leading, UiRect Title, UiRect Trailing, UiRect Secondary);

    private protected override void ReleaseOwnedResources()
    {
        _title = null!;
        _leading = _trailing = _secondary = null;
        _layout.Clear();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiNavigationBarWidget))]
[UiComponent("NavigationBar", UiComponentExposure.Fluent)]
public partial class UiFluentNavigationBar;

public sealed partial class UiImmediateContext
{
    internal NavigationBarScope BeginNavigationBar(string id, string label, UiRect bounds, UiNavigationBarAppearance appearance)
    {
        var parent = _accessibilityParent;
        var root = ResolveId(id);
        ExposeAccessibility(root, parent, UiAccessibilityRole.Navigation, label, bounds,
            focusable: false, valuePattern: false, literalLabel: true);
        if (appearance == UiNavigationBarAppearance.Brand && _theme.ForcedColors is null)
        {
            var top = new UiColor(0, 75, 135);
            var bottom = new UiColor(0, 47, 96);
            _builder.AddRectFilledMultiColor(bounds, top, top, bottom, bottom);
        }
        else if (appearance != UiNavigationBarAppearance.Transparent || _theme.ForcedColors is not null)
            PaintButtonFrame(bounds, _theme.WindowBg, default, 0, 0);
        _accessibilityParent = root;
        return new(this, parent);
    }

    internal readonly struct NavigationBarScope(UiImmediateContext ui, string parent) : IDisposable
    {
        public void Dispose() => ui._accessibilityParent = parent;
    }
}
