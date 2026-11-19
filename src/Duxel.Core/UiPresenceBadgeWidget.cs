namespace Duxel.Core;

public enum UiPresenceStatus { Available, Away, Busy, DoNotDisturb, Offline, OutOfOffice, Unknown, Blocked }

public readonly record struct UiPresenceBadgeOptions(UiPresenceStatus Status = UiPresenceStatus.Available,
    UiBadgeSize Size = UiBadgeSize.Medium, bool OutOfOffice = false)
{
    public void Validate()
    {
        if ((uint)Status > 7 || (uint)Size > 5)
            throw new ArgumentOutOfRangeException(nameof(UiPresenceBadgeOptions));
    }
    public string StatusText
    {
        get
        {
            Validate();
            var text = Status switch
            {
                UiPresenceStatus.Available => "available", UiPresenceStatus.Away => "away",
                UiPresenceStatus.Busy => "busy", UiPresenceStatus.DoNotDisturb => "do not disturb",
                UiPresenceStatus.Offline => "offline", UiPresenceStatus.OutOfOffice => "out of office",
                UiPresenceStatus.Unknown => "unknown", UiPresenceStatus.Blocked => "blocked",
                _ => throw new ArgumentOutOfRangeException(nameof(Status))
            };
            return OutOfOffice && Status != UiPresenceStatus.OutOfOffice ? text + " out of office" : text;
        }
    }
}

/// <summary>The icon is borrowed. A presentation must not dispose its owner.</summary>
public readonly record struct UiPresenceBadgeContent(string Label, UiMenuIcon Icon);
public interface IUiPresenceBadgePresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiPresenceBadgeOptions options);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiPresenceBadgeContent content, UiPresenceBadgeOptions options);
}

/// <summary>One passive image representing a person's status, with no hover, activation or timer.</summary>
public class UiPresenceBadge<TPresentation> : UiControlWidget where TPresentation : IUiPresenceBadgePresentation
{
    private UiPresenceBadgeOptions _options;
    private string _statusText;
    private UiMenuIcon _icon;
    public UiPresenceBadge(UiPresenceBadgeOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        options.Validate();
        _options = options;
        _statusText = options.StatusText;
    }
    public UiPresenceBadgeOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            _statusText = value.StatusText;
            RequestRebuild();
        }
    }
    public string StatusText => _statusText;
    public UiMenuIcon Icon
    {
        get => _icon;
        set
        {
            ThrowIfDisposed();
            if (_icon == value) return;
            ThrowIfBuilding();
            _icon = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var size = TPresentation.Measure(context, _options);
        ValidateSize(size);
        return context.HasBoxConstraints ? context.LayoutConstraints.Constrain(size) : size;
    }
    internal static void ValidateSize(UiVector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new InvalidOperationException("PresenceBadge presentation returned invalid extents.");
    }
    protected override void RenderControl(UiImmediateContext ui) =>
        ui.RenderPresenceBadge<TPresentation>(new(_statusText, _icon), _options);
    private protected override void ReleaseOwnedResources()
    {
        _icon = default;
        _statusText = string.Empty;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiPresenceBadge<>), typeof(UiFluentPresenceBadgePresentation))]
[UiComponent("PresenceBadge", UiComponentExposure.Fluent)]
public partial class UiFluentPresenceBadge;

public sealed partial class UiImmediateContext
{
    internal void RenderPresenceBadge<TPresentation>(UiPresenceBadgeContent content, UiPresenceBadgeOptions options)
        where TPresentation : IUiPresenceBadgePresentation
    {
        EnsureNotMeasuring();
        options.Validate();
        var context = new UiControlContext(this);
        var natural = TPresentation.Measure(context, options);
        UiPresenceBadge<TPresentation>.ValidateSize(natural);
        var size = ConstrainToLayout(natural);
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        using (context.Clip(bounds)) TPresentation.Paint(context, bounds, content, options);
        ExposeAccessibility(ResolveId(UiControlWidget.PrimaryControlId), _accessibilityParent,
            UiAccessibilityRole.Image, content.Label, bounds, focusable : false, valuePattern : false);
    }
}
