namespace Duxel.Core;

public enum UiAvatarSize
{
    Size32, Size16 = 16, Size20 = 20, Size24 = 24, Size28 = 28, Size36 = 36,
    Size40 = 40, Size48 = 48, Size56 = 56, Size64 = 64, Size72 = 72,
    Size96 = 96, Size120 = 120, Size128 = 128
}
public enum UiAvatarShape
{
    Circular, Square
}
public enum UiAvatarColor
{
    Neutral, Brand, Colorful, DarkRed, Cranberry, Red, Pumpkin, Peach, Marigold, Gold,
    Brass, Brown, Forest, Seafoam, DarkGreen, LightTeal, Teal, Steel, Blue, RoyalBlue,
    Cornflower, Navy, Lavender, Purple, Grape, Lilac, Pink, Magenta, Plum, Beige, Mink,
    Platinum, Anchor
}
/// <summary>Null Size selects the badge size from the avatar body size.</summary>
public readonly record struct UiAvatarPresenceOptions(UiPresenceStatus Status = UiPresenceStatus.Available,
    UiBadgeSize? Size = null, bool OutOfOffice = false)
{
    public void Validate()
    {
        if ((uint)Status > 7 || (Size is { } size && (uint)size > 5))
            throw new ArgumentOutOfRangeException(nameof(UiAvatarPresenceOptions));
    }
    public UiPresenceBadgeOptions Resolve(int avatarSize)
    {
        Validate();
        return new(Status, Size ?? (avatarSize switch
        {
            >= 96 => UiBadgeSize.ExtraLarge,
            >= 64 => UiBadgeSize.Large,
            >= 56 => UiBadgeSize.Medium,
            >= 40 => UiBadgeSize.Small,
            >= 28 => UiBadgeSize.ExtraSmall,
            _ => UiBadgeSize.Tiny
        }), OutOfOffice);
    }
}
public readonly record struct UiAvatarOptions(UiAvatarSize Size = UiAvatarSize.Size32,
    UiAvatarShape Shape = UiAvatarShape.Circular, UiAvatarColor Color = UiAvatarColor.Neutral,
    bool RightToLeft = false, bool ShowActivityRing = false, UiAvatarPresenceOptions? Presence = null, bool ShowActivityShadow = false,
    bool IsInactive = false)
{
    internal UiAvatarOptions VisibleOptions => IsInactive ? this with { ShowActivityRing = false, ShowActivityShadow = false } : this;
    public int PixelSize => Size switch
    {
        UiAvatarSize.Size32 => 32,
        UiAvatarSize.Size16 or UiAvatarSize.Size20 or UiAvatarSize.Size24 or UiAvatarSize.Size28
            or UiAvatarSize.Size36 or UiAvatarSize.Size40 or UiAvatarSize.Size48 or UiAvatarSize.Size56
            or UiAvatarSize.Size64 or UiAvatarSize.Size72 or UiAvatarSize.Size96 or UiAvatarSize.Size120
            or UiAvatarSize.Size128 => (int)Size,
        _ => throw new ArgumentOutOfRangeException(nameof(Size))
    };
    public void Validate()
    {
        _ = PixelSize;
        Presence?.Validate();
        if ((uint)Shape > 1 || (uint)Color > (uint)UiAvatarColor.Anchor)
            throw new ArgumentOutOfRangeException(nameof(UiAvatarOptions));
    }
}

/// <summary>Image and icon are borrowed for painting. A presentation must not mutate or dispose them.</summary>
public readonly record struct UiAvatarContent(string Name, string Initials, UiMenuIcon Icon,
    UiImage? Image, int ColorIndex, string PresenceText = "", UiBoxShadow? Shadow = null);
public interface IUiAvatarPresentation
{
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Promises that painting depends only on supplied content/options and configured theme/typography.</summary>
    static virtual bool CanRetainPaint => false;
    static abstract UiVector2 Measure(UiControlContext context, UiAvatarOptions options);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options);
}

/// <summary>A passive named image with one owned image lease and a borrowed optional icon.</summary>
public class UiAvatar<TPresentation> : UiControlWidget where TPresentation : IUiAvatarPresentation
{
    private string _name;
    private string? _initials;
    private string _displayInitials = string.Empty;
    private string?[]? _mediaInitials;
    private string _activityText = "active";
    private string? _presenceText;
    private string _displayPresenceText = string.Empty;
    private string _accessibilityLabel = string.Empty;
    private string? _idForColor;
    private int _colorIndex;
    private UiMenuIcon _icon;
    private UiImage? _image;
    private UiBoxShadow? _shadow;
    private UiAvatarOptions _options;
    private UiAvatarPaintGroup<TPresentation>? _inactivePaint;

    public UiAvatar(string name = "", UiAvatarOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(name);
        options.Validate();
        _name = name;
        _options = options;
        UpdateDerivedContent();
    }
    public string Name
    {
        get => _name;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_name == value)
                return;
            ThrowIfBuilding();
            _name = value;
            UpdateDerivedContent();
            RequestRebuild();
        }
    }
    /// <summary>Null derives initials from Name; an empty string explicitly selects the icon.</summary>
    public string? Initials
    {
        get => _initials;
        set
        {
            ThrowIfDisposed();
            if (_initials == value)
                return;
            ThrowIfBuilding();
            _initials = value;
            UpdateDerivedContent();
            RequestRebuild();
        }
    }
    public string DisplayInitials => _displayInitials;
    /// <summary>Localized activity wording included in the default accessible name when the ring is shown.</summary>
    public string ActivityText
    {
        get => _activityText;
        set
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (_activityText == value)
                return;
            ThrowIfBuilding();
            _activityText = value;
            UpdateDerivedContent();
            RequestRebuild();
        }
    }

    /// <summary>Null uses the status wording; a nonempty value supplies the complete localized status.</summary>
    public string? PresenceText
    {
        get => _presenceText;
        set
        {
            ThrowIfDisposed();
            if (value is not null)
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (_presenceText == value)
                return;
            ThrowIfBuilding();
            _presenceText = value;
            UpdateDerivedContent();
            RequestRebuild();
        }
    }

    public string? IdForColor
    {
        get => _idForColor;
        set
        {
            ThrowIfDisposed();
            if (_idForColor == value)
                return;
            ThrowIfBuilding();
            _idForColor = value;
            UpdateDerivedContent();
            RequestRebuild();
        }
    }
    public UiAvatarOptions Options
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
            UpdateDerivedContent();
            RequestRebuild();
        }
    }
    public UiMenuIcon Icon
    {
        get => _icon;
        set
        {
            ThrowIfDisposed();
            if (_icon == value)
                return;
            ThrowIfBuilding();
            _icon = value;
            RequestRebuild();
        }
    }
    public UiImageSource? Source
    {
        get => _image?.Source;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_image?.Source, value))
                return;
            ThrowIfBuilding();
            if (value is null)
            {
                _image!.Dispose();
                _image = null;
            }
            else if (_image is null)
                _image = new(value);
            else
                _image.Source = value;
            RequestRebuild();
        }
    }
    private void UpdateDerivedContent()
    {
        if (_mediaInitials is not null)
            Array.Clear(_mediaInitials);
        _displayInitials = _initials ?? UiAvatarInitials.FromName(_name,
            _options.Size == UiAvatarSize.Size16, _options.RightToLeft);
        _colorIndex = UiAvatarPalette.ColorIndex(_idForColor ?? _name);
        var label = _name.Length > 0 ? _name : _displayInitials;
        _displayPresenceText = _options.Presence is { } presence
            ? _presenceText ?? presence.Resolve(_options.PixelSize).StatusText : string.Empty;
        if (_displayPresenceText.Length > 0)
            label = label.Length == 0 ? _displayPresenceText : label + " " + _displayPresenceText;
        _accessibilityLabel = _options.IsInactive || (!_options.ShowActivityRing && !_options.ShowActivityShadow) ? label
            : label.Length == 0 ? _activityText : label + " " + _activityText;
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var size = TPresentation.Measure(context, _options.VisibleOptions);
        ValidateSize(size);
        return context.HasBoxConstraints ? context.LayoutConstraints.Constrain(size) : size;
    }
    private void PreparePaintResources()
    {
        if (_options.VisibleOptions.ShowActivityShadow)
            _shadow ??= new(default);
        if (_options.IsInactive)
            _inactivePaint ??= new();
        else
        {
            _inactivePaint?.Dispose();
            _inactivePaint = null;
        }
    }
    internal void PaintMedia(UiControlContext context, UiRect bounds, UiAvatarOptions options,
        UiAvatarPaintGroup<TPresentation>? inactivePaint, UiBoxShadow? shadow)
    {
        ThrowIfDisposed();
        var firstOnly = options.Size == UiAvatarSize.Size16;
        var initials = _initials;
        if (initials is null)
        {
            if (firstOnly == (_options.Size == UiAvatarSize.Size16) && options.RightToLeft == _options.RightToLeft)
                initials = _displayInitials;
            else
            {
                var index = (firstOnly ? 2 : 0) + (options.RightToLeft ? 1 : 0);
                var cache = _mediaInitials ??= new string?[4];
                initials = cache[index] ??= UiAvatarInitials.FromName(_name, firstOnly, options.RightToLeft);
            }
        }
        var content = new UiAvatarContent(_name, initials, _icon, _image, _colorIndex, _displayPresenceText, shadow);
        context.PaintAvatar<TPresentation>(bounds, content, options, inactivePaint);
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        PreparePaintResources();
        ui.RenderAvatar<TPresentation>(PreparePaintContent(), _options, _accessibilityLabel, _inactivePaint);
    }
    internal string ResolvedAccessibilityName => _accessibilityLabel;
    internal string ResolvedPresenceText => _displayPresenceText;
    internal UiAvatarContent PreparePaintContent()
    {
        if (_options.VisibleOptions.ShowActivityShadow)
            _shadow ??= new(default);
        return new(_name, _displayInitials, _icon, _image, _colorIndex, _displayPresenceText, _shadow);
    }
    internal static void ValidateSize(UiVector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new InvalidOperationException("Avatar presentation returned invalid extents.");
    }
    private protected override void ReleaseOwnedResources()
    {
        _image?.Dispose();
        _shadow?.Dispose();
        _inactivePaint?.Dispose();
        _inactivePaint = null;
        _shadow = null;
        _image = null;
        _icon = default;
        _name = _displayInitials = _activityText = _accessibilityLabel = string.Empty;
        _initials = _idForColor = _presenceText = null;
        _mediaInitials = null;
        _displayPresenceText = string.Empty;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiAvatar<>), typeof(UiFluentAvatarPresentation))]
[UiComponent("Avatar", UiComponentExposure.Fluent)]
public partial class UiFluentAvatar;

public sealed partial class UiImmediateContext
{
    internal void RenderAvatar<TPresentation>(UiAvatarContent content, UiAvatarOptions options, string accessibilityName,
        UiAvatarPaintGroup<TPresentation>? inactivePaint = null)
        where TPresentation : IUiAvatarPresentation
    {
        EnsureNotMeasuring();
        options.Validate();
        var visibleOptions = options.VisibleOptions;
        var context = new UiControlContext(this);
        var natural = TPresentation.Measure(context, visibleOptions);
        UiAvatar<TPresentation>.ValidateSize(natural);
        var size = ConstrainToLayout(natural);
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        context.PaintAvatar<TPresentation>(bounds, content, options, inactivePaint);
        ExposeAccessibility(ResolveId(UiControlWidget.PrimaryControlId), _accessibilityParent,
            UiAccessibilityRole.Image, accessibilityName, bounds, focusable: false, valuePattern: false);
    }
}

public readonly partial struct UiControlContext
{
    internal void PaintAvatar<TPresentation>(UiRect bounds, UiAvatarContent content, UiAvatarOptions options,
        UiAvatarPaintGroup<TPresentation>? inactivePaint) where TPresentation : IUiAvatarPresentation
    {
        using var clip = Clip(bounds);
        if (options.IsInactive)
            (inactivePaint ?? throw new InvalidOperationException("Inactive Avatar requires its owned paint group."))
                .Paint(_ui, this, bounds, content, options.VisibleOptions);
        else
            TPresentation.Paint(this, bounds, content, options.VisibleOptions);
    }
}
