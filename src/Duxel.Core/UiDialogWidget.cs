namespace Duxel.Core;

public enum UiDialogType
{
    Modal, NonModal, Alert
}

public readonly record struct UiDialogOptions(UiDialogType Type = UiDialogType.Modal,
    float Width = 600, float? MaximumHeight = null, bool LightDismiss = true,
    bool? ShowCloseButton = null, string CloseLabel = "Close", string? Description = null, bool MotionEnabled = true)
{
    public UiDialogOptions() : this(UiDialogType.Modal, 600, null, true, null, "Close", null) { }
    /// <summary>Keeps Escape from dismissing this dialog. Explicit close actions remain available.</summary>
    public bool SuppressEscapeDismissal { get; init; }
    internal bool HasCloseButton => ShowCloseButton ?? Type == UiDialogType.NonModal;
    internal void Validate()
    {
        if ((uint)Type > 2 || !float.IsFinite(Width) || Width <= 0
            || MaximumHeight is { } height && (!float.IsFinite(height) || height <= 0))
            throw new ArgumentOutOfRangeException(nameof(UiDialogOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(CloseLabel);
        if (UiAccessibilityTree.ContainsControl(CloseLabel, false)
            || Description is { } description && UiAccessibilityTree.ContainsControl(description, true))
            throw new ArgumentException("Dialog accessibility text contains unsupported control characters.");
    }
}

public readonly record struct UiDialogMetrics(float Padding, float Gap, float CornerRadius, float BorderWidth, float MinimumContentHeight);

public interface IUiDialogPresentation : IUiSurfacePresentation;

internal sealed class UiDialogInputState : UiOverlayInputState
{
    internal bool BackdropPressed, MotionEnabled;
    internal UiBoxShadow? Shadow;
    internal UiPaintGroup? SurfacePaint;
    internal UiRect NaturalBounds;
    internal UiColor BackdropColor;
    internal UiOverlayMotionSample EntryFrom = new(.85f, 0, 0), LastMotion = new(1, 1, 1);
    internal override void PrepareOpening() => Context?.PrepareDialogOpening(this);
    internal override void PrepareClosing() => Context?.PrepareDialogClosing(this);
    internal override double? InteractionDeadline => null;
    internal override void CancelPendingDismissal()
    {
    }
    internal override bool ContainsScreenPoint(UiVector2 point) => point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width
        && point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height;
    internal override void ResetInteraction(bool releaseResources)
    {
        BackdropPressed = false;
        if (releaseResources)
        {
            Shadow?.Dispose();
            Shadow = null;
            SurfacePaint?.Dispose();
            SurfacePaint = null;
        }
    }
}

/// <summary>Owns a screen-space dialog portal with retained title, scrolling content and actions.</summary>
public class UiDialog<TPresentation> : UiControlWidget where TPresentation : IUiDialogPresentation
{
    private string _title;
    private UiDialogOptions _options;
    private readonly UiSurfaceVisibility _visibility;
    internal readonly UiDialogInputState Input = new();
    internal readonly UiSurfaceBody<TPresentation> Body;
    internal readonly UiScrollView Viewport;

    public UiDialog(string title, UiWidget content, UiWidget[]? actions = null, UiDialogOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ValidateTitle(title);
        ArgumentNullException.ThrowIfNull(content);
        _options = options ?? new();
        _options.Validate();
        actions ??= [];
        var unique = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance) { content };
        ValidateOwnedChild("content", content);
        for (var i = 0; i < actions.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(actions[i]);
            if (!unique.Add(actions[i]))
                throw new ArgumentException("Dialog slots cannot share a widget instance.", nameof(actions));
            ValidateOwnedChild(i.ToString(System.Globalization.CultureInfo.InvariantCulture), actions[i]);
        }
        _title = title;
        _visibility = new(Input, RequestRebuild);
        Body = new(title, content, actions, () => Close());
        Viewport = Dux.ScrollView(Body);
        OwnChild("surface", Viewport);
    }

    public string Title
    {
        get => _title;
        set
        {
            ThrowIfDisposed();
            ValidateTitle(value);
            if (_title == value)
                return;
            ThrowIfBuilding();
            _title = value;
            Body.Title.Text = value;
            RequestRebuild();
        }
    }
    public UiWidget Content => Body.Content.Child;
    public ReadOnlySpan<UiWidget> Actions => Body.Actions.Children;
    public bool IsOpen => Input.Open;
    public UiDialogOptions Options
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
    public UiBinding<bool>? Open
    {
        get => _visibility.Binding;
        set
        {
            ThrowIfDisposed();
            ThrowIfBuilding();
            _visibility.SetBinding(value);
        }
    }
    public Action<bool>? OpenChanged
    {
        get => _visibility.Changed;
        set
        {
            ThrowIfDisposed();
            _visibility.Changed = value;
        }
    }
    public void Show()
    {
        ThrowIfDisposed();
        _visibility.Show(RenderingContext?.ControlActivationOrigin);
    }
    public bool Close()
    {
        ThrowIfDisposed();
        return _visibility.Close();
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => default;
    protected override void RenderControl(UiImmediateContext ui)
    {
        Input.Attach(ui);
        var (pending, origin) = _visibility.TakePending();
        ui.RenderDialog(this, PrimaryInputId(Title), Open?.Value, pending, origin);
    }
    private static void ValidateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (UiAccessibilityTree.ContainsControl(title, false))
            throw new ArgumentException("Dialog title must be single-line text.", nameof(title));
    }
    private protected override void ReleaseOwnedResources()
    {
        _visibility.Dispose();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiDialog<>), typeof(UiFluentDialogPresentation))]
[UiComponent("Dialog", UiComponentExposure.Fluent)]
public partial class UiFluentDialog;
