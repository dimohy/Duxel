namespace Duxel.Core;

public enum UiPersonaSize
{
    Medium, ExtraSmall, Small, Large, ExtraLarge, Huge
}
public enum UiPersonaTextPosition
{
    After, Before, Below
}
public enum UiPersonaTextAlignment
{
    Start, Center
}
public readonly record struct UiPersonaOptions(UiPersonaSize Size = UiPersonaSize.Medium,
    UiPersonaTextPosition TextPosition = UiPersonaTextPosition.After,
    UiPersonaTextAlignment TextAlignment = UiPersonaTextAlignment.Start,
    bool PresenceOnly = false, bool RightToLeft = false, bool ShowMedia = true)
{
    public UiPersonaOptions() : this(UiPersonaSize.Medium, UiPersonaTextPosition.After, UiPersonaTextAlignment.Start, false, false, true) { }
    internal void Validate()
    {
        if ((uint)Size > 5 || (uint)TextPosition > 2 || (uint)TextAlignment > 1)
            throw new ArgumentOutOfRangeException(nameof(UiPersonaOptions));
    }
}
public readonly record struct UiPersonaContent(string Name, string? PrimaryText = null, string? SecondaryText = null,
    string? TertiaryText = null, string? QuaternaryText = null)
{
    public string Primary => PrimaryText ?? Name;
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Name);
        foreach (var text in new[] { Name, PrimaryText, SecondaryText, TertiaryText, QuaternaryText })
            if (text is not null && UiAccessibilityTree.ContainsControl(text, false))
                throw new ArgumentException("Persona text must be single-line.", nameof(UiPersonaContent));
    }
}
public readonly record struct UiPersonaLayout(UiVector2 Size, UiRect Media, UiControlTextBlock[] Text, UiRect[] TextBounds);
public interface IUiPersonaPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiAvatarSize AvatarSize(UiPersonaOptions options);
    static abstract UiPresenceBadgeOptions Presence(UiAvatarPresenceOptions presence, UiPersonaOptions options);
    static abstract UiVector2 MeasurePresence(UiPresenceBadgeOptions presence);
    static abstract UiPersonaLayout Measure(UiControlContext context, UiPersonaContent content, UiPersonaOptions options, UiVector2 media);
    static abstract void PaintText(UiControlContext context, UiPersonaLayout layout, UiPersonaOptions options, UiVector2 origin);
    static abstract void PaintPresence(UiControlContext context, UiRect bounds, UiPresenceBadgeOptions presence);
}

/// <summary>Passive person information with one owned avatar and one consumer-owned visual binding.</summary>
public class UiPersona<TPresentation, TAvatarPresentation> : UiControlWidget
    where TPresentation : IUiPersonaPresentation where TAvatarPresentation : IUiAvatarPresentation
{
    private UiPersonaContent _content;
    private UiPersonaOptions _options;
    private UiAvatar<TAvatarPresentation>? _avatar;
    private UiAvatarPaintBinding<TAvatarPresentation>? _paint;
    private UiRetainedLayout<(UiPersonaLayout Layout, LayoutKey Key)>? _retained;
    private string _accessibleText;
    private string? _presenceText;
    private string? _activityText;
    private string? _accessibleName;
    private readonly record struct LayoutKey(UiPersonaContent Content, UiPersonaOptions Options, UiAvatarOptions Avatar);
    public UiAvatar<TAvatarPresentation> Avatar => _avatar ?? throw new ObjectDisposedException(nameof(UiPersona<TPresentation, TAvatarPresentation>));
    public UiPersonaContent Content
    {
        get => _content;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_content == value)
                return;
            ThrowIfBuilding();
            _content = value;
            Avatar.Name = value.Name;
            UpdateAccessibleText();
            RequestRebuild();
        }
    }
    public UiPersonaOptions Options
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
    public UiPersona(UiPersonaContent content, UiPersonaOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        content.Validate();
        _content = content;
        _options = options ?? new();
        _options.Validate();
        _avatar = new(content.Name);
        OwnChild("avatar", _avatar);
        _paint = new(_avatar);
        _accessibleText = string.Empty;
        UpdateAccessibleText();
    }
    public UiPersona(string name, UiPersonaOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(new UiPersonaContent(name), options, identity) { }
    private void UpdateAccessibleText()
    {
        _accessibleText = string.Join(", ", new[] { _content.Primary, _content.SecondaryText, _content.TertiaryText, _content.QuaternaryText }
            .Where(static text => !string.IsNullOrEmpty(text)));
        _accessibleName = null;
    }
    private UiAvatarOptions ResolvedAvatar => Avatar.Options with { Size = TPresentation.AvatarSize(_options), RightToLeft = _options.RightToLeft };
    private LayoutKey CurrentKey => new(_content, _options, ResolvedAvatar);
    protected override bool CanRetainMeasurement => _retained is { IsValid: true } cache && cache.CachedResult.Key == CurrentKey;
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    private UiPersonaLayout MeasureLayout(UiControlContext context, bool render)
    {
        var key = CurrentKey;
        var cache = _retained ??= new();
        if (cache.IsValid && cache.CachedResult.Key != key)
            cache.Invalidate();
        var allowed = TPresentation.CanRetainMeasurement && TAvatarPresentation.CanRetainMeasurement && context.CanRetainLayoutMeasurement;
        if (cache.TryGet(context, render, out var found, allowed) && found.Key == key)
            return found.Layout;
        var attempt = cache.Begin(context);
        var media = !_options.ShowMedia ? default : _options.PresenceOnly
            ? key.Avatar.Presence is { } presence ? TPresentation.MeasurePresence(TPresentation.Presence(presence, _options)) : default
            : TAvatarPresentation.Measure(context, key.Avatar.VisibleOptions);
        UiAvatar<TAvatarPresentation>.ValidateSize(media);
        var layout = TPresentation.Measure(context, _content, _options, media);
        UiAvatar<TAvatarPresentation>.ValidateSize(layout.Size);
        if (layout.Text is null || layout.TextBounds is null || layout.Text.Length != 4 || layout.TextBounds.Length != 4)
            throw new InvalidOperationException("Persona presentation must return four text slots.");
        cache.Commit(context, attempt, (layout, key), layout.Size, allowed);
        return layout;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context, false).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var layout = MeasureLayout(context, true);
        var avatar = ResolvedAvatar;
        var presenceText = _options.ShowMedia && avatar.Presence is not null ? Avatar.ResolvedPresenceText : string.Empty;
        var activityText = _options.ShowMedia && !_options.PresenceOnly
            && (avatar.VisibleOptions.ShowActivityRing || avatar.VisibleOptions.ShowActivityShadow) ? Avatar.ActivityText : string.Empty;
        if (_presenceText != presenceText || _activityText != activityText)
        {
            _presenceText = presenceText;
            _activityText = activityText;
            _accessibleName = null;
        }
        if (_accessibleName is null)
        {
            var name = _accessibleText;
            if (presenceText.Length > 0)
                name = name.Length == 0 ? presenceText : name + ", " + presenceText;
            if (activityText.Length > 0)
                name = name.Length == 0 ? activityText : name + ", " + activityText;
            _accessibleName = name;
        }
        ui.RenderPersona<TPresentation, TAvatarPresentation>(layout, _options, avatar, _paint!, _accessibleName);
    }
    private protected override void ReleaseOwnedResources()
    {
        _paint?.Dispose();
        _paint = null;
        _avatar = null;
        _retained = null;
        _content = default;
        _accessibleText = string.Empty;
        _accessibleName = _presenceText = _activityText = null;
        base.ReleaseOwnedResources();
    }
}
[UiWidgetStyle(typeof(UiPersona<,>), typeof(UiFluentPersonaPresentation), typeof(UiFluentAvatarPresentation))]
[UiComponent("Persona", UiComponentExposure.Fluent)]
public partial class UiFluentPersona;

public sealed partial class UiImmediateContext
{
    internal void RenderPersona<TPresentation, TAvatarPresentation>(UiPersonaLayout layout, UiPersonaOptions options,
        UiAvatarOptions avatarOptions, UiAvatarPaintBinding<TAvatarPresentation> paint, string name)
        where TPresentation : IUiPersonaPresentation where TAvatarPresentation : IUiAvatarPresentation
    {
        var origin = AllocateLayoutBox(layout.Size);
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var context = new UiControlContext(this);
        try
        {
            using var clip = context.Clip(bounds);
            var media = layout.Media with
            {
                X = origin.X + layout.Media.X,
                Y = origin.Y + layout.Media.Y
            };
            if (media.Width > 0 && media.Height > 0)
            {
                if (options.PresenceOnly)
                {
                    if (avatarOptions.Presence is { } presence)
                        TPresentation.PaintPresence(context, media, TPresentation.Presence(presence, options));
                }
                else
                    paint.Paint(context, media, avatarOptions);
            }
            TPresentation.PaintText(context, layout, options, origin);
            ExposeAccessibility(ResolveId(UiControlWidget.PrimaryControlId), _accessibilityParent, UiAccessibilityRole.Group, name,
                bounds, focusable: false, valuePattern: false, literalLabel: true);
        }
        finally { FinishLayoutBox(origin, layout.Size); }
    }
}
