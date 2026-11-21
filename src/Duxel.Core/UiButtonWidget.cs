namespace Duxel.Core;

/// <summary>Input result independent of the button's layout and drawing implementation.</summary>
public readonly record struct UiButtonInteraction(bool Activated, bool Hovered, bool Held)
{
    public bool Focused
    {
        get; init;
    }
    public bool FocusVisible
    {
        get; init;
    }
    /// <summary>Whether a disclosure trigger currently owns an open surface.</summary>
    public bool Expanded
    {
        get; init;
    }
}

/// <summary>A statically selected input policy. Implementations store state in the UI context, not in static fields.</summary>
public interface IUiButtonBehavior
{
    static virtual bool AccessibilityFocus => false;
    static abstract UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds);
}

/// <summary>A statically selected layout and paint implementation; arbitrary drawing is supported.</summary>
public interface IUiButtonPresentation
{
    /// <summary>Promises pure measurement from configured typography, requested size and text. Custom presentations are non-retained by default.</summary>
    static virtual bool CanRetainMeasurement => false;
    static virtual UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => default;
    static abstract UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize);
    static abstract void Paint(UiControlContext context, string text, UiVector2 textSize,
        UiRect bounds, UiButtonInteraction interaction);
}

/// <summary>Access to the current layer, clip and text pipeline without a second renderer.</summary>
public readonly partial struct UiControlContext
{
    private readonly UiImmediateContext _ui;
    internal string? MotionKey { get; init; }
    internal bool SeparateTabSelectionIndicator { get; init; }
    public bool ButtonHasText
    {
        get;
    }
    internal UiControlContext(UiImmediateContext ui, bool buttonHasText = false)
    {
        _ui = ui;
        ButtonHasText = buttonHasText;
    }
    public UiTheme Theme => _ui.ButtonTheme;
    public UiFluentColorScheme FluentColorScheme => _ui.ControlFluentColorScheme;
    public UiControlTypographyScope BeginTypography<TTypeface, TMetrics>(bool hasText = true)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics => _ui.BeginControlTypography<TTypeface, TMetrics>(hasText);
    /// <summary>Scales font and line metrics together while preserving the surrounding typography on disposal.</summary>
    public UiControlTypographyScope BeginTypography<TTypeface, TMetrics>(float scale, bool hasText = true)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics => _ui.BeginControlTypography<TTypeface, TMetrics>(hasText, scale);
    public void TypographyButtonText(string text, UiRect bounds, UiVector2 textSize, UiColor color) => _ui.PaintTypographyButtonText(text, bounds, textSize, color);
    public void FluentDivider(string text, UiDividerOptions options = default) => _ui.RenderFluentDivider(text, options);
    public void FluentBadge<TIcon>(UiBadgeContent content, UiFluentBadgeOptions options = default) where TIcon : IUiBadgeIcon =>
        _ui.RenderFluentBadge<TIcon>(content, options);
    public float AvailableWidth => _ui.GetContentRegionAvail().X;
    public UiRect ClipRect => _ui.ControlClipRect;
    public UiControlClipScope Clip(UiRect bounds) => _ui.BeginControlClip(bounds);
    public void FontIcon<TFont>(string glyph, UiRect bounds, UiColor color) where TFont : IUiIconFontFace => _ui.PaintFontIcon<TFont>(glyph, bounds, color);
    public double TimeSeconds => _ui.GetTime();
    public void RequestAnimation() => _ui.RequestControlAnimation();
    /// <summary>Retains the animation dependency, waking only when these local paint bounds are visible.</summary>
    public void RequestAnimation(UiRect bounds) => _ui.RequestControlAnimation(bounds);
    public void ProgressGradient(UiRect bar, UiRect bounds, UiColor edge, UiColor center, float radius = 0) =>
        _ui.PaintProgressGradient(bar, bounds, edge, center, radius);
    public UiDesignTokens Tokens => _ui.GetDesignTokens();
    public UiButtonSkin LegacySkin => _ui.ButtonLegacySkin;
    public UiVector2 DefaultPadding => _ui.ButtonDefaultPadding;
    public bool IsEnabled => _ui.IsControlEnabled;
    public UiButtonInteraction ActivateOnRelease(string id, UiRect bounds) => _ui.ProcessButton(id, bounds, UiButtonActivation.Release);
    public UiButtonInteraction ActivateOnPress(string id, UiRect bounds) => _ui.ProcessButton(id, bounds, UiButtonActivation.Press);
    public UiButtonInteraction ActivateSpaceOnRelease(string id, UiRect bounds) =>
        _ui.ProcessButton<UiImmediateContext.SpaceControlKeys>(id, bounds, UiButtonActivation.Release);
    public UiButtonInteraction ActivateEnterOnRelease(string id, UiRect bounds) =>
        _ui.ProcessButton<UiImmediateContext.LinkControlKeys>(id, bounds, UiButtonActivation.Release);
    public UiSliderInteraction Slide(string id, UiRect bounds, UiSliderLayout layout, UiSliderOptions options, float value) =>
        _ui.ProcessSlider(id, bounds, layout, options, value);
    public void Frame(UiRect bounds, UiColor fill, UiColor border, float radius, float borderWidth) =>
        _ui.PaintButtonFrame(bounds, fill, border, radius, borderWidth);
    public void Circle(UiVector2 center, float radius, UiColor fill) => _ui.PaintButtonCircle(center, radius, fill);
    public void FocusOutline(UiRect bounds, float radius) =>
        Frame(new(bounds.X + 2, bounds.Y + 2, MathF.Max(0, bounds.Width - 4), MathF.Max(0, bounds.Height - 4)),
            new UiColor(0, 0, 0, 0), Theme.Text, MathF.Max(0, radius - 2), 2);
    public void Text(string text, UiVector2 position, UiColor color) => _ui.PaintButtonText(text, position, color);
    public UiTextLayout LayoutText(string text) => _ui.LayoutControlText(text);
    public UiVector2 MeasureText(string text) => _ui.MeasureControlText(text);
    public UiVector2 MeasureTypography<TTypeface, TMetrics>(string text, bool wrap = false)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics => _ui.MeasureControlTypography<TTypeface, TMetrics>(text, wrap);
    public UiTextLayout PlaceText(UiVector2 size) => _ui.PlaceControlText(size);
    public string WrapText(string text) => _ui.WrapControlText(text);
    public string WrapText(string text, float width) => _ui.WrapControlText(text, width);
    public float TypographyLineHeight => _ui.ControlTypographyLineHeight;
    public float TypographyBaseline => _ui.ControlTypographyBaseline;
    public UiControlTextBlock MeasureTextBlock(string text) => _ui.MeasureControlTextBlock(text);
    public void TypographyTextBlock(in UiControlTextBlock block, UiRect bounds, UiColor color, UiItemHorizontalAlign align = UiItemHorizontalAlign.Left) =>
        _ui.PaintControlTextBlock(block, bounds, color, align);
    public UiRadioGroupInteraction RadioGroupInput(string id, UiRect bounds, ReadOnlySpan<UiRadioOption> items,
        ReadOnlySpan<UiRadioOptionLayout> layouts, string? selected, UiRadioGroupInputState state, UiButtonActivation activation = UiButtonActivation.Release) =>
        _ui.ProcessRadioGroup(id, bounds, items, layouts, selected, state, activation);
    internal UiRadioGroupInteraction FluentRadioGroupInput(string id, UiRect bounds, ReadOnlySpan<UiRadioOption> items,
        ReadOnlySpan<UiRadioOptionLayout> layouts, string? selected, UiRadioGroupInputState state) =>
        _ui.ProcessRadioGroup(id, bounds, items, layouts, selected, state, UiButtonActivation.Release, rightToLeft : RightToLeft);
    public void PaintText(string text, UiTextLayout layout, UiColor color) => _ui.PaintControlText(text, layout, color);
    public UiInputEditResult EditSingleLine(string resolvedId, ref string value, int maxLength, in UiInputLayout layout, bool selectAllOnFocus = false) =>
        _ui.EditSingleLineInput(resolvedId, ref value, maxLength, layout, selectAllOnFocus);
    public UiInputEditResult EditNavigableSingleLine(string resolvedId, ref string value, int maxLength, in UiInputLayout layout) =>
        _ui.EditNavigableInput(resolvedId, ref value, maxLength, layout);
    public void RestartInputCaretBlink() => _ui.RestartInputCaretBlink();
    public UiMultilineEditResult EditMultiline(string resolvedId, ref string value, int maxLength,
        in UiMultilineInputLayout layout) => _ui.ProcessMultilineInput(resolvedId, ref value, maxLength, layout);
    public UiMultilineEditResult EditNavigableMultiline(string resolvedId, ref string value, int maxLength,
        in UiMultilineInputLayout layout) => _ui.EditNavigableMultiline(resolvedId, ref value, maxLength, layout);
    public void PaintEditableMultiline(in UiMultilineInputLayout layout, in UiMultilineEditResult edit,
        in UiInputTextPaint paint) => _ui.PaintMultilineInput(layout, edit, paint);
    public void PaintEditableMultiline(in UiMultilineInputLayout layout, in UiMultilineEditResult edit,
        in UiInputTextPaint paint, UiColor? placeholderColor) => _ui.PaintMultilineInput(layout, edit, paint, placeholderColor);
    public UiVector2 MeasureMultiline<TPresentation>(float width = 0, float height = 0)
        where TPresentation : IUiMultilineInputPresentation => _ui.MeasureMultiline<TPresentation>(width, height);
    public UiVector2 MeasureMultiline<TPresentation>(float width, float height, UiMultilineInputOptions options)
        where TPresentation : IUiMultilineInputPresentation => _ui.MeasureMultiline<TPresentation>(width, height, options);
    public void PaintEditableText(in UiInputLayout layout, in UiInputEditResult edit, in UiInputTextPaint paint) => _ui.PaintEditableInputText(layout, edit, paint);
    internal bool LegacyInputSelectAllOnFocus => _ui.LegacyInputSelectAllOnFocus;
    internal UiInputLayout MeasureContextInput(string label, float width) => _ui.MeasureContextInput(label, width);
    internal UiInputLayout ArrangeContextInput(in UiInputLayout local, UiVector2 allocatedSize) => _ui.ArrangeContextInput(local, allocatedSize);
    internal UiInputLayout PlaceContextInput(in UiInputLayout local, UiVector2 origin) => _ui.PlaceContextInput(local, origin);
    internal void PaintContextInputLabel(string label, in UiInputLayout layout) => _ui.PaintContextInputLabel(label, layout);
    internal void PaintContextInput(in UiInputLayout layout, in UiInputEditResult edit) => _ui.PaintContextInput(layout, edit);
    public void Typography<TTypeface, TMetrics>(string text, UiColor? color = null)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics =>
        _ui.RenderTypography<TTypeface, TMetrics>(text, color);
    public void TypographyWrapped<TTypeface, TMetrics>(string text, UiColor? color = null)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics =>
        _ui.RenderTypography<TTypeface, TMetrics>(text, color, wrap: true);
    public void Line(UiVector2 start, UiVector2 end, UiColor color, float width) => _ui.PaintControlLine(start, end, color, width);
    internal UiToggleSkin LegacyToggleSkin => _ui.ControlToggleSkin;
    internal UiToggleLayout MeasureLegacyCheckbox(UiVector2 textSize, bool hasLabel) => _ui.MeasureLegacyCheckbox(textSize, hasLabel);
    internal void PaintLegacyCheckbox(string text, UiVector2 textSize, UiRect bounds, UiToggleLayout layout, UiButtonInteraction interaction, bool? value) =>
        _ui.PaintLegacyCheckbox(text, textSize, bounds, layout, interaction, value == true);
    internal UiToggleLayout MeasureLegacySwitch(UiVector2 textSize, bool hasLabel) => _ui.MeasureLegacySwitch(textSize, hasLabel);
    internal void PaintLegacySwitch(string text, UiVector2 textSize, UiRect bounds, UiToggleLayout layout, UiButtonInteraction interaction, bool? value) =>
        _ui.PaintLegacySwitch(text, textSize, bounds, layout, interaction, value == true, SampleStateMotion(value == true));
}

/// <summary>Generates an inheritable widget and constructor forwarding for a closed implementation type.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UiWidgetStyleAttribute(Type widget, params Type[] typeArguments) : Attribute
{
    public Type Widget { get; } = widget;
    public Type[] TypeArguments { get; } = typeArguments;
}

public readonly struct UiReleaseButtonBehavior : IUiButtonBehavior
{
    public static bool AccessibilityFocus => true;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) => context.ActivateOnRelease(id, bounds);
}

public readonly struct UiPressButtonBehavior : IUiButtonBehavior
{
    public static bool AccessibilityFocus => true;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) => context.ActivateOnPress(id, bounds);
}

public readonly struct UiSpaceButtonBehavior : IUiButtonBehavior
{
    public static bool AccessibilityFocus => true;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) =>
        context.ActivateSpaceOnRelease(id, bounds);
}

/// <summary>Compatibility policy for existing numeric skin scopes.</summary>
public readonly struct UiConfiguredButtonBehavior : IUiButtonBehavior
{
    public static bool AccessibilityFocus => true;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) => context.LegacySkin.Activation switch
    {
        UiButtonActivation.Release => context.ActivateOnRelease(id, bounds),
        UiButtonActivation.Press => context.ActivateOnPress(id, bounds),
        _ => throw new InvalidOperationException("Unknown button activation policy.")
    };
}

/// <summary>Existing button rendering, shared by immediate and inheritable widgets.</summary>
public readonly struct UiDefaultButtonPresentation : IUiButtonPresentation
{
    public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize)
    {
        var skin = context.LegacySkin;
        var padding = context.DefaultPadding;
        return new(requestedSize.X > 0 ? requestedSize.X : MathF.Max(skin.MinimumWidth, textSize.X + (skin.PaddingX ?? padding.X) * 2),
            requestedSize.Y > 0 ? requestedSize.Y : MathF.Max(skin.MinimumHeight, textSize.Y + (skin.PaddingY ?? padding.Y) * 2));
    }

    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
    {
        var theme = context.Theme;
        var skin = context.LegacySkin;
        var tokens = context.Tokens;
        var fill = interaction.Held ? theme.ButtonActive : interaction.Hovered ? theme.ButtonHovered : theme.Button;
        var border = interaction.Held ? theme.ButtonBorderActive : interaction.Hovered ? theme.ButtonBorderHovered : theme.ButtonBorder;
        var radius = interaction.Held ? skin.PressedCornerRadius ?? skin.CornerRadius : skin.CornerRadius;
        context.Frame(bounds, fill, border, radius ?? tokens.ControlCornerRadius, skin.BorderWidth ?? tokens.ControlBorderWidth);
        context.Text(text, new(bounds.X + (bounds.Width - textSize.X) / 2,
            bounds.Y + (bounds.Height - textSize.Y) / 2 + (interaction.Held ? skin.PressedOffsetY ?? tokens.ControlPressedOffsetY : 0)), theme.ButtonText);
    }
}

/// <summary>A leaf uses the same retained identity and lifetime as a user-composed widget.</summary>
public abstract class UiControlWidget : UiWidget
{
    internal override UiWidget? ResolveLayoutRoot(UiImmediateContext ui)
    {
        ThrowIfDisposed();
        return this;
    }
    internal const string PrimaryControlId = "\u001fcontrol";
    protected UiControlWidget(UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity) { }
    protected override IUiView Build(UiBuildContext context) =>
        throw new InvalidOperationException("Leaf widgets render directly and do not build a child tree.");
    protected override void RenderContent(UiImmediateContext ui) => RenderControl(ui);
    protected abstract void RenderControl(UiImmediateContext ui);
    protected override UiVector2 MeasureContent(UiControlContext context) => throw new NotSupportedException("The leaf widget must provide pure measurement for layout containers.");
    protected string PrimaryInputId(string label) => HasScopedIdentity ? PrimaryControlId : label;
}

/// <summary>Inherit this button to customize activation while keeping a compiled behavior/presentation pair.</summary>
public class UiButton<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiButtonBehavior
    where TPresentation : IUiButtonPresentation
{
    private string _label;
    private UiVector2 _size;
    private Action? _activated;
    public UiButton(string label, Action? activated = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        _label = label;
        _activated = activated;
    }
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (string.Equals(_label, value, StringComparison.Ordinal))
                return;
            _label = value;
            RequestRebuild();
        }
    }
    public UiVector2 Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            if (_size == value)
                return;
            _size = value;
            RequestRebuild();
        }
    }
    public Action? Activated
    {
        get => _activated;
        set
        {
            ThrowIfDisposed();
            _activated = value;
        }
    }
    protected virtual void OnActivated() => Activated?.Invoke();
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureButton<TPresentation>(Label, Size);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var id = PrimaryInputId(Label);
        if (RenderButton(ui, id))
        {
            using var activation = ui.BeginControlActivation(id);
            OnActivated();
        }
    }
    private protected virtual bool RenderButton(UiImmediateContext ui, string id) =>
        ui.Button<TBehavior, TPresentation>(id, Label, Size, constrainLayout : true);
    private protected override void ReleaseOwnedResources()
    {
        _label = string.Empty;
        _activated = null;
        base.ReleaseOwnedResources();
    }
}

[UiComponent("LegacyButton", UiComponentExposure.Legacy)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class UiButton : UiButton<UiConfiguredButtonBehavior, UiDefaultButtonPresentation>
{
    public UiButton(string label, Action? activated = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(label, activated, identity) { }
}

/// <summary>Generates a named, inheritable button bound to concrete implementation types.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UiButtonStyleAttribute(Type behavior, Type presentation) : Attribute
{
    public Type Behavior { get; } = behavior;
    public Type Presentation { get; } = presentation;
}
