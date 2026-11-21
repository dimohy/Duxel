namespace Duxel.Core;

public enum UiDrawerType { Overlay, Inline }
public enum UiDrawerPosition { Start, End, Bottom }
public enum UiDrawerSize { Small, Medium, Large, Full }
/// <summary>The title and optional owned action content in a Drawer header.</summary>
public readonly record struct UiDrawerHeader(string Title, UiWidget? Actions = null);

/// <summary>The owned content and initial scrolling of a Drawer body.</summary>
public readonly record struct UiDrawerBody(UiWidget Content, bool Scroll = true);

/// <summary>Owned actions laid out in the optional footer of a Drawer.</summary>
public readonly record struct UiDrawerFooter(UiWidget[] Actions);

public readonly record struct UiDrawerOptions(UiDrawerType Type = UiDrawerType.Overlay,
    UiDrawerPosition Position = UiDrawerPosition.Start, UiDrawerSize Size = UiDrawerSize.Small,
    float? Extent = null, float? InlineCrossExtent = null, bool Modal = true, bool LightDismiss = true,
    bool ShowCloseButton = true, string CloseLabel = "Close", string? Description = null,
    bool RightToLeft = false, bool StickyHeaderFooter = true, bool MotionEnabled = true, bool Separator = false, bool ScrollBody = true)
{
    public UiDrawerOptions() : this(UiDrawerType.Overlay, UiDrawerPosition.Start, UiDrawerSize.Small,
        null, null, true, true, true, "Close", null, false, true, true, false, true) { }
    internal void Validate()
    {
        if ((uint)Type > 1 || (uint)Position > 2 || (uint)Size > 3
            || Extent is { } extent && (!float.IsFinite(extent) || extent <= 0)
            || InlineCrossExtent is { } cross && (!float.IsFinite(cross) || cross <= 0))
            throw new ArgumentOutOfRangeException(nameof(UiDrawerOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(CloseLabel);
        if (UiAccessibilityTree.ContainsControl(CloseLabel, false)
            || Description is { } text && UiAccessibilityTree.ContainsControl(text, true))
            throw new ArgumentException("Drawer accessibility text contains unsupported control characters.");
    }
    internal float ResolveExtent(float available) => MathF.Min(available, Extent ?? Size switch
    {
        UiDrawerSize.Small => 320,
        UiDrawerSize.Medium => 592,
        UiDrawerSize.Large => 940,
        _ => available
    });
}
public interface IUiDrawerPresentation : IUiSurfacePresentation
{
    static virtual UiColor? ScrollSeparator(UiControlContext context) => null;
}

internal sealed class UiDrawerInputState : UiOverlayInputState
{
    internal bool BackdropPressed, MotionEnabled, Inline, AutoFocusPending;
    internal UiBoxShadow? Shadow;
    internal UiPaintGroup? SurfacePaint;
    internal UiRect NaturalBounds;
    internal UiDrawerInlineMotion InlineMotion;
    internal UiColor BackdropColor;
    internal UiVector2 ClosedOffset;
    internal UiOverlayMotionSample EntryFrom = new(1, 1, 0), LastMotion = new(1, 1, 1);
    internal override void PrepareOpening() => Context?.PrepareDrawerOpening(this);
    internal override void PrepareClosing() => Context?.PrepareDrawerClosing(this);
    internal override double? InteractionDeadline => null;
    internal override bool ContainsScreenPoint(UiVector2 point) => point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width
        && point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height;
    internal override void CancelPendingDismissal() { }
    internal override void ResetInteraction(bool releaseResources)
    {
        BackdropPressed = false;
        if (!releaseResources) return;
        InlineMotion.Reset();
        Shadow?.Dispose();
        Shadow = null;
        SurfacePaint?.Dispose();
        SurfacePaint = null;
    }
}

/// <summary>Owns an edge surface with shared focus/visibility, a scrolling body and retained header/footer slots.</summary>
public class UiDrawer<TPresentation> : UiControlWidget where TPresentation : IUiDrawerPresentation
{
    private string _title;
    private UiDrawerOptions _options;
    private readonly UiDrawerType? _fixedType;
    private readonly UiSurfaceVisibility _visibility;
    private (object Resources, int Frame, UiDrawerOptions Options, UiVector2 Natural)? _inlineMeasurement;
    internal readonly UiDrawerInputState Input = new();
    internal readonly UiSurfaceBody<TPresentation> Body;
    internal readonly UiScrollView Viewport;
    public UiDrawer(string title, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(title, content, actions, options, identity, null)
    {
    }

    private protected UiDrawer(string title, UiWidget content, UiWidget[]? actions, UiDrawerOptions? options,
        UiWidgetIdentity identity, UiDrawerType? fixedType) : this(new UiDrawerHeader(title), content, actions, options, identity, fixedType)
    {
    }

    public UiDrawer(UiDrawerHeader header, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(header, content, actions, options, identity, null)
    {
    }

    public UiDrawer(UiDrawerHeader header, UiDrawerBody body, UiDrawerFooter? footer = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(header, body, footer, options, identity, null)
    {
    }

    private protected UiDrawer(UiDrawerHeader header, UiDrawerBody body, UiDrawerFooter? footer, UiDrawerOptions? options,
        UiWidgetIdentity identity, UiDrawerType? fixedType)
        : this(header, body.Content, footer?.Actions, (options ?? new UiDrawerOptions()) with { ScrollBody = body.Scroll }, identity, fixedType)
    {
    }

    private protected UiDrawer(UiDrawerHeader header, UiWidget content, UiWidget[]? actions, UiDrawerOptions? options,
        UiWidgetIdentity identity, UiDrawerType? fixedType) : base(identity)
    {
        ValidateTitle(header.Title);
        ArgumentNullException.ThrowIfNull(content);
        _options = options ?? new();
        _options.Validate();
        _fixedType = fixedType;
        if (fixedType is { } type)
            _options = _options with { Type = type };
        actions = actions?.ToArray() ?? [];
        var unique = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance) { content };
        ValidateOwnedChild("content", content);
        if (header.Actions is { } headerActions)
        {
            if (!unique.Add(headerActions)) throw new ArgumentException("Drawer slots cannot share a widget instance.", nameof(header));
            ValidateOwnedChild("header-actions", headerActions);
        }
        for (var i = 0; i < actions.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(actions[i]);
            if (!unique.Add(actions[i])) throw new ArgumentException("Drawer slots cannot share a widget instance.", nameof(actions));
            ValidateOwnedChild(i.ToString(System.Globalization.CultureInfo.InvariantCulture), actions[i]);
        }
        _title = header.Title;
        _visibility = new(Input, RequestRebuild);
        Body = new(header.Title, content, actions, () => Close(), header.Actions);
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
            if (_title == value) return;
            ThrowIfBuilding();
            _title = value;
            Body.Title.Text = value;
            RequestRebuild();
        }
    }
    public UiDrawerOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_fixedType is { } fixedType && value.Type != fixedType)
                throw new ArgumentException("This drawer has a fixed hosting type.", nameof(value));
            if (_options == value) return;
            if (Input.Open && _options.Type != value.Type) throw new InvalidOperationException("Close the drawer before changing its hosting type.");
            ThrowIfBuilding();
            if (_options.Type != value.Type)
            {
                Input.InlineMotion.Reset();
                Input.SurfacePaint?.Dispose();
                Input.SurfacePaint = null;
                _inlineMeasurement = null;
            }
            _options = value;
            RequestRebuild();
        }
    }
    public UiWidget Content => Body.Content.Child;
    public UiWidget? HeaderActions => Body.HeaderActions;
    public ReadOnlySpan<UiWidget> Actions => Body.Actions.Children;
    public bool IsOpen => Input.Open;
    public UiBinding<bool>? Open
    {
        get => _visibility.Binding;
        set { ThrowIfDisposed(); ThrowIfBuilding(); _visibility.SetBinding(value); }
    }
    public Action<bool>? OpenChanged
    {
        get => _visibility.Changed;
        set { ThrowIfDisposed(); _visibility.Changed = value; }
    }
    public void Show() { ThrowIfDisposed(); _visibility.Show(RenderingContext?.ControlActivationOrigin); }
    public bool Close() { ThrowIfDisposed(); return _visibility.Close(); }
    protected override bool CanRetainMeasurement => false;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        if (Options.Type == UiDrawerType.Overlay) return default;
        var progress = Input.InlineMotion.Sample(_visibility.DesiredOpen, Options.MotionEnabled, context.TimeSeconds);
        if (progress <= 0 && !_visibility.DesiredOpen) return default;
        var natural = InlineSize(context);
        _inlineMeasurement = (context.LayoutMeasurementKey.Resources, context.LayoutFrame, Options, natural);
        return UiDrawerInlineMotion.Allocation(natural, progress, Options.Position == UiDrawerPosition.Bottom);
    }
    internal UiVector2 InlineNaturalSize(UiControlContext context)
        => _inlineMeasurement is { } measured && ReferenceEquals(measured.Resources, context.LayoutMeasurementKey.Resources)
            && measured.Frame == context.LayoutFrame && measured.Options == Options ? measured.Natural : InlineSize(context);
    internal UiVector2 InlineSize(UiControlContext context)
    {
        var constraints = context.LayoutConstraints.Validate();
        var bottom = Options.Position == UiDrawerPosition.Bottom;
        var available = bottom ? constraints.MaxHeight : constraints.MaxWidth;
        var cross = Options.InlineCrossExtent ?? (bottom ? constraints.MaxWidth : constraints.MaxHeight);
        var extent = Options.ResolveExtent(available);
        if (!float.IsFinite(extent) || !float.IsFinite(cross))
            throw new InvalidOperationException("An inline drawer requires finite axis constraints or an explicit cross extent.");
        return constraints.Constrain(bottom ? new(cross, extent) : new(extent, cross));
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        Input.Attach(ui);
        var (pending, origin) = _visibility.TakePending();
        ui.RenderDrawer(this, PrimaryInputId(Title), Open?.Value, pending, origin);
        if (pending == true && !Input.Open) RequestRebuild();
    }
    private static void ValidateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (UiAccessibilityTree.ContainsControl(title, false)) throw new ArgumentException("Drawer title must be single-line text.", nameof(title));
    }
    private protected override void ReleaseOwnedResources()
    {
        _visibility.Dispose();
        base.ReleaseOwnedResources();
    }
}
[UiWidgetStyle(typeof(UiDrawer<>), typeof(UiFluentDrawerPresentation))]
[UiComponent("Drawer", UiComponentExposure.Fluent)]
public partial class UiFluentDrawer;

/// <summary>A drawer that participates in its parent's layout and keeps the surrounding content interactive.</summary>
public class UiInlineDrawer<TPresentation> : UiDrawer<TPresentation> where TPresentation : IUiDrawerPresentation
{
    public UiInlineDrawer(string title, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(title, content, actions, options, identity, UiDrawerType.Inline)
    {
    }

    public UiInlineDrawer(UiDrawerHeader header, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(header, content, actions, options, identity, UiDrawerType.Inline)
    {
    }

    public UiInlineDrawer(UiDrawerHeader header, UiDrawerBody body, UiDrawerFooter? footer = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(header, body, footer, options, identity, UiDrawerType.Inline)
    {
    }
}

/// <summary>A drawer that opens above the page and defaults to modal interaction.</summary>
public class UiOverlayDrawer<TPresentation> : UiDrawer<TPresentation> where TPresentation : IUiDrawerPresentation
{
    public UiOverlayDrawer(string title, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(title, content, actions, options, identity, UiDrawerType.Overlay)
    {
    }

    public UiOverlayDrawer(UiDrawerHeader header, UiWidget content, UiWidget[]? actions = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(header, content, actions, options, identity, UiDrawerType.Overlay)
    {
    }

    public UiOverlayDrawer(UiDrawerHeader header, UiDrawerBody body, UiDrawerFooter? footer = null, UiDrawerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(header, body, footer, options, identity, UiDrawerType.Overlay)
    {
    }
}

[UiWidgetStyle(typeof(UiInlineDrawer<>), typeof(UiFluentDrawerPresentation))]
[UiComponent("InlineDrawer", UiComponentExposure.Fluent)]
public partial class UiFluentInlineDrawer;

[UiWidgetStyle(typeof(UiOverlayDrawer<>), typeof(UiFluentDrawerPresentation))]
[UiComponent("OverlayDrawer", UiComponentExposure.Fluent)]
public partial class UiFluentOverlayDrawer;
