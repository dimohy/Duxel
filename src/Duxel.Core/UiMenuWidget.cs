namespace Duxel.Core;

public readonly record struct UiMenuOption(string Value, string Label, bool Enabled = true)
{
    /// <summary>A borrowed two-way state binding. Checkbox activation keeps the menu open.</summary>
    public UiBinding<bool>? Checked
    {
        get; private init;
    }
    public UiMenuRadioGroup? RadioGroup
    {
        get; private init;
    }
    public UiMenuIcon Icon
    {
        get; init;
    }
    /// <summary>Single-line trailing information; this does not register a keyboard shortcut.</summary>
    public string? SecondaryContent
    {
        get; init;
    }

    public static UiMenuOption Checkbox(string value, string label, UiBinding<bool> state, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(value, label, enabled)
        {
            Checked = state
        };
    }

    public static UiMenuOption Radio(string value, string label, UiMenuRadioGroup group, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new(value, label, enabled)
        {
            RadioGroup = group
        };
    }
}

/// <summary>A named exclusive menu group. Its binding remains owned by the caller.</summary>
public sealed class UiMenuRadioGroup
{
    public string Name
    {
        get;
    }
    public UiBinding<string?> Selection
    {
        get;
    }
    public UiMenuRadioGroup(string name, UiBinding<string?> selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selection);
        Name = name;
        Selection = selection;
    }
}

/// <summary>One frame's resolved content, including the menu-wide checkmark column.</summary>
public readonly record struct UiMenuItemContent(string Label, bool? Checked, bool HasCheckmarks)
{
    internal bool MultipleSelection
    {
        get; init;
    }
    public UiMenuIcon Icon
    {
        get; init;
    }
    public bool HasIcons
    {
        get; init;
    }
    public string? SecondaryContent
    {
        get; init;
    }
}

public readonly record struct UiMenuOptions(float MinimumWidth = 160, float ItemHeight = 32, float Padding = 4,
    bool RightToLeft = false, float ItemSpacing = 0, float? MaximumWidth = null)
{
    /// <summary>Optional surface height limit. Overflow uses the shared vertical scrollbar.</summary>
    public float? MaximumHeight
    {
        get; init;
    }
    public UiMenuOptions() : this(160, 32, 4, false) { }
    public UiMenuOptions Validate()
    {
        if (!float.IsFinite(MinimumWidth) || MinimumWidth <= 0 || !float.IsFinite(ItemHeight) || ItemHeight <= 0
            || !float.IsFinite(Padding) || Padding < 0 || !float.IsFinite(ItemSpacing) || ItemSpacing < 0
            || MaximumWidth is { } maximum && (!float.IsFinite(maximum) || maximum < MinimumWidth)
            || MaximumHeight is { } height && (!float.IsFinite(height) || height < ItemHeight + 2 * Padding))
            throw new ArgumentOutOfRangeException(nameof(UiMenuOptions));
        return this;
    }
}

/// <summary>A statically selected menu surface, independent of dismissal and composite focus.</summary>
public interface IUiMenuPresentation
{
    /// <summary>Paints a focused row overlay after item clips close and all rows have been painted.</summary>
    static virtual void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options) { }
    /// <summary>Promises deterministic trigger measurement; popup items and bindings remain live.</summary>
    static virtual bool CanRetainTriggerMeasurement => false;
    static virtual UiMenuOptions DefaultOptions => new();
    static abstract UiVector2 MeasureTrigger(UiControlContext context, string label);
    static abstract void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction);
    static abstract float MeasureItem(UiControlContext context, string label);
    static abstract void PaintSurface(UiControlContext context, UiRect bounds);
    static abstract void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options);
    static virtual float MeasureItem(UiControlContext context, UiMenuItemContent content) =>
        throw new NotSupportedException("This menu presentation must implement structured item content.");
    static virtual void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        UiButtonInteraction interaction, UiMenuOptions options) =>
        throw new NotSupportedException("This menu presentation must implement structured item content.");
}

/// <summary>Theme-based infrastructure presentation; Fluent menu styling is a separate presentation.</summary>
public readonly struct UiThemeMenuPresentation : IUiMenuPresentation
{
    public static bool CanRetainTriggerMeasurement => true;
    public static UiVector2 MeasureTrigger(UiControlContext context, string label)
    {
        using var typography = UiFluentButtonPresentation.BeginText(context, true);
        return UiFluentButtonPresentation.Measure(context, context.MeasureText(label), default);
    }
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
    {
        using var typography = UiFluentButtonPresentation.BeginText(context, true);
        UiFluentButtonPresentation.Paint(context, label, context.MeasureText(label), bounds, interaction);
    }
    public static float MeasureItem(UiControlContext context, string label)
    {
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        return context.MeasureText(label).X + 24;
    }
    public static void PaintSurface(UiControlContext context, UiRect bounds) =>
        context.Frame(bounds, context.Theme.PopupBg, context.Theme.Border, 4, 1);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => PaintItem(context, new UiMenuItemContent(label, null, false), bounds, interaction, options);

    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) =>
        UiMenuContentLayout.Measure(context, content, 12);

    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
    {
        var theme = context.Theme;
        var fill = interaction.Held ? theme.MenuItemBgActive
            : interaction.Hovered || interaction.FocusVisible ? theme.MenuItemBgHovered : default;
        context.Frame(bounds, fill, default, 2, 0);
        var color = context.IsEnabled ? theme.MenuItemText : theme.TextDisabled;
        UiMenuContentLayout.Paint(context, content, bounds, interaction, options, color, color,
            context.IsEnabled ? theme.TextDisabled : color, color, 12, 12);
    }
}

internal sealed class UiMenuInputState
{
    internal int PublishedFrame = int.MinValue;
    internal string? PublishedRoot;
    internal bool Open;
    internal Func<bool, bool>? CanChangeOpen;
    internal int DismissalVersion;
    internal bool FocusOnOpen;
    internal int Focused = -1;
    internal bool KeyboardPressed;
    internal bool RevealFocused;
    internal string? PressedValue;
    internal string? TriggerId;
    internal string? FocusOwnerId;
    internal string[] ReturnStops = [];
    internal UiRect BlockingRect;
    internal float ScrollOffset;
    internal readonly UiScrollOwner ScrollOwner = new();
    internal int BlockingTier;
    internal int BlockingFrame = -1;
    internal UiMenuOption[]? IdentityItems;
    internal string[] ItemIds = [];
    internal bool?[] Checked = [];
    internal UiMenuRadioGroup[] Groups = [];
    internal string[] GroupIds = [];
    internal int[] GroupIndex = [], GroupStarts = [], GroupEnds = [];
    internal string?[] Selection = [];
    internal string? Root;
    internal void Prepare(string root, UiMenuOption[] items)
    {
        if (Root == root && ReferenceEquals(IdentityItems, items))
            return;
        Root = root;
        RevealFocused = true;
        IdentityItems = items;
        ItemIds = new string[items.Length];
        Checked = new bool?[items.Length];
        GroupIndex = new int[items.Length];
        Array.Fill(GroupIndex, -1);
        var groups = new List<UiMenuRadioGroup>();
        var starts = new List<int>();
        var ends = new List<int>();
        for (var i = 0; i < items.Length; i++)
        {
            ItemIds[i] = root + "##item-" + Convert.ToHexString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(items[i].Value.AsSpan()));
            if (items[i].RadioGroup is not { } group)
                continue;
            var index = groups.IndexOf(group);
            if (index < 0)
            {
                index = groups.Count;
                groups.Add(group);
                starts.Add(i);
                ends.Add(i);
            }
            GroupIndex[i] = index;
            ends[index] = i;
        }
        Groups = groups.ToArray();
        GroupStarts = starts.ToArray();
        GroupEnds = ends.ToArray();
        Selection = new string?[Groups.Length];
        GroupIds = Groups.Select(group => root + "##radio-" + Convert.ToHexString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(group.Name.AsSpan()))).ToArray();
    }
}

/// <summary>Selects pointer activation for the popup trigger and its items.</summary>
public interface IUiMenuBehavior
{
    static abstract UiButtonActivation TriggerActivation { get; }
    static abstract UiButtonActivation OptionActivation { get; }
}

public readonly struct UiDefaultMenuBehavior : IUiMenuBehavior
{
    public static UiButtonActivation TriggerActivation => UiButtonActivation.Release;
    public static UiButtonActivation OptionActivation => UiButtonActivation.Release;
}

/// <summary>Owns menu data and one trigger; publishes a transient composite popup without a second UI tree.</summary>
public class UiMenu<TBehavior, TPresentation> : UiControlWidget
    where TBehavior : IUiMenuBehavior
    where TPresentation : IUiMenuPresentation
{
    private UiMenuOption[] _items;
    private readonly UiMenuInputState _input = new();
    private Action<string>? _invoked;
    private UiMenuOptions _options;
    public string Label
    {
        get;
    }
    public bool IsOpen => _input.Open;
    public ReadOnlySpan<UiMenuOption> Items => _items;
    public UiMenuOptions Options
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
            _input.RevealFocused = true;
            RequestRebuild();
        }
    }
    public UiMenu(string label, ReadOnlySpan<UiMenuOption> items, Action<string> invoked, UiMenuOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(invoked);
        _options = (options ?? TPresentation.DefaultOptions).Validate();
        _items = ValidateItems(items);
        Label = label;
        _invoked = invoked;
    }
    private static UiMenuOption[] ValidateItems(ReadOnlySpan<UiMenuOption> items)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        var names = new Dictionary<string, UiMenuRadioGroup>(StringComparer.Ordinal);
        var bindings = new HashSet<UiBinding<string?>>();
        var completedGroups = new HashSet<UiMenuRadioGroup>();
        UiMenuRadioGroup? previous = null;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Value) || string.IsNullOrWhiteSpace(item.Label) || !values.Add(item.Value))
                throw new ArgumentException("Menu entries require unique nonempty values and labels.", nameof(items));
            if (item.SecondaryContent is { } secondary && (string.IsNullOrWhiteSpace(secondary) || UiAccessibilityTree.ContainsControl(secondary, false)))
                throw new ArgumentException("Secondary menu content must be nonempty single-line text.", nameof(items));
            var group = item.RadioGroup;
            if (previous is not null && !ReferenceEquals(previous, group))
                completedGroups.Add(previous);
            if (group is not null)
            {
                if (completedGroups.Contains(group))
                    throw new ArgumentException("Radio group entries must be contiguous.", nameof(items));
                if (names.TryGetValue(group.Name, out var known))
                {
                    if (!ReferenceEquals(known, group))
                        throw new ArgumentException("Radio group names must identify one group instance.", nameof(items));
                }
                else
                {
                    names.Add(group.Name, group);
                    if (!bindings.Add(group.Selection))
                        throw new ArgumentException("Independent radio groups require independent bindings.", nameof(items));
                }
            }
            previous = group;
        }
        return items.ToArray();
    }
    public void SetItems(ReadOnlySpan<UiMenuOption> items)
    {
        ThrowIfDisposed();
        var next = ValidateItems(items);
        ThrowIfBuilding();
        var focused = (uint)_input.Focused < (uint)_items.Length ? _items[_input.Focused].Value : null;
        _items = next;
        _input.Focused = Array.FindIndex(next, item => item.Value == focused && item.Enabled);
        _input.PressedValue = null;
        _input.KeyboardPressed = false;
        RequestRebuild();
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainTriggerMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => TPresentation.MeasureTrigger(context, Label);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var value = RenderMenuControl(ui, _items, _options, _input);
        if (value is not null)
            _invoked?.Invoke(value);
    }

    private protected virtual string? RenderMenuControl(UiImmediateContext ui, UiMenuOption[] items,
        UiMenuOptions options, UiMenuInputState input) => ui.RenderMenu<TBehavior, TPresentation>(PrimaryInputId(Label), Label, items, options, input);
    private protected override void ReleaseOwnedResources()
    {
        _items = [];
        _input.Open = false;
        _input.ScrollOwner.Release();
        _input.ItemIds = [];
        _input.Checked = [];
        _input.Groups = [];
        _input.GroupIds = [];
        _input.GroupIndex = _input.GroupStarts = _input.GroupEnds = [];
        _input.Selection = [];
        _input.IdentityItems = null;
        _invoked = null;
        base.ReleaseOwnedResources();
    }
}

public class UiMenu<TPresentation>(string label, ReadOnlySpan<UiMenuOption> items, Action<string> invoked, UiMenuOptions? options = null)
    : UiMenu<UiDefaultMenuBehavior, TPresentation>(label, items, invoked, options)
    where TPresentation : IUiMenuPresentation;

public static partial class Dux
{
    public static UiMenu<UiThemeMenuPresentation> Menu(string label, ReadOnlySpan<UiMenuOption> items,
        Action<string> invoked, UiMenuOptions? options = null) => new(label, items, invoked, options);
}
