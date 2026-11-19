namespace Duxel.Core;

[UiFluentIcon("dismiss", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiToastDismissIcon;
internal readonly struct UiToastClosePresentation : IUiButtonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => context.BeginTypography<UiRegularTypeface, UiFluentType300>(false);
    public static UiVector2 Measure(UiControlContext context, UiVector2 text, UiVector2 requested) => new(24, 24);
    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
    {
        if (interaction.Hovered || interaction.Held) context.Frame(bounds, context.Theme.FrameBgHovered, default, 4, 0);
        UiToastDismissIcon.Paint(context, new(bounds.X + 2, bounds.Y + 2, 20, 20), context.Theme.Text, interaction);
        if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
    }
}
public class UiToastWidget : UiControlWidget
{
    private UiToastOptions _options;
    private float _width = 292;
    private bool _isOpen = true, _reducedMotion, _animationPaused, _rightToLeft;
    private string _statusText = string.Empty;
    private ulong _revision = 1;
    private UiToastAnnouncementState _announcement;
    private readonly WeakReference<UiImmediateContext> _announcementContext = new(null!);
    internal bool IsPortalContent;
    public event Action? Dismissed;
    public UiToastOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); ThrowIfBuilding(); ArgumentNullException.ThrowIfNull(value); value.Validate();
            if (_options == value) return;
            _options = value; _revision++; Synchronize(); RequestRebuild();
        }
    }
    public float Width
    {
        get => _width;
        set
        {
            ThrowIfDisposed(); ThrowIfBuilding();
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(Width));
            if (_width == value) return;
            _width = value; RequestRebuild();
        }
    }
    public bool IsOpen => _isOpen;
    public void Show() { ThrowIfDisposed(); ThrowIfBuilding(); if (_isOpen) return; _isOpen = true; _revision++; _announcement = default; RequestRebuild(); }
    public void Hide()
    {
        ThrowIfDisposed(); ThrowIfBuilding(); if (!_isOpen) return;
        _isOpen = false; RequestRebuild(); Dismissed?.Invoke();
    }
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set { ThrowIfDisposed(); ThrowIfBuilding(); if (_reducedMotion == value) return; _reducedMotion = value; Synchronize(); RequestRebuild(); }
    }
    public bool AnimationPaused
    {
        get => _animationPaused;
        set { ThrowIfDisposed(); ThrowIfBuilding(); if (_animationPaused == value) return; _animationPaused = value; Synchronize(); RequestRebuild(); }
    }
    public bool RightToLeft
    {
        get => _rightToLeft;
        set { ThrowIfDisposed(); ThrowIfBuilding(); if (_rightToLeft == value) return; _rightToLeft = value; Synchronize(); RequestRebuild(); }
    }
    internal override bool AllowsVisualOverflow => true;
    internal readonly UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation> Spinner;
    internal readonly UiProgress<UiFluentProgressBehavior, UiFluentProgressPresentation> Progress;
    internal readonly UiInlineText Status;
    internal readonly UiInlineText Title;
    internal readonly UiInlineText Body;
    internal readonly UiButton<UiSpaceButtonBehavior, UiToastClosePresentation> Close;
    internal readonly UiFluentParagraphButton Action;
    internal readonly UiBoxShadow Shadow = new(UiFluentShadow.Resolve(UiFluentColorScheme.Light, UiShadowElevation.Shadow8));
    internal UiVector2 TitleSize, BodySize, ActionSize, ProgressSize, StatusSize, DetailsSize;
    internal readonly UiToastDetails Details;
    internal readonly UiScrollView DetailsViewport;
    public UiToastWidget(UiToastOptions options, Action? dismissed = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        Dismissed = dismissed;
        Title = new([new(Options.Title, UiInlineTextWeight.Semibold)], identity : UiWidgetIdentity.Instance);
        Body = new([new(Options.Body ?? string.Empty)], identity : UiWidgetIdentity.Instance);
        Close = new("Close", Hide);
        Action = new("Action", () =>
        {
            var callback = Options.Action ?? throw new InvalidOperationException("An absent toast action cannot be invoked.");
            callback();
        });
        Spinner = new(options : new(Size : UiSpinnerSize.Tiny));
        Progress = new(0) { Size = new(0, 2) };
        Status = new([new(string.Empty)], true, null, UiWidgetIdentity.Instance, UiStyledTextFlags.Caption);
        Details = new(this);
        DetailsViewport = new(Details) { AccessibleName = "Notification content" };
        OwnChild("details-viewport", DetailsViewport);
        OwnChild("title", Title); OwnChild("body", Body); OwnChild("close", Close); OwnChild("action", Action);
        OwnChild("spinner", Spinner); OwnChild("progress", Progress); OwnChild("status", Status);
        Synchronize();
    }
    internal void Synchronize()
    {
        if (Title.Text != Options.Title) Title.Runs = [new(Options.Title, UiInlineTextWeight.Semibold)];
        var body = Options.Body ?? string.Empty;
        if (Body.Text != body) Body.Runs = [new(body)];
        Title.RightToLeft = RightToLeft;
        Body.RightToLeft = RightToLeft;
        Status.RightToLeft = RightToLeft;
        var alignment = RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        Title.HorizontalAlignment = alignment;
        Body.HorizontalAlignment = alignment;
        Status.HorizontalAlignment = alignment;
        Close.Label = Options.CloseLabel;
        Action.Label = Options.ActionLabel ?? "Action";
        Action.RightToLeft = RightToLeft;
        Spinner.Options = new(Size : UiSpinnerSize.Tiny, ReducedMotion : ReducedMotion, RightToLeft : RightToLeft)
            { Paused = ReducedMotion || AnimationPaused };
        Spinner.AccessibleName = Options.Title;
        Progress.Value = Options.Progress?.Value ?? 0;
        Progress.ReducedMotion = ReducedMotion || AnimationPaused;
        Progress.RightToLeft = RightToLeft;
        Progress.AccessibleName = Options.Title;
        _statusText = Options.Progress is { } progress ? progress.StatusText
            ?? (progress.Value is { } value ? value.ToString("P0", System.Globalization.CultureInfo.CurrentCulture) : string.Empty) : string.Empty;
        if (Status.Text != _statusText) Status.Runs = [new(_statusText)];
    }
    internal UiVector2 MeasureLayout(UiControlContext context)
    {
        if (!IsOpen) return default;
        var options = Options;
        var width = context.LayoutConstraints.Constrain(new(Width, 0)).X;
        var layout = UiToastContentLayout.Resolve(width, RightToLeft, options.CanDismiss);
        var textWidth = layout.Width;
        DetailsSize = MeasureDetails(context, textWidth);
        var availableHeight = MathF.Max(0, context.LayoutConstraints.MaxHeight - layout.VerticalPadding);
        var height = DetailsSize.Y;
        if (height > availableHeight)
            height = context.MeasureParentDataChild(DetailsViewport, null, new(textWidth, textWidth, 0, availableHeight)).Y;
        return context.LayoutConstraints.Constrain(new(width, layout.VerticalPadding + MathF.Max(20, height)));
    }
    internal UiVector2 MeasureDetails(UiControlContext context, float width)
    {
        var options = Options;
        var constraints = new UiBoxConstraints(0, width, 0, float.PositiveInfinity);
        TitleSize = context.MeasureParentDataChild(Title, null, constraints);
        BodySize = string.IsNullOrEmpty(options.Body) ? default : context.MeasureParentDataChild(Body, null, constraints);
        ActionSize = options.Action is null ? default : context.MeasureParentDataChild(Action, null, constraints);
        ProgressSize = options.Progress?.Value is not null ? context.MeasureParentDataChild(Progress, null, new(0, width, 0, float.PositiveInfinity)) : default;
        StatusSize = _statusText.Length > 0 ? context.MeasureParentDataChild(Status, null, constraints) : default;
        return new(width, TitleSize.Y + (BodySize.Y > 0 ? 4 + BodySize.Y : 0)
            + (ProgressSize.Y > 0 ? 8 + ProgressSize.Y : 0) + (StatusSize.Y > 0 ? 4 + StatusSize.Y : 0)
            + (ActionSize.Y > 0 ? 8 + ActionSize.Y : 0));
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context);
    protected override void RenderControl(UiImmediateContext ui)
    {
        if (!IsOpen) return;
        ui.RenderToastWidget(this, PrimaryControlId);
    }
    internal void AnnounceInline(UiImmediateContext ui, string id)
    {
        if (IsPortalContent) return;
        if (!_announcementContext.TryGetTarget(out var prior) || !ReferenceEquals(prior, ui))
        {
            _announcementContext.SetTarget(ui); _announcement = default;
        }
        ui.AnnounceToast(ref _announcement, Options, id, _revision);
    }
    private protected override void ReleaseOwnedResources() { Dismissed = null; Shadow.Dispose(); _announcementContext.SetTarget(null!); base.ReleaseOwnedResources(); }
}
public sealed partial class UiImmediateContext
{
    internal void RenderToastWidget(UiToastWidget content, string id)
    {
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(content.MeasureLayout(context));
        var origin = AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var options = content.Options;
        var forced = context.Theme.ForcedColors is not null;
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var fill = forced ? context.Theme.WindowBg : dark ? new UiColor(41, 41, 41) : new UiColor(255, 255, 255);
        content.Shadow.Options = UiFluentShadow.Resolve(context.FluentColorScheme, UiShadowElevation.Shadow8);
        if (!forced) context.BoxShadow(content.Shadow, root, 4);
        context.Frame(root, fill, forced ? context.Theme.Border : default, 4, forced ? 1 : 0);
        using var clip = context.Clip(root);
        var ink = forced ? context.Theme.Text : dark ? new UiColor(255, 255, 255) : new UiColor(36, 36, 36);
        var icon = new UiRect(content.RightToLeft ? root.X + root.Width - 32 : root.X + 12, root.Y + 12, 20, 20);
        var iconInk = forced ? ink : UiFluentMessageBarPresentation.ResolvePalette(context.FluentColorScheme, options.Intent switch { UiToastIntent.Info => UiMessageBarIntent.Info, UiToastIntent.Success => UiMessageBarIntent.Success, UiToastIntent.Warning => UiMessageBarIntent.Warning, _ => UiMessageBarIntent.Error }).Icon;
        if (options.Progress is { Value: null }) RenderParentDataChild(content.Spinner, null, icon, root);
        else switch (options.Intent)
        {
            case UiToastIntent.Info: UiMessageBarInfoIcon.Paint(context, icon, iconInk, default); break;
            case UiToastIntent.Success: UiMessageBarSuccessIcon.Paint(context, icon, iconInk, default); break;
            case UiToastIntent.Warning: UiMessageBarWarningIcon.Paint(context, icon, iconInk, default); break;
            case UiToastIntent.Error: UiMessageBarErrorIcon.Paint(context, icon, iconInk, default); break;
        }
        var layout = UiToastContentLayout.Resolve(root.Width, content.RightToLeft, options.CanDismiss);
        var x = root.X + layout.Start;
        var width = layout.Width;
        var group = ResolveId(id);
        var parent = _accessibilityParent;
        if (!content.IsPortalContent)
        {
            ExposeAccessibility(group, parent, UiAccessibilityRole.Group, options.Title, root,
                focusable : false, valuePattern : false, literalLabel : true, busy : options.Progress is not null);
            _accessibilityParent = group;
        }
        PushStyleColor(UiStyleColor.Text, ink);
        try
        {
            var detailsBounds = new UiRect(x, root.Y + layout.Top, width, MathF.Max(0, root.Height - layout.VerticalPadding));
            RenderParentDataChild(content.DetailsSize.Y > detailsBounds.Height ? content.DetailsViewport : content.Details,
                null, detailsBounds, root);
            if (options.CanDismiss)
            {
                content.Close.Label = options.CloseLabel;
                RenderParentDataChild(content.Close, null, new(content.RightToLeft ? root.X + 12 : root.X + root.Width - 36, root.Y + 10, 24, 24), root);
            }
        }
        finally { PopStyleColor(); _accessibilityParent = parent; }
        content.AnnounceInline(this, group);
        FinishLayoutBox(origin, new(root.Width, root.Height));
    }

    internal void RenderToastDetails(UiToastWidget content)
    {
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(content.MeasureDetails(context, context.LayoutConstraints.MaxWidth));
        var origin = AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var options = content.Options;
        var x = root.X; var y = root.Y; var width = root.Width;
        var title = new UiRect(x, y, width, content.TitleSize.Y);
        RenderParentDataChild(content.Title, null, title, root); y += content.TitleSize.Y;
        if (content.BodySize.Y > 0)
        {
            y += 4;
            RenderParentDataChild(content.Body, null, new(x, y, width, content.BodySize.Y), root); y += content.BodySize.Y;
        }
        if (content.ProgressSize.Y > 0)
        {
            y += 8;
            RenderParentDataChild(content.Progress, null, new(x, y, width, content.ProgressSize.Y), root); y += content.ProgressSize.Y;
        }
        if (content.StatusSize.Y > 0)
        {
            y += 4;
            RenderParentDataChild(content.Status, null, new(x, y, width, content.StatusSize.Y), root); y += content.StatusSize.Y;
        }
        if (options.ActionLabel is { } label)
        {
            content.Action.Label = label;
            RenderParentDataChild(content.Action, null, new(content.RightToLeft ? x + width - MathF.Min(width, content.ActionSize.X) : x,
                y + 8, MathF.Min(width, content.ActionSize.X), content.ActionSize.Y), root);
        }
        FinishLayoutBox(origin, size);
    }
}

internal sealed class UiToastDetails(UiToastWidget owner) : UiControlWidget
{
    protected override UiVector2 MeasureContent(UiControlContext context) => owner.MeasureDetails(context, context.LayoutConstraints.MaxWidth);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderToastDetails(owner);
}
