// Card constants follow Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

public enum UiCardOrientation { Vertical, Horizontal }
public enum UiCardSize { Medium, Small, Large }
public enum UiCardAppearance { Filled, FilledAlternative, Outline, Subtle }
public readonly record struct UiCardOptions(UiCardSize Size = UiCardSize.Medium,
    UiCardAppearance Appearance = UiCardAppearance.Filled, float? Width = null, float? Height = null,
    UiFocusGroupMode? FocusMode = null, bool Disabled = false, UiCardOrientation Orientation = UiCardOrientation.Vertical)
{
    public bool IsInteractive { get; internal init; }
    public bool HasPreview { get; internal init; }
    public bool IsSelectable { get; internal init; }
    public bool IsSelected { get; internal init; }
    internal UiFocusGroupMode EffectiveFocusMode => IsSelectable ? UiFocusGroupMode.Off
        : FocusMode ?? (IsInteractive ? UiFocusGroupMode.NoTab : UiFocusGroupMode.Off);
    public void Validate()
    {
        if ((uint)Size > 2 || (uint)Appearance > 3 || (uint)Orientation > 1 || FocusMode is { } mode && (uint)mode > 3)
            throw new ArgumentOutOfRangeException(nameof(UiCardOptions));
        new UiContainerOptions(Width: Width, Height: Height).Validate();
    }
}

public readonly record struct UiCardMetrics(float Padding, float CornerRadius, UiShadowElevation? Shadow = null,
    UiShadowElevation? ReservedShadow = null)
{
    internal UiEdgeInsets ShadowInsets
    {
        get
        {
            if ((ReservedShadow ?? Shadow) is not { } elevation) return default;
            var paint = UiFluentShadow.Resolve(UiFluentColorScheme.Light, elevation).PaintBounds(new(0, 0, 1, 1));
            return new(-paint.X, -paint.Y, paint.X + paint.Width - 1, paint.Y + paint.Height - 1);
        }
    }
    internal UiCardMetrics Validate()
    {
        if (!float.IsFinite(Padding) || !float.IsFinite(CornerRadius) || Padding < 0 || CornerRadius < 0)
            throw new InvalidOperationException("Card presentation metrics must be finite and nonnegative.");
        _ = ShadowInsets;
        if (Shadow is { } shadow)
        {
            _ = UiFluentShadow.Resolve(UiFluentColorScheme.Light, shadow);
            if (ReservedShadow is { } reserve && reserve < shadow)
                throw new InvalidOperationException("Card shadow reservation cannot be smaller than its painted shadow.");
        }
        return this;
    }
}

/// <summary>Statically selected metrics and Card painting; child layout/input remains in the owned content tree.</summary>
public interface IUiCardPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiCardMetrics Measure(UiCardOptions options);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiCardOptions options,
        UiCardMetrics metrics, UiButtonInteraction interaction);
    static virtual void PaintOverlay(UiControlContext context, UiRect bounds, UiCardOptions options,
        UiCardMetrics metrics, UiButtonInteraction interaction) { }
    static virtual UiShadowElevation? Shadow(UiCardOptions options, UiCardMetrics metrics,
        UiButtonInteraction interaction) => metrics.Shadow;
    static virtual UiColor? ContentForeground(UiControlContext context, UiCardOptions options, UiButtonInteraction interaction) => null;
}

public readonly struct UiCardLayout<TPresentation> : IUiSingleChildLayout<UiCardOptions>
    where TPresentation : IUiCardPresentation
{
    public static bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    public static void Validate(UiCardOptions options) => options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context, UiWidget? child,
        UiCardOptions options, UiBoxConstraints constraints)
    {
        var metrics = TPresentation.Measure(options).Validate();
        return context.MeasureCardBody(child ?? throw new InvalidOperationException("Card requires content."), options, metrics, constraints);
    }
}

/// <summary>Owns persistent content, a named accessibility group and an optional root action.</summary>
public class UiCard<TPresentation> : UiSingleChildBox<UiCardLayout<TPresentation>, UiCardOptions, UiPlainContainerPresentation>
    where TPresentation : IUiCardPresentation
{
    private string _label;
    private Action? _activated;
    private UiBinding<bool>? _selection;
    private UiBoxShadow? _shadow;
    private UiWidget? _floatingAction;
    private UiRetainedLayout<UiVector2>? _floatingMeasurement;
    internal UiCardSwipeDismissal? SwipeDismissal { get; init; }
    public UiCard(string label, UiWidget content, UiCardOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance, Action? activated = null, UiBinding<bool>? selection = null,
        UiWidget? floatingAction = null)
        : base(ValidateContent(label, content), options, identity, floatingAction, "floating-action")
    { _label = label; _activated = activated; _selection = selection; _floatingAction = floatingAction; }
    public UiWidget? FloatingAction { get { ThrowIfDisposed(); return _floatingAction; } }
    internal override void InvalidateRetainedMeasurement()
    {
        base.InvalidateRetainedMeasurement();
        _floatingMeasurement?.Invalidate();
    }
    public Action? Activated
    {
        get => _activated;
        set
        {
            ThrowIfDisposed();
            if (_activated == value) return;
            ThrowIfBuilding(); _activated = value; RequestRebuild();
        }
    }
    public UiBinding<bool>? Selection
    {
        get => _selection;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_selection, value)) return;
            ThrowIfBuilding(); _selection = value; RequestRebuild();
        }
    }
    protected override UiCardOptions LayoutOptions => Options with
    { IsInteractive = _activated is not null || _selection is not null, IsSelectable = _selection is not null, IsSelected = _selection?.Value ?? false };
    protected override UiCardOptions ResolveLayoutOptions(UiControlContext context)
        => LayoutOptions with { HasPreview = context.CardHasPreview(Child) };
    protected virtual void OnActivated() => _activated?.Invoke();
    protected virtual void OnSelectionChanged(UiBinding<bool> selection, bool selected) { selection.Value = selected; RequestRebuild(); }
    private static UiWidget ValidateContent(string label, UiWidget content)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(content);
        return content;
    }
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value);
            if (_label == value) return;
            ThrowIfBuilding(); _label = value; RequestRebuild();
        }
    }
    public new UiWidget Child => base.Child!;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var options = ResolveLayoutOptions(context);
        var metrics = TPresentation.Measure(options).Validate();
        if (metrics.Shadow is { } elevation)
            _shadow ??= new(UiFluentShadow.Resolve(context.FluentColorScheme, elevation));
        var selection = _selection;
        var layout = GetLayout(context, render: true);
        UiVector2 floatingSize = default;
        if (_floatingAction is { } action)
        {
            floatingSize = MeasureOwnedChild(this, action, context,
                UiBoxConstraints.Loose(new(MathF.Max(0, layout.Surface.Width - 8), MathF.Max(0, layout.Surface.Height - 8))),
                ref _floatingMeasurement, render: true);
        }
        var edit = ui.RenderCard<TPresentation>(_label, Child, options, metrics, _shadow, layout,
            _floatingAction, floatingSize, selection, SwipeDismissal);
        if (edit.SelectionChanged) OnSelectionChanged(selection!, edit.Selected);
        else if (edit.ChildSelectionChanged) RequestRebuild();
        if (edit.Activated) OnActivated();
    }
    private protected override void ReleaseOwnedResources()
    {
        _shadow?.Dispose(); _shadow = null; _label = string.Empty; _activated = null; _selection = null;
        _floatingAction = null; _floatingMeasurement = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiCard<>), typeof(UiFluentCardPresentation))]
[UiComponent("Card", UiComponentExposure.Fluent)]
public partial class UiFluentCard;

public readonly struct UiFluentCardPresentation : IUiCardPresentation
{
    public static UiShadowElevation? Shadow(UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) =>
        options.IsInteractive && !options.Disabled && interaction.Hovered
            && options.Appearance is UiCardAppearance.Filled or UiCardAppearance.FilledAlternative
            ? UiShadowElevation.Shadow8 : metrics.Shadow;
    public static bool CanRetainMeasurement => true;
    public static UiCardMetrics Measure(UiCardOptions options)
    {
        options.Validate();
        return new(
        options.Size switch { UiCardSize.Small => 8, UiCardSize.Large => 16, _ => 12 },
        options.Size switch { UiCardSize.Small => 2, UiCardSize.Large => 6, _ => 4 },
        options.Appearance == UiCardAppearance.Outline ? null
            : options.Disabled ? UiShadowElevation.Shadow2
            : options.Appearance is UiCardAppearance.Filled or UiCardAppearance.FilledAlternative ? UiShadowElevation.Shadow4 : null,
        options.Appearance is UiCardAppearance.Filled or UiCardAppearance.FilledAlternative
            ? options.IsInteractive ? UiShadowElevation.Shadow8 : UiShadowElevation.Shadow4 : null);
    }
    private readonly record struct SurfaceStyle(UiColor Fill, UiColor Border, float BorderWidth);
    private static SurfaceStyle RestStyle(UiControlContext context, UiCardOptions options)
    {
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var background = options.Disabled && options.Appearance != UiCardAppearance.Outline
            ? dark ? new UiColor(20, 20, 20) : new(240, 240, 240) : options.Appearance switch
        {
            UiCardAppearance.Filled => dark ? new UiColor(41, 41, 41) : new(255, 255, 255),
            UiCardAppearance.FilledAlternative => dark ? new UiColor(31, 31, 31) : new(250, 250, 250),
            _ => default
        };
        var border = options.Disabled ? dark ? new UiColor(66, 66, 66) : new(224, 224, 224)
            : options.Appearance == UiCardAppearance.Outline ? dark ? new UiColor(102, 102, 102) : new(209, 209, 209) : default;
        return new(background, border, options.Disabled || options.Appearance == UiCardAppearance.Outline ? 1 : 0);
    }
    public static UiColor? ContentForeground(UiControlContext context, UiCardOptions options, UiButtonInteraction interaction) =>
        options.Disabled ? context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(92, 92, 92) : new(189, 189, 189)
            : options.IsInteractive && interaction.Hovered
                ? context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(255, 255, 255) : new(36, 36, 36) : null;
    private static SurfaceStyle ResolveStyle(UiControlContext context, UiCardOptions options, UiButtonInteraction interaction)
    {
        if (options.IsSelected && !options.Disabled)
        {
            var selectedDark = context.FluentColorScheme == UiFluentColorScheme.Dark;
            var selectedLevel = options.Appearance switch
            {
                UiCardAppearance.Filled => selectedDark ? 56 : 235,
                UiCardAppearance.FilledAlternative => selectedDark ? 46 : 230,
                UiCardAppearance.Subtle => selectedDark ? 51 : 235,
                _ => 0
            };
            var selectedStroke = selectedDark ? 112 : 189;
            if (options.Appearance == UiCardAppearance.Outline && interaction.Hovered)
                selectedStroke = selectedDark ? interaction.Held ? 107 : 117 : interaction.Held ? 179 : 199;
            return new(options.Appearance == UiCardAppearance.Outline ? default : new((byte)selectedLevel, (byte)selectedLevel, (byte)selectedLevel),
                new((byte)selectedStroke, (byte)selectedStroke, (byte)selectedStroke), 1);
        }
        if (!options.IsInteractive || options.Disabled || !interaction.Hovered) return RestStyle(context, options);
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var pressed = interaction.Held;
        var level = options.Appearance switch
        {
            UiCardAppearance.Filled => dark ? pressed ? 31 : 61 : pressed ? 224 : 245,
            UiCardAppearance.FilledAlternative => dark ? pressed ? 20 : 51 : pressed ? 219 : 240,
            UiCardAppearance.Subtle => dark ? pressed ? 46 : 56 : pressed ? 224 : 245,
            _ => 0
        };
        var stroke = dark ? pressed ? 107 : 117 : pressed ? 179 : 199;
        return new(options.Appearance == UiCardAppearance.Outline ? default : new((byte)level, (byte)level, (byte)level),
            new((byte)stroke, (byte)stroke, (byte)stroke), options.Appearance == UiCardAppearance.Outline ? 1 : 0);
    }
    public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options,
        UiCardMetrics metrics, UiButtonInteraction interaction)
    {
        var style = ResolveStyle(context, options, interaction);
        if (!options.HasPreview) context.Frame(bounds, style.Fill, style.Border, metrics.CornerRadius, style.BorderWidth);
        else if (style.Fill.Rgba != 0) context.Frame(bounds, style.Fill, default, metrics.CornerRadius, 0);
    }
    public static void PaintOverlay(UiControlContext context, UiRect bounds, UiCardOptions options,
        UiCardMetrics metrics, UiButtonInteraction interaction)
    {
        if (!options.HasPreview) return;
        var style = ResolveStyle(context, options, interaction);
        if (style.BorderWidth > 0) context.Frame(bounds, default, style.Border, metrics.CornerRadius, style.BorderWidth);
    }

}

public sealed partial class UiImmediateContext
{
    internal readonly record struct CardEdit(bool Activated, bool SelectionChanged, bool Selected, bool ChildSelectionChanged);
    internal CardEdit RenderCard<TPresentation>(string label, UiWidget child, UiCardOptions options, UiCardMetrics metrics,
        UiBoxShadow? shadow, UiContainerLayoutResult layout, UiWidget? floatingAction, UiVector2 floatingSize,
        UiBinding<bool>? selection, UiCardSwipeDismissal? swipeDismissal) where TPresentation : IUiCardPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var origin = AllocateLayoutBox(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var surface = layout.Surface with { X = origin.X + layout.Surface.X, Y = origin.Y + layout.Surface.Y };
        var body = layout.Child with { X = origin.X + layout.Child.X, Y = origin.Y + layout.Child.Y };
        using var clip = context.Clip(root);
        var id = ResolveId("##card");
        var selectionId = options.IsSelectable && floatingAction is null ? ResolveId("##card-selection") : null;
        var interactive = options.IsInteractive && !options.Disabled && IsControlEnabled;
        var pointerInside = ContainsRoundedRect(surface, metrics.CornerRadius, _mousePosition);
        using var pointerClip = new LayoutPointerBlockScope(this, !pointerInside);
        var hovered = interactive && !_layoutPointerBlocked && pointerInside && _popupTierDepth >= _blockingKeyboardTier
            && IsHovering(IntersectRect(CurrentClipRect, surface));
        var activeWithin = _state.ActiveId is { } active && (active == id
            || !string.IsNullOrEmpty(_idPrefix) && active.Length > _idPrefix.Length
                && active[_idPrefix.Length] == '/' && active.StartsWith(_idPrefix, StringComparison.Ordinal));
        var interaction = new UiButtonInteraction(false, hovered, hovered && _leftMouseDown && (_leftMousePressed || activeWithin));
        if ((!interactive || _popupTierDepth < _blockingKeyboardTier) && _state.ActiveId == id) _state.ActiveId = null;
        if (TPresentation.Shadow(options, metrics, interaction) is { } paintShadow && surface.Width > 0 && surface.Height > 0)
        {
            if ((metrics.ReservedShadow ?? metrics.Shadow) is not { } reserved || paintShadow > reserved)
                throw new InvalidOperationException("Card interaction shadow exceeds its layout reservation.");
            if (shadow is not null) shadow.Options = UiFluentShadow.Resolve(context.FluentColorScheme, paintShadow);
            context.BoxShadow(shadow ?? throw new InvalidOperationException("Card shadow resource is missing."), surface, metrics.CornerRadius);
        }
        TPresentation.Paint(context, surface, options, metrics, interaction);
        var previousParent = _accessibilityParent;
        var visible = IntersectRect(CurrentClipRect, surface);
        var selectionKeyboard = selectionId is not null && PrepareCardSelection(selectionId, id, label, surface, options);
        var focusable = options.EffectiveFocusMode != UiFocusGroupMode.Off && !options.Disabled && IsControlEnabled
            && visible.Width > 0 && visible.Height > 0 && _popupTierDepth >= _blockingKeyboardTier;
        var focusGroup = focusable ? BeginControlFocusGroup(id, surface, options.EffectiveFocusMode) : -1;
        var trackChildPointer = focusGroup < 0 && (interactive || swipeDismissal is not null);
        if (trackChildPointer) _focusGroupPointerDepth++;
        var claims = _focusGroupPointerClaims;
        var foreground = TPresentation.ContentForeground(context, options, interaction);
        _accessibilityParent = id;
        if (foreground is { } color) PushStyleColor(UiStyleColor.Text, color);
        try
        {
            using var roundedClip = metrics.CornerRadius > 0 ? context.RoundedClip(surface, metrics.CornerRadius) : default;
            using var bodyScope = context.BeginCardBody(child, options, metrics, surface);
            var floating = new UiRect(surface.X + surface.Width - 4 - floatingSize.X, surface.Y + 4, floatingSize.X, floatingSize.Y);
            var tabStart = _currentTabStops.Count;
            var accessibilityStart = _accessibility?.StagedCount ?? 0;
            RenderParentDataChild(child, null, body, surface,
                blockPointer: floatingAction is not null && IsLayoutPointerInside(floating, surface),
                clipToAllocation: !options.HasPreview);
            if (floatingAction is not null)
            {
                var tabSplit = _currentTabStops.Count;
                var accessibilitySplit = _accessibility?.StagedCount ?? 0;
                RenderParentDataChild(floatingAction, null, floating, surface);
                // Paint above content, but retain the source's floating-action-first navigation order.
                _currentTabStops.Reverse(tabStart, tabSplit - tabStart);
                _currentTabStops.Reverse(tabSplit, _currentTabStops.Count - tabSplit);
                _currentTabStops.Reverse(tabStart, _currentTabStops.Count - tabStart);
                _accessibility?.PromoteTrailingStagedRange(accessibilityStart, accessibilitySplit);
            }
        }
        finally
        {
            if (foreground.HasValue) PopStyleColor();
            if (focusGroup >= 0 || trackChildPointer) _focusGroupPointerDepth--;
            _accessibilityParent = previousParent;
        }
        TPresentation.PaintOverlay(context, surface, options, metrics, interaction);
        var childClaimedPointer = claims != _focusGroupPointerClaims;
        if (swipeDismissal is not null)
            ProcessCardSwipeDismissal(swipeDismissal, surface,
                !options.Disabled && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, childClaimedPointer);
        if (focusGroup >= 0) EndControlFocusGroup(focusGroup, surface, childClaimedPointer);
        if (interactive)
        {
            var previousBlocked = _layoutPointerBlockedTier;
            if (childClaimedPointer) _layoutPointerBlockedTier = _popupTierDepth;
            try { interaction = ProcessControlPointer(selectionId is null ? "##card" : "##card-selection", surface, UiButtonActivation.Release); }
            finally { _layoutPointerBlockedTier = previousBlocked; }
            interaction = ApplyAccessibilityInvoke(id, surface, interaction);
            if (selectionId is not null && _leftMousePressed && !childClaimedPointer && interaction.Hovered)
            {
                _state.PendingTextInputFocusId = null;
                FocusGroupControl(selectionId, keyboard: false);
            }
        }
        var latestSelected = selection?.Value ?? false;
        var childSelectionChanged = options.IsSelectable && latestSelected != options.IsSelected;
        var selectionChanged = options.IsSelectable && (selectionKeyboard || interaction.Activated);
        var selected = selectionChanged ? !latestSelected : latestSelected;
        if (selectionId is not null && selected != options.IsSelected) _accessibility?.UpdateToggleState(selectionId, selected);
        if (selectionChanged || childSelectionChanged) _requestFrame?.Invoke();
        if (selectionId is not null && !options.Disabled && IsControlEnabled && _state.FocusedId == selectionId)
        {
            _focusedControlSeen = true; _focusedControlTier = _popupTierDepth;
            if (_state.NavCursorVisible) context.FocusOutline(surface, metrics.CornerRadius);
        }
        focusable &= _popupTierDepth >= _blockingKeyboardTier;
        var focused = focusable && _state.FocusedId == id;
        if (focused && _state.NavCursorVisible) context.FocusOutline(surface, metrics.CornerRadius);
        _accessibility?.Add(new(id, previousParent, UiAccessibilityRole.Card,
            _accessibilityWidget?.AccessibleName ?? label, visible, IsControlEnabled && !options.Disabled,
            visible.Width <= 0 || visible.Height <= 0, focused, Focusable: focusable, ValuePattern: false)
            { InvokePattern = options.IsInteractive }, surface);
        FinishLayoutBox(origin, layout.Size);
        return new(interaction.Activated, selectionChanged, selected, childSelectionChanged);
    }

    private readonly struct LayoutPointerBlockScope : IDisposable
    {
        private readonly UiImmediateContext _owner;
        private readonly int _previous;
        internal LayoutPointerBlockScope(UiImmediateContext owner, bool blocked)
        {
            _owner = owner; _previous = owner._layoutPointerBlockedTier;
            if (blocked) owner._layoutPointerBlockedTier = owner._popupTierDepth;
        }
        public void Dispose() => _owner._layoutPointerBlockedTier = _previous;
    }

    private static bool ContainsRoundedRect(UiRect bounds, float radius, UiVector2 point)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || point.X < bounds.X || point.Y < bounds.Y
            || point.X >= bounds.X + bounds.Width || point.Y >= bounds.Y + bounds.Height) return false;
        var r = MathF.Min(radius, MathF.Min(bounds.Width, bounds.Height) / 2);
        var dx = point.X - Math.Clamp(point.X, bounds.X + r, bounds.X + bounds.Width - r);
        var dy = point.Y - Math.Clamp(point.Y, bounds.Y + r, bounds.Y + bounds.Height - r);
        return dx * dx + dy * dy <= r * r;
    }

    private bool PrepareCardSelection(string id, string parent, string label, UiRect bounds, UiCardOptions options)
    {
        var previous = _lastItemId;
        BeginDisabled(options.Disabled);
        try
        {
            _lastItemId = id;
            var keys = ProcessControlKeyboard<ActionControlKeys>(bounds, default);
            var toggle = ApplyAccessibilityToggle(id, bounds);
            var visible = IntersectRect(CurrentClipRect, bounds);
            _accessibility?.Add(new(id, parent, UiAccessibilityRole.Checkbox, _accessibilityWidget?.AccessibleName ?? label,
                visible, IsControlEnabled, visible.Width <= 0 || visible.Height <= 0, _state.FocusedId == id,
                Focusable: IsControlEnabled && visible.Width > 0 && visible.Height > 0 && _popupTierDepth >= _blockingKeyboardTier,
                ValuePattern: false) { ToggleState = options.IsSelected ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off }, bounds);
            return keys.Activated || toggle;
        }
        finally { EndDisabled(); _lastItemId = previous; }
    }
}
