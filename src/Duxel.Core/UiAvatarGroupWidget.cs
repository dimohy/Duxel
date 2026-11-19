namespace Duxel.Core;

public enum UiAvatarGroupLayout
{
    Spread, Stack, Pie
}

/// <summary>Immutable member description. Image pixels and icon painters are borrowed.</summary>
public readonly record struct UiAvatarGroupItem(string Value, string Name)
{
    public string? Initials
    {
        get; init;
    }
    public UiImageSource? Source
    {
        get; init;
    }
    public UiMenuIcon Icon
    {
        get; init;
    }
    public UiAvatarColor Color { get; init; } = UiAvatarColor.Colorful;
    public bool ShowActivityRing
    {
        get; init;
    }
    public UiAvatarPresenceOptions? Presence
    {
        get; init;
    }
}

public readonly record struct UiAvatarGroupOptions(UiAvatarSize Size = UiAvatarSize.Size32,
    UiAvatarGroupLayout Layout = UiAvatarGroupLayout.Spread, int MaximumInlineItems = 5,
    bool RightToLeft = false, string OverflowLabel = "More people")
{
    public UiAvatarGroupOptions() : this(UiAvatarSize.Size32, UiAvatarGroupLayout.Spread, 5, false, "More people") { }
    internal void Validate()
    {
        new UiAvatarOptions(Size).Validate();
        if ((uint)Layout > 2 || MaximumInlineItems < 1)
            throw new ArgumentOutOfRangeException(nameof(UiAvatarGroupOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(OverflowLabel);
    }
}

public interface IUiAvatarGroupPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float Spacing(UiAvatarGroupOptions options);
    static abstract float Divider(UiAvatarGroupOptions options);
    static abstract void PaintSeparator(UiControlContext context, UiRect bounds, UiAvatarGroupOptions options);
    static abstract void PaintAvatar(UiControlContext context, UiRect bounds, UiAvatarContent content,
        UiAvatarOptions avatarOptions, bool squareEdges);
    static abstract void PaintOverflow(UiControlContext context, UiRect bounds, string count,
        UiButtonInteraction interaction, UiAvatarGroupOptions options);
}

internal readonly record struct UiAvatarGroupLayoutResult(UiVector2 Size, int[] Inline,
    UiRect[] Bounds, UiRect[] Bodies, UiRect OverflowBounds, UiMenuOption[] Overflow, string Count,
    UiAvatarGroupOptions Options, string OverflowName, string[] Identifiers);

/// <summary>Owns avatar resources and reuses the shared accessible menu for hidden members.</summary>
public class UiAvatarGroup<TPresentation> : UiControlWidget where TPresentation : IUiAvatarGroupPresentation
{
    private UiAvatarGroupItem[] _items;
    private UiFluentAvatar[] _avatars;
    private readonly UiMenuIcon[] _menuIcons;
    private readonly string[] _identifiers;
    private readonly UiOwnedMenuState _input = new();
    private UiAvatarGroupOptions _options;
    private UiRetainedLayout<UiAvatarGroupLayoutResult>? _retained;
    private Action<string>? _selected;
    public string Label
    {
        get;
    }
    public ReadOnlySpan<UiAvatarGroupItem> Items => _items;
    public bool IsOverflowOpen => _input.Menu.Open;
    public UiAvatarGroupOptions Options
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
            ApplyAvatarOptions();
            RequestRebuild();
        }
    }
    public UiAvatarGroup(string label, UiAvatarGroupItem[] items, UiAvatarGroupOptions? options = null,
        Action<string>? selected = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(items);
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Value) || string.IsNullOrWhiteSpace(item.Name)
                || UiAccessibilityTree.ContainsControl(item.Name, false) || !values.Add(item.Value))
                throw new ArgumentException("Group members require unique values and nonempty single-line names.", nameof(items));
            new UiAvatarOptions(Color: item.Color, Presence: item.Presence).Validate();
        }
        _options = options ?? new();
        _options.Validate();
        Label = label;
        _items = items.ToArray();
        _selected = selected;
        _avatars = new UiFluentAvatar[items.Length];
        _menuIcons = new UiMenuIcon[items.Length];
        _identifiers = Enumerable.Range(0, items.Length).Select(i => "##avatar-" + i).ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            var avatar = _avatars[i] = Fluent.Avatar(items[i].Name);
            avatar.Initials = items[i].Initials;
            avatar.Source = items[i].Source;
            avatar.Icon = items[i].Icon;
            OwnChild(i.ToString(System.Globalization.CultureInfo.InvariantCulture), avatar);
            _menuIcons[i] = new((context, bounds, _, _, _) => UiFluentAvatarPresentation.Paint(context,
                bounds, avatar.PreparePaintContent(), avatar.Options with
                {
                    Size = UiAvatarSize.Size32,
                    ShowActivityRing = false,
                    Presence = null
                }));
        }
        ApplyAvatarOptions();
    }
    private void ApplyAvatarOptions()
    {
        for (var i = 0; i < _avatars.Length; i++)
            _avatars[i].Options = new(_options.Size, Color: _items[i].Color, RightToLeft: _options.RightToLeft,
                ShowActivityRing: _options.Layout != UiAvatarGroupLayout.Pie && _items[i].ShowActivityRing,
                Presence: _options.Layout == UiAvatarGroupLayout.Spread ? _items[i].Presence : null);
    }
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    private UiAvatarGroupLayoutResult MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = TPresentation.CanRetainMeasurement && context.CanRetainLayoutMeasurement;
        if (_retained is { } prior && prior.TryGet(context, render, out var cached, allowed))
            return cached;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var options = _options;
        var edge = new UiAvatarOptions(options.Size).PixelSize;
        var pie = options.Layout == UiAvatarGroupLayout.Pie;
        var count = pie ? Math.Min(3, _items.Length) : Math.Min(_items.Length,
            options.MaximumInlineItems - (_items.Length > options.MaximumInlineItems ? 1 : 0));
        var hidden = pie ? _items.Length : _items.Length - count;
        var indices = pie ? Enumerable.Range(0, count).ToArray() : Enumerable.Range(hidden, count).ToArray();
        var spacing = TPresentation.Spacing(options);
        var slots = count + (!pie && hidden > 0 ? 1 : 0);
        var width = slots == 0 ? 0 : pie ? edge : slots * edge + (slots - 1) * spacing;
        var rects = new UiRect[count];
        var bodies = new UiRect[count];
        var left = 0f;
        var top = 0f;
        var right = width;
        var bottom = slots == 0 ? 0f : edge;
        var outline = options.Layout == UiAvatarGroupLayout.Stack ? TPresentation.Divider(options) : 0;
        for (var i = 0; i < count; i++)
        {
            var x = i * (edge + spacing);
            var body = bodies[i] = new(pie ? 0 : options.RightToLeft ? width - x - edge : x, 0, edge, edge);
            var avatar = UiFluentAvatarPresentation.NaturalLayout(_avatars[indices[i]].Options);
            var visual = rects[i] = new(body.X - avatar.Body.X, -avatar.Body.Y, avatar.Extent.X, avatar.Extent.Y);
            left = MathF.Min(left, MathF.Min(visual.X, body.X - outline));
            top = MathF.Min(top, MathF.Min(visual.Y, body.Y - outline));
            right = MathF.Max(right, MathF.Max(visual.X + visual.Width, body.X + edge + outline));
            bottom = MathF.Max(bottom, MathF.Max(visual.Y + visual.Height, edge + outline));
        }
        var overflowX = count * (edge + spacing);
        var trigger = pie ? new UiRect(0, 0, edge, edge)
            : new UiRect(options.RightToLeft ? width - overflowX - edge : overflowX, 0, edge, edge);
        if (!pie && hidden > 0)
        {
            left = MathF.Min(left, trigger.X - outline);
            top = MathF.Min(top, -outline);
            right = MathF.Max(right, trigger.X + edge + outline);
            bottom = MathF.Max(bottom, edge + outline);
        }
        for (var i = 0; i < count; i++)
        {
            rects[i] = rects[i] with
            {
                X = rects[i].X - left,
                Y = rects[i].Y - top
            };
            bodies[i] = bodies[i] with
            {
                X = bodies[i].X - left,
                Y = bodies[i].Y - top
            };
        }
        trigger = trigger with
        {
            X = trigger.X - left,
            Y = trigger.Y - top
        };
        var size = context.LayoutConstraints.Constrain(new(right - left, bottom - top));
        var overflow = hidden == 0 ? [] : Enumerable.Range(0, hidden).Select(i =>
            new UiMenuOption(_items[i].Value, _items[i].Name) { Icon = _menuIcons[i] with { SizeHint = edge } }).ToArray();
        var hiddenText = hidden.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = new UiAvatarGroupLayoutResult(size, indices, rects, bodies, trigger, overflow,
            "+" + hiddenText, options, options.OverflowLabel + " (" + hiddenText + ")", _identifiers);
        cache.Commit(context, attempt, result, size, allowed);
        return result;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context).Size;
    protected virtual void OnSelected(string value) => _selected?.Invoke(value);
    protected override void RenderControl(UiImmediateContext ui)
    {
        _input.Attach(ui);
        var value = ui.RenderAvatarGroup<TPresentation>(PrimaryInputId(Label), Label, _avatars,
            MeasureLayout(new(ui), true), _input.Menu);
        if (value is not null)
            OnSelected(value);
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Dispose();
        _avatars = [];
        _items = [];
        _retained = null;
        _selected = null;
        Array.Clear(_menuIcons);
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiAvatarGroup<>), typeof(UiFluentAvatarGroupPresentation))]
[UiComponent("AvatarGroup", UiComponentExposure.Fluent)]
public partial class UiFluentAvatarGroup;
