namespace Duxel.Core;

/// <summary>A decorative standalone icon borrowing a compiled or SVG painter. The painter owner must outlive this widget.</summary>
public class UiIcon : UiControlWidget
{
    private UiMenuIcon _icon;
    private UiMenuIcon _hoverIcon;
    private UiVector2 _size;
    private UiColor _color;

    public UiIcon(UiMenuIcon icon, UiVector2 size, UiColor color,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ValidateIcon(icon);
        ValidateSize(size);
        _icon = icon;
        _size = size;
        _color = color;
    }

    public UiMenuIcon Icon
    {
        get => _icon;
        set
        {
            ThrowIfDisposed();
            ValidateIcon(value);
            if (_icon == value) return;
            ThrowIfBuilding();
            _icon = value;
            RequestRebuild();
        }
    }

    public UiVector2 Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            ValidateSize(value);
            if (_size == value) return;
            ThrowIfBuilding();
            _size = value;
            RequestRebuild();
        }
    }

    /// <summary>Optional painter used when the pointer is over the decorative icon.</summary>
    public UiMenuIcon HoverIcon
    {
        get => _hoverIcon;
        set
        {
            ThrowIfDisposed();
            if (_hoverIcon == value) return;
            ThrowIfBuilding();
            _hoverIcon = value;
            RequestRebuild();
        }
    }

    public UiColor Color
    {
        get => _color;
        set
        {
            ThrowIfDisposed();
            if (_color == value) return;
            ThrowIfBuilding();
            _color = value;
            RequestRebuild();
        }
    }

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.HasBoxConstraints ? context.LayoutConstraints.Constrain(_size) : _size;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderIcon(_icon, _hoverIcon, _size, _color);

    private protected override void ReleaseOwnedResources()
    {
        _icon = default;
        _hoverIcon = default;
        base.ReleaseOwnedResources();
    }

    private static void ValidateIcon(UiMenuIcon icon)
    {
        if (icon.IsEmpty) throw new ArgumentException("A standalone icon requires a painter.", nameof(icon));
    }

    private static void ValidateSize(UiVector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Icon dimensions must be finite and positive.");
    }
}

[UiWidgetStyle(typeof(UiIcon))]
[UiComponent("Icon", UiComponentExposure.Fluent)]
public partial class UiFluentStandaloneIcon;

public sealed partial class UiImmediateContext
{
    internal void RenderIcon(UiMenuIcon icon, UiMenuIcon hoverIcon, UiVector2 requested, UiColor color)
    {
        EnsureNotMeasuring();
        var size = ConstrainToLayout(requested);
        var cursor = AdvanceCursor(size);
        if (size.X <= 0 || size.Y <= 0) return;
        var bounds = new UiRect(cursor.X, cursor.Y, size.X, size.Y);
        var painter = !hoverIcon.IsEmpty && IsControlEnabled
            && IsMouseHoveringRect(cursor, new(cursor.X + size.X, cursor.Y + size.Y)) ? hoverIcon : icon;
        painter.Paint(new(this), bounds, color, default);
    }
}
