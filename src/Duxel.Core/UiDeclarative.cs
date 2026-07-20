namespace Duxel.Core;

public interface IUiView
{
    void Render(UiImmediateContext ui);
}

public interface IUiViewStyle
{
    IUiView Apply(IUiView view);
}

public sealed class UiView(Action<UiImmediateContext> render) : IUiView
{
    private readonly Action<UiImmediateContext> _render = render ?? throw new ArgumentNullException(nameof(render));

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _render(ui);
    }
}

public sealed class UiBinding<T>(Func<T> getValue, Action<T> setValue)
{
    private readonly Func<T> _getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
    private readonly Action<T> _setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));

    public T Value
    {
        get => _getValue();
        set => _setValue(value);
    }

    public UiBinding<TValue> Map<TValue>(Func<T, TValue> getValue, Func<T, TValue, T> setValue)
    {
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(setValue);
        return new UiBinding<TValue>(
            () => getValue(Value),
            value => Value = setValue(Value, value));
    }
}

public sealed class UiState<T>
{
    private readonly UiBinding<T> _binding;

    public UiState(T value)
    {
        Value = value;
        _binding = new UiBinding<T>(() => Value, value => Value = value);
    }

    public T Value { get; set; }

    public UiBinding<T> Binding => _binding;

    public void Set(T value)
    {
        Value = value;
    }

    public void Update(Func<T, T> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Value = update(Value);
    }

    public static implicit operator UiBinding<T>(UiState<T> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Binding;
    }

    public override string? ToString() => Value?.ToString();
}

public abstract class UiComponent : IUiView
{
    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        Build().Render(ui);
    }

    protected abstract IUiView Build();

    protected static UiState<T> State<T>(T value) => new(value);
}

public readonly record struct UiEdgeInsets(float Left, float Top, float Right, float Bottom)
{
    public UiEdgeInsets(float all)
        : this(all, all, all, all)
    {
    }

    public UiEdgeInsets(float horizontal, float vertical)
        : this(horizontal, vertical, horizontal, vertical)
    {
    }
}

public sealed class UiModifier
{
    private readonly IReadOnlyList<UiModifierScope> _scopes;

    private UiModifier(IReadOnlyList<UiModifierScope> scopes)
    {
        _scopes = scopes;
    }

    public static UiModifier Empty { get; } = new([]);

    public UiModifier Then(UiModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        return new UiModifier([.._scopes, ..modifier._scopes]);
    }

    public IUiView Apply(IUiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (_scopes.Count == 0)
        {
            return view;
        }

        return new UiView(ui =>
        {
            for (var i = 0; i < _scopes.Count; i++)
            {
                _scopes[i].Enter(ui);
            }

            try
            {
                view.Render(ui);
            }
            finally
            {
                for (var i = _scopes.Count - 1; i >= 0; i--)
                {
                    _scopes[i].Exit(ui);
                }
            }
        });
    }

    public static UiModifier Disabled(bool disabled = true)
        => Scope(ui => ui.BeginDisabled(disabled), ui => ui.EndDisabled());

    public static UiModifier FontSize(float size)
        => Scope(ui => ui.PushFontSize(size), ui => ui.PopFontSize());

    public static UiModifier Foreground(UiColor color)
        => StyleColor(UiStyleColor.Text, color);

    public static UiModifier Foreground(UiStyleColor color)
        => Scope(ui => ui.PushStyleColor(UiStyleColor.Text, ui.GetColorU32(color)), ui => ui.PopStyleColor());

    public static UiModifier Accent()
        => Foreground(UiStyleColor.CheckMark);

    public static UiModifier Muted()
        => Foreground(UiStyleColor.TextDisabled);

    public static UiModifier Tone(UiTextTone tone)
        => Scope(ui => ui.PushStyleColor(UiStyleColor.Text, ResolveTextTone(ui, tone)), ui => ui.PopStyleColor());

    public static UiModifier ButtonRole(UiButtonRole role)
        => role is UiButtonRole.Normal
            ? Empty
            : Scope(ui => PushButtonRole(ui, role), ui => ui.PopStyleColor(6));

    public static UiModifier ItemWidth(float width)
        => Scope(ui => ui.PushItemWidth(width), ui => ui.PopItemWidth());

    public static UiModifier FillWidth()
        => Scope(
            ui => ui.PushItemWidth(MathF.Max(1f, ui.GetContentRegionAvail().X)),
            ui => ui.PopItemWidth());

    public static UiModifier ItemSpacing(UiVector2 spacing)
        => StyleVar(UiStyleVar.ItemSpacing, spacing);

    public static UiModifier TextWrap(float wrapPosX = 0f)
        => Scope(ui => ui.PushTextWrapPos(wrapPosX), ui => ui.PopTextWrapPos());

    public static UiModifier Tooltip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Scope(_ => { }, ui => ui.SetItemTooltip(text));
    }

    public static UiModifier Tooltip(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Scope(_ => { }, ui => ui.SetItemTooltip(text()));
    }

    public static UiModifier StyleColor(UiStyleColor color, UiColor value)
        => Scope(ui => ui.PushStyleColor(color, value), ui => ui.PopStyleColor());

    public static UiModifier StyleVar(UiStyleVar styleVar, float value)
        => Scope(ui => ui.PushStyleVar(styleVar, value), ui => ui.PopStyleVar());

    public static UiModifier StyleVar(UiStyleVar styleVar, UiVector2 value)
        => Scope(ui => ui.PushStyleVar(styleVar, value), ui => ui.PopStyleVar());

    private static UiModifier Scope(Action<UiImmediateContext> enter, Action<UiImmediateContext> exit)
        => new([new UiModifierScope(enter, exit)]);

    private static UiColor ResolveTextTone(UiImmediateContext ui, UiTextTone tone)
        => tone switch
        {
            UiTextTone.Normal => ui.GetColorU32(UiStyleColor.Text),
            UiTextTone.Muted => ui.GetColorU32(UiStyleColor.TextDisabled),
            UiTextTone.Accent => ui.GetColorU32(UiStyleColor.CheckMark),
            UiTextTone.Success => new UiColor(35, 134, 54),
            UiTextTone.Warning => new UiColor(210, 153, 34),
            UiTextTone.Danger => new UiColor(218, 54, 51),
            _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
        };

    private static void PushButtonRole(UiImmediateContext ui, UiButtonRole role)
    {
        var baseColor = role switch
        {
            UiButtonRole.Primary => ui.GetColorU32(UiStyleColor.CheckMark),
            UiButtonRole.Danger => new UiColor(218, 54, 51),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };

        ui.PushStyleColor(UiStyleColor.Button, WithAlpha(baseColor, 210));
        ui.PushStyleColor(UiStyleColor.ButtonHovered, WithAlpha(baseColor, 232));
        ui.PushStyleColor(UiStyleColor.ButtonActive, WithAlpha(baseColor, 255));
        ui.PushStyleColor(UiStyleColor.ButtonBorder, WithAlpha(baseColor, 255));
        ui.PushStyleColor(UiStyleColor.ButtonBorderHovered, WithAlpha(baseColor, 255));
        ui.PushStyleColor(UiStyleColor.ButtonText, new UiColor(255, 255, 255));
    }

    private static UiColor WithAlpha(UiColor color, byte alpha)
        => new((color.Rgba & 0x00FFFFFFu) | ((uint)alpha << 24));

    private readonly record struct UiModifierScope(Action<UiImmediateContext> Enter, Action<UiImmediateContext> Exit);
}

public readonly record struct UiWindowOptions(
    UiVector2 Position = default,
    UiVector2 Size = default,
    UiBinding<bool>? Open = null,
    bool Focus = false,
    bool TopMost = false);

public readonly record struct UiSectionOptions(
    bool DefaultOpen = true,
    bool Collapsible = true,
    float Spacing = 8f);

public readonly record struct UiFormRow(
    string Label,
    IUiView Content,
    float LabelWidth = 140f);

public readonly record struct UiChoice<T>(
    T Value,
    string Label);

public readonly record struct UiTab(
    string Label,
    IUiView Content);

public readonly record struct UiTableColumn(
    string Header,
    float Width = 0f,
    float AlignX = 0f);

public readonly record struct UiSwitchCase<T>(
    T Value,
    IUiView Content);

public enum UiTextTone
{
    Normal,
    Muted,
    Accent,
    Success,
    Warning,
    Danger,
}

public enum UiButtonRole
{
    Normal,
    Primary,
    Danger,
}

public readonly record struct UiTextStyle(
    float FontSize = 0f,
    UiStyleColor? Foreground = null,
    UiTextTone? Tone = null,
    bool Wrap = false) : IUiViewStyle
{
    public static UiTextStyle Body => new();

    public static UiTextStyle Title => new(28f, UiStyleColor.Text);

    public static UiTextStyle Subtitle => new(20f, UiStyleColor.Text);

    public static UiTextStyle Caption => new(12f, UiStyleColor.TextDisabled);

    public static UiTextStyle Muted => new(Foreground: UiStyleColor.TextDisabled);

    public static UiTextStyle Accent => new(Foreground: UiStyleColor.CheckMark);

    public static UiTextStyle Success => new(Tone: UiTextTone.Success);

    public static UiTextStyle Warning => new(Tone: UiTextTone.Warning);

    public static UiTextStyle Danger => new(Tone: UiTextTone.Danger);

    public IUiView Apply(IUiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (Wrap)
        {
            view = view.TextWrap();
        }

        if (Foreground is { } foreground)
        {
            view = view.Foreground(foreground);
        }

        if (Tone is { } tone)
        {
            view = view.Tone(tone);
        }

        return FontSize > 0f ? view.FontSize(FontSize) : view;
    }
}

public readonly record struct UiButtonStyle(UiButtonRole Role = UiButtonRole.Normal) : IUiViewStyle
{
    public static UiButtonStyle Normal => new(UiButtonRole.Normal);

    public static UiButtonStyle Primary => new(UiButtonRole.Primary);

    public static UiButtonStyle Danger => new(UiButtonRole.Danger);

    public IUiView Apply(IUiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return Role is UiButtonRole.Normal ? view : view.ButtonRole(Role);
    }
}

public readonly record struct UiCommand(
    string Label,
    Action? Action,
    UiButtonRole Role = UiButtonRole.Normal,
    Func<bool>? Enabled = null,
    Func<string>? Tooltip = null,
    UiVector2 Size = default);

public readonly record struct UiCommandBarOptions(
    float Spacing = 8f,
    bool Small = false,
    UiVector2 ButtonSize = default);

public enum UiBadgeTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
}

public readonly record struct UiBadgeOptions(
    UiBadgeTone Tone = UiBadgeTone.Accent,
    UiColor? Background = null,
    UiColor? Foreground = null,
    UiColor? Border = null,
    UiVector2 Padding = default,
    float Radius = 999f,
    float FontSize = 0f);

public readonly record struct UiMetricOptions(
    UiColor? ValueColor = null,
    UiColor? LabelColor = null,
    float ValueFontSize = 24f,
    float LabelFontSize = 13f,
    float Spacing = 3f);

public readonly record struct UiMetricCardOptions(
    UiTextTone ValueTone = UiTextTone.Accent,
    UiTextTone DetailTone = UiTextTone.Muted,
    float Height = 104f,
    float Padding = 14f,
    float ValueFontSize = 26f,
    float LabelFontSize = 12f,
    float Spacing = 5f);

public readonly record struct UiMetaItem(
    Func<string> Text,
    UiTextTone Tone = UiTextTone.Accent);

public readonly record struct UiPropertyItem(
    string Label,
    Func<string> Value,
    UiTextTone ValueTone = UiTextTone.Normal);

public readonly record struct UiPropertyListOptions(
    float LabelWidth = 86f,
    float Spacing = 6f);

public readonly record struct UiSettingItem(
    string Label,
    IUiView Content,
    Func<string>? Description = null);

public readonly record struct UiSettingsGroupOptions(
    float LabelWidth = 130f,
    float Spacing = 10f,
    float Padding = 12f,
    float Height = 0f,
    bool Panel = true);

public readonly record struct UiStatusRowOptions(
    float TitleWidth = 180f,
    float MetaWidth = 96f,
    float ProgressWidth = 120f,
    float RowHeight = 24f,
    float ProgressHeight = 18f,
    float Spacing = 10f,
    Func<string>? Tooltip = null);

public readonly record struct UiEmptyStateOptions(
    float Height = 120f,
    float Padding = 14f,
    UiTextTone MessageTone = UiTextTone.Muted);

public readonly record struct UiCalloutOptions(
    UiTextTone Tone = UiTextTone.Accent,
    float Height = 0f,
    float Padding = 14f,
    float TitleFontSize = 15f,
    float MessageFontSize = 0f);

public readonly record struct UiShellItem(
    string Label,
    IUiView Content,
    Func<string>? Detail = null,
    UiTextTone DetailTone = UiTextTone.Muted);

public readonly record struct UiShellItem<TValue>(
    TValue Value,
    string Label,
    IUiView Content,
    Func<string>? Detail = null,
    UiTextTone DetailTone = UiTextTone.Muted);

public readonly record struct UiAppShellOptions(
    string? WindowTitle = null,
    string SidebarTitle = "Navigation",
    UiVector2 Position = default,
    UiVector2 Size = default,
    float SidebarWidth = 176f,
    float SidebarRowHeight = 30f,
    float Padding = 14f,
    float Spacing = 12f,
    bool Focus = false,
    bool TopMost = false,
    UiBinding<bool>? Open = null,
    IUiView? Menu = null,
    IUiView? Commands = null,
    IUiView? Footer = null);

public readonly record struct UiSurfaceOptions(
    UiVector2 Size = default,
    UiEdgeInsets Padding = default,
    UiColor? Background = null,
    UiColor? Border = null,
    float Radius = 8f,
    bool Clip = true);

public readonly record struct UiDecorationOptions(
    UiVector2 Size = default,
    UiColor? Background = null,
    UiStyleColor? BackgroundStyle = null,
    UiColor? Border = null,
    UiStyleColor? BorderStyle = null,
    float Radius = 0f,
    UiDesignToken? RadiusToken = null,
    float BorderThickness = 1f,
    bool FillWidth = false,
    UiEdgeInsets Padding = default);

public readonly record struct UiPanelStyle(
    UiEdgeInsets Padding = default,
    UiStyleColor? Background = null,
    UiStyleColor? Border = null,
    UiDesignToken? Radius = null,
    float BorderThickness = 1f,
    float Height = 0f,
    bool? FillWidth = null) : IUiViewStyle
{
    public IUiView Apply(IUiView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var padding = Padding == default ? new UiEdgeInsets(14f) : Padding;
        var thickness = BorderThickness > 0f ? BorderThickness : 1f;
        var fillWidth = FillWidth ?? true;
        var styled = view
            .Padding(padding)
            .Background(Background ?? UiStyleColor.FrameBg)
            .Border(Border ?? UiStyleColor.Border, thickness)
            .CornerRadius(Radius ?? UiDesignToken.ControlCornerRadius);

        if (fillWidth)
        {
            return styled.FillFrameWidth(Height);
        }

        return Height > 0f ? styled.Height(Height) : styled;
    }
}

public readonly record struct UiSegmentedOptions(
    UiVector2 ItemSize = default,
    float Spacing = 0f,
    bool FillWidth = false);

public readonly record struct UiGridOptions(
    int Columns = 2,
    float Spacing = 8f);

public static class DuxelView
{
    public static DuxelLayoutFactory Layout { get; } = new();
    public static DuxelControlFactory Controls { get; } = new();
    public static DuxelTextFactory Text { get; } = new();
    public static DuxelMenuFactory Menus { get; } = new();
    public static DuxelWindowFactory Windows { get; } = new();
    public static DuxelDisplayFactory Display { get; } = new();

    public static IUiView Custom(Action<UiImmediateContext> render) => new UiView(render);

    public static IUiView Widget(IUiCustomWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return new UiView(widget.Render);
    }

    public static UiScreen App(IUiView root) => Screen(root);

    public static UiScreen Screen(IUiView root) => new DeclarativeUiScreen(root);

    internal static void RenderChildren(UiImmediateContext ui, IReadOnlyList<IUiView> children)
    {
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Render(ui);
        }
    }

    private sealed class DeclarativeUiScreen(IUiView root) : UiScreen
    {
        private readonly IUiView _root = root ?? throw new ArgumentNullException(nameof(root));

        public override void Render(UiImmediateContext ui)
        {
            _root.Render(ui);
        }
    }
}

public sealed class DuxelLayoutFactory
{
    internal DuxelLayoutFactory() { }

    public IUiView Column(params IUiView[] children)
        => VStack(children);

    public IUiView Column(float spacing, params IUiView[] children)
        => VStack(spacing, children);

    public IUiView VStack(params IUiView[] children)
        => new UiView(ui => DuxelView.RenderChildren(ui, children));

    public IUiView VStack(float spacing, params IUiView[] children)
        => UiModifier.ItemSpacing(new UiVector2(spacing, spacing)).Apply(VStack(children));

    public IUiView Row(params IUiView[] children)
        => HStack(children);

    public IUiView Row(float spacing, params IUiView[] children)
        => HStack(spacing, children);

    public IUiView HStack(params IUiView[] children)
        => new UiView(ui =>
        {
            ui.BeginGroup();
            try
            {
                for (var i = 0; i < children.Length; i++)
                {
                    if (i > 0)
                    {
                        ui.SameLine();
                    }

                    children[i].Render(ui);
                }
            }
            finally
            {
                ui.EndGroup();
            }
        });

    public IUiView HStack(float spacing, params IUiView[] children)
        => UiModifier.ItemSpacing(new UiVector2(spacing, spacing)).Apply(HStack(children));

    public IUiView Group(params IUiView[] children)
        => new UiView(ui =>
        {
            ui.BeginGroup();
            try
            {
                DuxelView.RenderChildren(ui, children);
            }
            finally
            {
                ui.EndGroup();
            }
        });

    public IUiView Child(string id, IUiView content, UiVector2 size = default, bool border = false)
        => new UiView(ui =>
        {
            var opened = ui.BeginChild(id, size, border);
            try
            {
                if (opened)
                {
                    content.Render(ui);
                }
            }
            finally
            {
                ui.EndChild();
            }
        });

    public IUiView Key(object? key, IUiView content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            ui.PushID(FormatKey(key));
            try
            {
                content.Render(ui);
            }
            finally
            {
                ui.PopID();
            }
        });
    }

    public IUiView ScrollView(string id, IUiView content, UiVector2 size = default, bool border = false)
        => Child(id, content, size, border);

    public IUiView Section(string title, IUiView content, UiSectionOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default ? new UiSectionOptions() : options;
            var open = !effectiveOptions.Collapsible || ui.CollapsingHeader(title, effectiveOptions.DefaultOpen);
            if (!open)
            {
                return;
            }

            ui.Indent();
            try
            {
                content.Render(ui);
            }
            finally
            {
                ui.Unindent();
            }

            if (effectiveOptions.Spacing > 0f)
            {
                ui.Dummy(new UiVector2(1f, effectiveOptions.Spacing));
            }
        });
    }

    public IUiView Form(params UiFormRow[] rows)
        => Form(140f, rows);

    public UiFormRow Field(string label, IUiView content, float labelWidth = 140f)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiFormRow(label, content, labelWidth);
    }

    public IUiView Form(float labelWidth, params UiFormRow[] rows)
        => new UiView(ui =>
        {
            ui.Columns(2);
            try
            {
                ui.SetColumnWidth(0, MathF.Max(1f, labelWidth));
                for (var i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    var rowLabelWidth = row.LabelWidth > 0f ? row.LabelWidth : labelWidth;
                    ui.SetColumnWidth(0, MathF.Max(1f, rowLabelWidth));
                    ui.Text(row.Label);
                    ui.NextColumn();
                    row.Content.Render(ui);
                    ui.NextColumn();
                }
            }
            finally
            {
                ui.Columns(1);
            }
        });

    public IUiView FormRow(string label, IUiView content, float labelWidth = 140f)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            ui.Columns(2);
            try
            {
                ui.SetColumnWidth(0, MathF.Max(1f, labelWidth));
                ui.Text(label);
                ui.NextColumn();
                content.Render(ui);
                ui.NextColumn();
            }
            finally
            {
                ui.Columns(1);
            }
        });
    }

    public IUiView List<T>(string id, IEnumerable<T> source, Func<T, IUiView> row, UiVector2 size = default, bool border = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(row);
        return Child(id, ForEach(source, row), size, border);
    }

    public IUiView List<T>(string id, IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> row, UiVector2 size = default, bool border = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(row);
        return Child(id, ForEach(source, key, row), size, border);
    }

    public IUiView List<T>(string id, Func<IEnumerable<T>> source, Func<T, IUiView> row, UiVector2 size = default, bool border = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(row);
        return Child(id, ForEach(source, row), size, border);
    }

    public IUiView List<T>(string id, Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> row, UiVector2 size = default, bool border = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(row);
        return Child(id, ForEach(source, key, row), size, border);
    }

    public IUiView Grid(params IUiView[] children)
        => Grid(new UiGridOptions(), children);

    public IUiView Grid(int columns, params IUiView[] children)
        => Grid(new UiGridOptions(Columns: columns), children);

    public IUiView Grid(UiGridOptions options, params IUiView[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        return RenderGrid(options, ui =>
        {
            for (var i = 0; i < children.Length; i++)
            {
                children[i].Render(ui);
                if (i < children.Length - 1)
                {
                    ui.NextColumn();
                }
            }
        });
    }

    public IUiView Grid<T>(IEnumerable<T> source, Func<T, IUiView> cell, UiGridOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cell);
        return RenderGrid(options, ui =>
        {
            var rendered = 0;
            foreach (var item in source)
            {
                if (rendered > 0)
                {
                    ui.NextColumn();
                }

                cell(item).Render(ui);
                rendered++;
            }
        });
    }

    public IUiView Grid<T>(IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> cell, UiGridOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(cell);
        return RenderGrid(options, ui =>
        {
            var rendered = 0;
            foreach (var item in source)
            {
                if (rendered > 0)
                {
                    ui.NextColumn();
                }

                Key(key(item), cell(item)).Render(ui);
                rendered++;
            }
        });
    }

    public IUiView Grid<T>(Func<IEnumerable<T>> source, Func<T, IUiView> cell, UiGridOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cell);
        return RenderGrid(options, ui =>
        {
            var rendered = 0;
            foreach (var item in source())
            {
                if (rendered > 0)
                {
                    ui.NextColumn();
                }

                cell(item).Render(ui);
                rendered++;
            }
        });
    }

    public IUiView Grid<T>(Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> cell, UiGridOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(cell);
        return RenderGrid(options, ui =>
        {
            var rendered = 0;
            foreach (var item in source())
            {
                if (rendered > 0)
                {
                    ui.NextColumn();
                }

                Key(key(item), cell(item)).Render(ui);
                rendered++;
            }
        });
    }

    public IUiView Split(params IUiView[] columns)
        => new UiView(ui =>
        {
            if (columns.Length is 0)
            {
                return;
            }

            ui.Columns(columns.Length);
            try
            {
                for (var i = 0; i < columns.Length; i++)
                {
                    columns[i].Render(ui);
                    if (i < columns.Length - 1)
                    {
                        ui.NextColumn();
                    }
                }
            }
            finally
            {
                ui.Columns(1);
            }
        });

    private static IUiView RenderGrid(UiGridOptions options, Action<UiImmediateContext> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default ? new UiGridOptions() : options;
            var columns = Math.Max(1, effectiveOptions.Columns);
            var spacing = MathF.Max(0f, effectiveOptions.Spacing);
            if (columns is 1)
            {
                UiModifier.ItemSpacing(new UiVector2(spacing, spacing)).Apply(new UiView(render)).Render(ui);
                return;
            }

            ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(spacing, spacing));
            ui.Columns(columns);
            try
            {
                render(ui);
            }
            finally
            {
                ui.Columns(1);
                ui.PopStyleVar();
            }
        });
    }

    public UiTab Tab(string label, IUiView content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiTab(label, content);
    }

    public IUiView Tabs(string id, params UiTab[] tabs)
        => new UiView(ui =>
        {
            var activeIndex = -1;
            if (ui.BeginTabBar(id))
            {
                try
                {
                    for (var i = 0; i < tabs.Length; i++)
                    {
                        if (ui.BeginTabItem(tabs[i].Label))
                        {
                            activeIndex = i;
                        }

                        ui.EndTabItem();
                    }
                }
                finally
                {
                    ui.EndTabBar();
                }
            }

            if ((uint)activeIndex < (uint)tabs.Length)
            {
                tabs[activeIndex].Content.Render(ui);
            }
        });

    public IUiView Tree(string label, IUiView content, bool defaultOpen = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            if (!ui.TreeNode(label, defaultOpen))
            {
                return;
            }

            try
            {
                content.Render(ui);
            }
            finally
            {
                ui.TreePop();
            }
        });
    }

    public IUiView Table<T>(string id, IReadOnlyList<UiTableColumn> columns, IEnumerable<T> rows, Func<T, IReadOnlyList<IUiView>> cells, UiTableFlags flags = UiTableFlags.Borders | UiTableFlags.RowBg)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(cells);
        return new UiView(ui =>
        {
            if (columns.Count is 0 || !ui.BeginTable(id, columns.Count, flags))
            {
                return;
            }

            try
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];
                    ui.TableSetupColumn(column.Header, column.Width, column.AlignX);
                }

                ui.TableHeadersRow();
                foreach (var row in rows)
                {
                    var rowCells = cells(row);
                    ui.TableNextRow();
                    var cellCount = Math.Min(columns.Count, rowCells.Count);
                    for (var i = 0; i < cellCount; i++)
                    {
                        ui.TableSetColumnIndex(i);
                        rowCells[i].Render(ui);
                    }
                }
            }
            finally
            {
                ui.EndTable();
            }
        });
    }

    public IUiView Spacer(float height = 0f)
        => new UiView(ui =>
        {
            if (height > 0f)
            {
                ui.Dummy(new UiVector2(1f, height));
            }
            else
            {
                ui.Spacing();
            }
        });

    public IUiView Separator()
        => new UiView(ui => ui.Separator());

    public IUiView Separator(string text)
        => new UiView(ui => ui.SeparatorText(text));

    public IUiView If(bool condition, IUiView content)
        => condition ? content : Dux.Empty();

    public IUiView If(Func<bool> condition, IUiView content)
        => If(condition, content, Dux.Empty());

    public IUiView If(Func<bool> condition, IUiView whenTrue, IUiView whenFalse)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        return new UiView(ui =>
        {
            if (condition())
            {
                whenTrue.Render(ui);
            }
            else
            {
                whenFalse.Render(ui);
            }
        });
    }

    public IUiView When(bool condition, IUiView content)
        => If(condition, content);

    public IUiView When(Func<bool> condition, IUiView content)
        => If(condition, content);

    public IUiView Unless(bool condition, IUiView content)
        => If(!condition, content);

    public IUiView Unless(Func<bool> condition, IUiView content)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return If(() => !condition(), content);
    }

    public UiSwitchCase<T> Case<T>(T value, IUiView content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiSwitchCase<T>(value, content);
    }

    public IUiView Switch<T>(T value, params UiSwitchCase<T>[] cases)
        => Switch(() => value, Dux.Empty(), cases);

    public IUiView Switch<T>(T value, IUiView defaultContent, params UiSwitchCase<T>[] cases)
        => Switch(() => value, defaultContent, cases);

    public IUiView Switch<T>(Func<T> value, params UiSwitchCase<T>[] cases)
        => Switch(value, Dux.Empty(), cases);

    public IUiView Switch<T>(Func<T> value, IUiView defaultContent, params UiSwitchCase<T>[] cases)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(defaultContent);
        ArgumentNullException.ThrowIfNull(cases);
        return new UiView(ui =>
        {
            var current = value();
            for (var i = 0; i < cases.Length; i++)
            {
                var switchCase = cases[i];
                if (!EqualityComparer<T>.Default.Equals(current, switchCase.Value))
                {
                    continue;
                }

                ArgumentNullException.ThrowIfNull(switchCase.Content);
                switchCase.Content.Render(ui);
                return;
            }

            defaultContent.Render(ui);
        });
    }

    public IUiView ForEach<T>(IEnumerable<T> source, Func<T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            foreach (var item in source)
            {
                content(item).Render(ui);
            }
        });
    }

    public IUiView ForEach<T>(IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            foreach (var item in source)
            {
                ui.PushID(FormatKey(key(item)));
                try
                {
                    content(item).Render(ui);
                }
                finally
                {
                    ui.PopID();
                }
            }
        });
    }

    public IUiView ForEach<T>(Func<IEnumerable<T>> source, Func<T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            foreach (var item in source())
            {
                content(item).Render(ui);
            }
        });
    }

    public IUiView ForEach<T>(Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            foreach (var item in source())
            {
                ui.PushID(FormatKey(key(item)));
                try
                {
                    content(item).Render(ui);
                }
                finally
                {
                    ui.PopID();
                }
            }
        });
    }

    public IUiView ForEachIndexed<T>(IEnumerable<T> source, Func<int, T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var index = 0;
            foreach (var item in source)
            {
                content(index, item).Render(ui);
                index++;
            }
        });
    }

    public IUiView ForEachIndexed<T>(IEnumerable<T> source, Func<int, T, object?> key, Func<int, T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var index = 0;
            foreach (var item in source)
            {
                ui.PushID(FormatKey(key(index, item)));
                try
                {
                    content(index, item).Render(ui);
                }
                finally
                {
                    ui.PopID();
                }

                index++;
            }
        });
    }

    public IUiView ForEachIndexed<T>(Func<IEnumerable<T>> source, Func<int, T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var index = 0;
            foreach (var item in source())
            {
                content(index, item).Render(ui);
                index++;
            }
        });
    }

    public IUiView ForEachIndexed<T>(Func<IEnumerable<T>> source, Func<int, T, object?> key, Func<int, T, IUiView> content)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var index = 0;
            foreach (var item in source())
            {
                ui.PushID(FormatKey(key(index, item)));
                try
                {
                    content(index, item).Render(ui);
                }
                finally
                {
                    ui.PopID();
                }

                index++;
            }
        });
    }

    private static string FormatKey(object? key)
        => key switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => key.ToString() ?? string.Empty,
        };
}

public sealed class DuxelControlFactory
{
    internal DuxelControlFactory() { }

    public IUiView Button(string label, Action? clicked = null, UiVector2 size = default)
        => new UiView(ui =>
        {
            if (ui.Button(label, size))
            {
                clicked?.Invoke();
            }
        });

    public IUiView SmallButton(string label, Action? clicked = null)
        => new UiView(ui =>
        {
            if (ui.SmallButton(label))
            {
                clicked?.Invoke();
            }
        });

    public IUiView Checkbox(string label, UiBinding<bool> value)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value;
            if (ui.Checkbox(label, ref current))
            {
                value.Value = current;
            }
        });

    public IUiView Checkbox(string label, Func<bool> getValue, Action<bool> setValue)
        => Checkbox(label, Dux.Bind(getValue, setValue));

    public IUiView Toggle(string id, UiBinding<bool> value)
        => Checkbox(HiddenControlLabel(id), value);

    public IUiView Toggle(string id, Func<bool> getValue, Action<bool> setValue)
        => Toggle(id, Dux.Bind(getValue, setValue));

    public IUiView RadioButton(string label, UiBinding<int> value, int buttonValue)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value;
            if (ui.RadioButton(label, ref current, buttonValue))
            {
                value.Value = current;
            }
        });

    public IUiView RadioButton(string label, Func<int> getValue, Action<int> setValue, int buttonValue)
        => RadioButton(label, Dux.Bind(getValue, setValue), buttonValue);

    public IUiView InputText(string label, UiBinding<string> value, int maxLength = 256)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value ?? string.Empty;
            if (ui.InputText(label, ref current, maxLength))
            {
                value.Value = current;
            }
        });

    public IUiView InputText(string label, Func<string> getValue, Action<string> setValue, int maxLength = 256)
        => InputText(label, Dux.Bind(getValue, setValue), maxLength);

    public IUiView TextField(string id, UiBinding<string> value, int maxLength = 256)
        => InputText(HiddenControlLabel(id), value, maxLength);

    public IUiView TextField(string id, Func<string> getValue, Action<string> setValue, int maxLength = 256)
        => TextField(id, Dux.Bind(getValue, setValue), maxLength);

    public IUiView InputTextMultiline(string label, UiBinding<string> value, int maxLength = 4096, float height = 160f)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value ?? string.Empty;
            if (ui.InputTextMultiline(label, ref current, maxLength, height))
            {
                value.Value = current;
            }
        });

    public IUiView InputTextMultiline(string label, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f)
        => InputTextMultiline(label, Dux.Bind(getValue, setValue), maxLength, height);

    public IUiView TextArea(string id, UiBinding<string> value, int maxLength = 4096, float height = 160f)
        => InputTextMultiline(HiddenControlLabel(id), value, maxLength, height);

    public IUiView TextArea(string id, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f)
        => TextArea(id, Dux.Bind(getValue, setValue), maxLength, height);

    public IUiView MultilineTextField(string id, UiBinding<string> value, int maxLength = 4096, float height = 160f)
        => TextArea(id, value, maxLength, height);

    public IUiView MultilineTextField(string id, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f)
        => TextArea(id, getValue, setValue, maxLength, height);

    public IUiView InputInt(string label, UiBinding<int> value)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value;
            if (ui.InputInt(label, ref current))
            {
                value.Value = current;
            }
        });

    public IUiView InputInt(string label, Func<int> getValue, Action<int> setValue)
        => InputInt(label, Dux.Bind(getValue, setValue));

    public IUiView NumberField(string id, UiBinding<int> value)
        => InputInt(HiddenControlLabel(id), value);

    public IUiView NumberField(string id, Func<int> getValue, Action<int> setValue)
        => NumberField(id, Dux.Bind(getValue, setValue));

    public IUiView IntField(string id, UiBinding<int> value)
        => NumberField(id, value);

    public IUiView IntField(string id, Func<int> getValue, Action<int> setValue)
        => NumberField(id, getValue, setValue);

    public IUiView SliderFloat(string label, UiBinding<float> value, float min, float max)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value;
            if (ui.SliderFloat(label, ref current, min, max))
            {
                value.Value = current;
            }
        });

    public IUiView SliderFloat(string label, Func<float> getValue, Action<float> setValue, float min, float max)
        => SliderFloat(label, Dux.Bind(getValue, setValue), min, max);

    public IUiView Slider(string id, UiBinding<float> value, float min, float max)
        => SliderFloat(HiddenControlLabel(id), value, min, max);

    public IUiView Slider(string id, Func<float> getValue, Action<float> setValue, float min, float max)
        => Slider(id, Dux.Bind(getValue, setValue), min, max);

    public IUiView SliderInt(string label, UiBinding<int> value, int min, int max)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(value);
            var current = value.Value;
            if (ui.SliderInt(label, ref current, min, max))
            {
                value.Value = current;
            }
        });

    public IUiView SliderInt(string label, Func<int> getValue, Action<int> setValue, int min, int max)
        => SliderInt(label, Dux.Bind(getValue, setValue), min, max);

    public IUiView Slider(string id, UiBinding<int> value, int min, int max)
        => SliderInt(HiddenControlLabel(id), value, min, max);

    public IUiView Slider(string id, Func<int> getValue, Action<int> setValue, int min, int max)
        => Slider(id, Dux.Bind(getValue, setValue), min, max);

    public IUiView ProgressBar(Func<float> fraction, UiVector2 size = default, Func<string?>? overlay = null)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(fraction);
            ui.ProgressBar(fraction(), size, overlay?.Invoke());
        });

    private static string HiddenControlLabel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Contains("##", StringComparison.Ordinal))
        {
            throw new ArgumentException("Declarative control ids must not contain '##'.", nameof(id));
        }

        return $"##{id}";
    }

    public IUiView Combo(string label, UiBinding<int> selectedIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(selectedIndex);
            ArgumentNullException.ThrowIfNull(items);
            var current = selectedIndex.Value;
            if (ui.Combo(ref current, items, popupMaxHeightInItems, label))
            {
                selectedIndex.Value = current;
            }
        });

    public IUiView Combo(string label, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, int popupMaxHeightInItems = 8)
        => Combo(label, Dux.Bind(getValue, setValue), items, popupMaxHeightInItems);

    public IUiView Segmented(string id, UiBinding<int> selectedIndex, IReadOnlyList<string> items, UiSegmentedOptions options = default)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(selectedIndex);
            ArgumentNullException.ThrowIfNull(items);
            if (items.Count is 0)
            {
                return;
            }

            var current = selectedIndex.Value;
            RenderSegmented(
                ui,
                id,
                items,
                options,
                index => index == current,
                index =>
                {
                    selectedIndex.Value = index;
                    current = index;
                });
        });

    public IUiView Segmented(string id, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, UiSegmentedOptions options = default)
        => Segmented(id, Dux.Bind(getValue, setValue), items, options);

    public UiChoice<T> Choice<T>(T value, string label) => new(value, label);

    public IUiView Segmented<T>(string id, UiBinding<T> selectedValue, IReadOnlyList<UiChoice<T>> choices, UiSegmentedOptions options = default)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(selectedValue);
            ArgumentNullException.ThrowIfNull(choices);
            if (choices.Count is 0)
            {
                return;
            }

            var labels = new string[choices.Count];
            for (var i = 0; i < choices.Count; i++)
            {
                labels[i] = choices[i].Label;
            }

            var current = selectedValue.Value;
            RenderSegmented(
                ui,
                id,
                labels,
                options,
                index => EqualityComparer<T>.Default.Equals(current, choices[index].Value),
                index =>
                {
                    selectedValue.Value = choices[index].Value;
                    current = choices[index].Value;
                });
        });

    public IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, IReadOnlyList<UiChoice<T>> choices, UiSegmentedOptions options = default)
        => Segmented(id, Dux.Bind(getValue, setValue), choices, options);

    public IUiView Segmented<T>(string id, UiBinding<T> selectedValue, params UiChoice<T>[] choices)
        => Segmented(id, selectedValue, (IReadOnlyList<UiChoice<T>>)choices);

    public IUiView Segmented<T>(string id, UiBinding<T> selectedValue, UiSegmentedOptions options, params UiChoice<T>[] choices)
        => Segmented(id, selectedValue, (IReadOnlyList<UiChoice<T>>)choices, options);

    public IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, params UiChoice<T>[] choices)
        => Segmented(id, Dux.Bind(getValue, setValue), (IReadOnlyList<UiChoice<T>>)choices);

    public IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, UiSegmentedOptions options, params UiChoice<T>[] choices)
        => Segmented(id, Dux.Bind(getValue, setValue), (IReadOnlyList<UiChoice<T>>)choices, options);

    public IUiView EnumSegmented<TEnum>(string id, UiBinding<TEnum> selectedValue, UiSegmentedOptions options = default)
        where TEnum : struct, Enum
        => Segmented(id, selectedValue, CreateEnumChoices<TEnum>(static value => value.ToString()), options);

    public IUiView EnumSegmented<TEnum>(string id, Func<TEnum> getValue, Action<TEnum> setValue, UiSegmentedOptions options = default)
        where TEnum : struct, Enum
        => EnumSegmented(id, Dux.Bind(getValue, setValue), options);

    public IUiView EnumSegmented<TEnum>(string id, UiBinding<TEnum> selectedValue, Func<TEnum, string> label, UiSegmentedOptions options = default)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(label);
        return Segmented(id, selectedValue, CreateEnumChoices(label), options);
    }

    public IUiView EnumSegmented<TEnum>(string id, Func<TEnum> getValue, Action<TEnum> setValue, Func<TEnum, string> label, UiSegmentedOptions options = default)
        where TEnum : struct, Enum
        => EnumSegmented(id, Dux.Bind(getValue, setValue), label, options);

    public IUiView ListBox(string label, UiBinding<int> selectedIndex, IReadOnlyList<string> items, int visibleItems = 6)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(selectedIndex);
            ArgumentNullException.ThrowIfNull(items);
            var current = selectedIndex.Value;
            if (ui.ListBox(ref current, items, visibleItems, label))
            {
                selectedIndex.Value = current;
            }
        });

    public IUiView ListBox(string label, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, int visibleItems = 6)
        => ListBox(label, Dux.Bind(getValue, setValue), items, visibleItems);

    public IUiView Selectable(string label, UiBinding<bool> selected, UiVector2 size = default)
        => new UiView(ui =>
        {
            ArgumentNullException.ThrowIfNull(selected);
            var current = selected.Value;
            if (size == default ? ui.Selectable(label, ref current) : ui.Selectable(label, ref current, size))
            {
                selected.Value = current;
            }
        });

    public IUiView Selectable(string label, Func<bool> getValue, Action<bool> setValue, UiVector2 size = default)
        => Selectable(label, Dux.Bind(getValue, setValue), size);

    private static UiChoice<TEnum>[] CreateEnumChoices<TEnum>(Func<TEnum, string> label)
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var choices = new UiChoice<TEnum>[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var displayLabel = label(values[i])
                ?? throw new InvalidOperationException("Enum segmented label provider returned null.");
            choices[i] = new UiChoice<TEnum>(values[i], displayLabel);
        }

        return choices;
    }

    private static void RenderSegmented(
        UiImmediateContext ui,
        string id,
        IReadOnlyList<string> labels,
        UiSegmentedOptions options,
        Func<int, bool> isSelected,
        Action<int> select)
    {
        var effectiveOptions = options == default
            ? new UiSegmentedOptions()
            : options;
        var spacing = MathF.Max(0f, effectiveOptions.Spacing);
        var itemSize = ResolveSegmentedItemSize(ui, labels, effectiveOptions, spacing);
        var start = ui.GetCursorScreenPos();
        var totalWidth = (itemSize.X * labels.Count) + (spacing * MathF.Max(0, labels.Count - 1));
        var height = itemSize.Y > 0f ? itemSize.Y : ui.GetFrameHeight();
        var radius = MathF.Min(
            MathF.Max(0f, ui.GetDesignToken(UiDesignToken.ControlCornerRadius)),
            MathF.Min(totalWidth, height) * 0.5f);
        var drawList = ui.GetWindowDrawList();
        var groupRect = new UiRect(start.X, start.Y, totalWidth, height);
        var frameColor = ui.GetColorU32(UiStyleColor.Button);
        var borderColor = ui.GetColorU32(UiStyleColor.ButtonBorder);
        drawList.AddRectFilledRounded(groupRect, frameColor, ui.WhiteTextureId, radius, groupRect);

        ui.BeginRow();
        try
        {
            for (var i = 0; i < labels.Count; i++)
            {
                var label = labels[i] ?? throw new InvalidOperationException("Segmented label cannot be null.");
                var selected = isSelected(i);
                var x = start.X + ((itemSize.X + spacing) * i);
                ui.SetCursorScreenPos(new UiVector2(x, start.Y));
                var pressed = ui.InvisibleButton($"{label}##{id}:{i}", new UiVector2(itemSize.X, height));
                var hovered = ui.IsItemHovered();
                var active = ui.IsItemActive();
                var rect = new UiRect(x, start.Y, itemSize.X, height);
                if (selected || hovered || active)
                {
                    var segmentColor = ResolveSegmentColor(ui, selected, hovered, active);
                    FillSegment(ui, drawList, rect, segmentColor, radius, i, labels.Count);
                }

                ui.DrawTextAligned(
                    rect,
                    label,
                    ui.GetColorU32(UiStyleColor.ButtonText),
                    UiItemHorizontalAlign.Center,
                    UiItemVerticalAlign.Center,
                    clipToContainer: true);

                if (pressed)
                {
                    select(i);
                }
            }
        }
        finally
        {
            ui.EndRow();
        }

        if (spacing <= 0f)
        {
            for (var i = 1; i < labels.Count; i++)
            {
                var x = start.X + (itemSize.X * i);
                var divider = new UiRect(x, groupRect.Y + 1f, 1f, MathF.Max(0f, groupRect.Height - 2f));
                drawList.AddRectFilled(divider, borderColor, ui.WhiteTextureId, groupRect);
            }
        }

        drawList.AddRect(groupRect, borderColor, radius, 1f);
        ui.SetCursorScreenPos(new UiVector2(start.X, start.Y + height + ui.GetItemSpacing().Y));
    }

    private static UiVector2 ResolveSegmentedItemSize(UiImmediateContext ui, IReadOnlyList<string> labels, UiSegmentedOptions options, float spacing)
    {
        var itemCount = labels.Count;
        var size = options.ItemSize;
        if (size.X <= 0f)
        {
            var measuredWidth = 1f;
            for (var i = 0; i < labels.Count; i++)
            {
                measuredWidth = MathF.Max(measuredWidth, ui.CalcTextSize(labels[i] ?? string.Empty).X + 24f);
            }

            size = size with { X = measuredWidth };
        }

        if (size.Y <= 0f)
        {
            size = size with { Y = ui.GetFrameHeight() };
        }

        if (!options.FillWidth)
        {
            return size;
        }

        var available = ui.GetContentRegionAvail().X;
        var width = MathF.Max(1f, (available - (spacing * MathF.Max(0, itemCount - 1))) / MathF.Max(1, itemCount));
        return new UiVector2(width, size.Y);
    }

    private static UiColor ResolveSegmentColor(UiImmediateContext ui, bool selected, bool hovered, bool active)
    {
        var accent = ui.GetColorU32(UiStyleColor.CheckMark);
        if (active)
        {
            return selected ? WithAlpha(accent, 130) : ui.GetColorU32(UiStyleColor.ButtonActive);
        }

        if (hovered)
        {
            return selected ? WithAlpha(accent, 98) : ui.GetColorU32(UiStyleColor.ButtonHovered);
        }

        return selected ? WithAlpha(accent, 82) : ui.GetColorU32(UiStyleColor.Button);
    }

    private static void FillSegment(
        UiImmediateContext ui,
        UiDrawListBuilder drawList,
        UiRect rect,
        UiColor color,
        float radius,
        int index,
        int count)
    {
        _ = ui;
        _ = radius;
        _ = index;
        _ = count;
        var leftInset = 1f;
        var rightInset = 1f;
        var fillRect = new UiRect(
            rect.X + leftInset,
            rect.Y + 1f,
            MathF.Max(0f, rect.Width - leftInset - rightInset),
            MathF.Max(0f, rect.Height - 2f));

        drawList.AddRectFilled(fillRect, color, ui.WhiteTextureId, rect);
    }

    private static UiColor WithAlpha(UiColor color, byte alpha)
        => new((color.Rgba & 0x00FFFFFFu) | ((uint)alpha << 24));
}

public sealed class DuxelMenuFactory
{
    internal DuxelMenuFactory() { }

    public IUiView MainMenuBar(params IUiView[] menus)
        => new UiView(ui =>
        {
            if (!ui.BeginMainMenuBar())
            {
                return;
            }

            try
            {
                DuxelView.RenderChildren(ui, menus);
            }
            finally
            {
                ui.EndMainMenuBar();
            }
        });

    public IUiView Menu(string label, params IUiView[] items)
        => new UiView(ui =>
        {
            if (!ui.BeginMenu(label))
            {
                return;
            }

            try
            {
                DuxelView.RenderChildren(ui, items);
            }
            finally
            {
                ui.EndMenu();
            }
        });

    public IUiView MenuItem(string label, Action? clicked = null, Func<bool>? selected = null, Func<bool>? enabled = null)
        => new UiView(ui =>
        {
            var isSelected = selected?.Invoke() ?? false;
            var isEnabled = enabled?.Invoke() ?? true;
            if (ui.MenuItem(label, isSelected, isEnabled))
            {
                clicked?.Invoke();
            }
        });
}

public sealed class DuxelTextFactory
{
    internal DuxelTextFactory() { }

    public IUiView Block(string text) => new UiView(ui => ui.Text(text));

    public IUiView Block(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UiView(ui => ui.Text(text()));
    }

    public IUiView Wrapped(string text) => new UiView(ui => ui.TextWrapped(text));

    public IUiView Wrapped(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UiView(ui => ui.TextWrapped(text()));
    }

    public IUiView Colored(UiColor color, string text) => new UiView(ui => ui.TextColored(color, text));

    public IUiView Colored(UiColor color, Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UiView(ui => ui.TextColored(color, text()));
    }

    public IUiView Heading(string text, float fontSize = 24f)
        => Block(text).FontSize(fontSize);

    public IUiView Heading(Func<string> text, float fontSize = 24f)
        => Block(text).FontSize(fontSize);
}

public sealed class DuxelDisplayFactory
{
    internal DuxelDisplayFactory() { }

    public IUiView Surface(string id, IUiView content, UiSurfaceOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default
                ? new UiSurfaceOptions()
                : options;
            var size = ResolveSurfaceSize(ui, effectiveOptions.Size);
            var position = ui.GetCursorScreenPos();
            var rect = new UiRect(position.X, position.Y, size.X, size.Y);
            var background = effectiveOptions.Background ?? ui.GetColorU32(UiStyleColor.FrameBg);
            var border = effectiveOptions.Border ?? ui.GetColorU32(UiStyleColor.Border);
            var radius = MathF.Max(0f, effectiveOptions.Radius);
            var drawList = ui.GetWindowDrawList();

            drawList.AddRectFilledRounded(
                rect,
                background,
                border,
                ui.WhiteTextureId,
                radius,
                border.Rgba is 0 ? 0f : 1f,
                rect);

            var opened = ui.BeginChild(id, size, border: false);
            try
            {
                if (!opened)
                {
                    return;
                }

                var padding = effectiveOptions.Padding == default
                    ? new UiEdgeInsets(12f)
                    : effectiveOptions.Padding;
                var contentStart = ui.GetCursorScreenPos();
                var paddedRect = new UiRect(
                    contentStart.X + padding.Left,
                    contentStart.Y + padding.Top,
                    MathF.Max(0f, size.X - 4f - padding.Left - padding.Right),
                    MathF.Max(0f, size.Y - 4f - padding.Top - padding.Bottom));

                if (effectiveOptions.Clip)
                {
                    ui.PushClipRect(paddedRect, intersect: true);
                }

                try
                {
                    ui.SetCursorScreenPos(new UiVector2(paddedRect.X, paddedRect.Y));
                    content.Render(ui);
                    if (padding.Bottom > 0f)
                    {
                        ui.Dummy(new UiVector2(1f, padding.Bottom));
                    }
                }
                finally
                {
                    if (effectiveOptions.Clip)
                    {
                        ui.PopClipRect();
                    }
                }
            }
            finally
            {
                ui.EndChild();
            }
        });
    }

    public IUiView Card(string id, IUiView content, UiVector2 size = default)
        => Surface(id, content, new UiSurfaceOptions(Size: size));

    public IUiView Badge(string text, UiBadgeOptions options = default)
        => Badge(() => text, options);

    public IUiView Badge(Func<string> text, UiBadgeOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UiView(ui =>
        {
            var label = text() ?? string.Empty;
            float? fontSize = options.FontSize > 0f ? options.FontSize : null;
            var textSize = ui.CalcTextSize(label, fontSize);
            var padding = options.Padding == default ? new UiVector2(10f, 4f) : options.Padding;
            var height = MathF.Max(textSize.Y + (padding.Y * 2f), ui.GetTextLineHeight() + 6f);
            var width = MathF.Max(1f, textSize.X + (padding.X * 2f));
            var position = ui.GetCursorScreenPos();
            var rect = new UiRect(position.X, position.Y, width, height);
            var palette = ResolveBadgePalette(ui, options);
            var radius = options.Radius <= 0f ? height * 0.5f : MathF.Min(options.Radius, height * 0.5f);
            var drawList = ui.GetWindowDrawList();

            drawList.AddRectFilledRounded(
                rect,
                palette.Background,
                palette.Border,
                ui.WhiteTextureId,
                radius,
                palette.Border.Rgba is 0 ? 0f : 1f,
                rect);

            ui.DrawTextAligned(
                rect,
                label,
                palette.Foreground,
                UiItemHorizontalAlign.Center,
                UiItemVerticalAlign.Center,
                fontSize,
                clipToContainer: true);
            ui.Dummy(new UiVector2(width, height));
        });
    }

    public IUiView Metric(string label, string value, UiMetricOptions options = default)
        => Metric(label, () => value, options);

    public IUiView Metric(string label, Func<string> value, UiMetricOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default
                ? new UiMetricOptions()
                : options;
            var valueColor = effectiveOptions.ValueColor ?? ui.GetColorU32(UiStyleColor.Text);
            var labelColor = effectiveOptions.LabelColor ?? ui.GetColorU32(UiStyleColor.TextDisabled);

            ui.PushFontSize(effectiveOptions.ValueFontSize);
            try
            {
                ui.TextColored(valueColor, value() ?? string.Empty);
            }
            finally
            {
                ui.PopFontSize();
            }

            ui.SetCursorPosY(ui.GetCursorPosY() + MathF.Max(0f, effectiveOptions.Spacing));
            ui.PushFontSize(effectiveOptions.LabelFontSize);
            try
            {
                ui.TextColored(labelColor, label);
            }
            finally
            {
                ui.PopFontSize();
            }
        });
    }

    public IUiView MetricCard(string label, string value, string? detail = null, UiMetricCardOptions options = default)
        => MetricCard(label, () => value, detail is null ? null : () => detail, options);

    public IUiView MetricCard(string label, Func<string> value, string? detail = null, UiMetricCardOptions options = default)
        => MetricCard(label, value, detail is null ? null : () => detail, options);

    public IUiView MetricCard(string label, Func<string> value, Func<string>? detail, UiMetricCardOptions options = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        var effectiveOptions = options == default ? new UiMetricCardOptions() : options;
        var card = new UiView(ui =>
        {
            var labelSize = effectiveOptions.LabelFontSize > 0f ? effectiveOptions.LabelFontSize : 12f;
            var valueSize = effectiveOptions.ValueFontSize > 0f ? effectiveOptions.ValueFontSize : 26f;
            var spacing = MathF.Max(0f, effectiveOptions.Spacing);
            Dux.VStack(
                spacing,
                Dux.Caption(label, labelSize).Muted(),
                Dux.Text(value).TextStyle(new UiTextStyle(FontSize: valueSize, Tone: effectiveOptions.ValueTone)),
                BuildMetricCardDetail(detail, effectiveOptions))
                .Render(ui);
        });

        return card.Panel(new UiPanelStyle(
            Padding: new UiEdgeInsets(MathF.Max(0f, effectiveOptions.Padding)),
            Height: MathF.Max(1f, effectiveOptions.Height)));
    }

    public UiPropertyItem Property(string label, string value, UiTextTone valueTone = UiTextTone.Normal)
        => Property(label, () => value, valueTone);

    public UiPropertyItem Property(string label, Func<string> value, UiTextTone valueTone = UiTextTone.Normal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        return new UiPropertyItem(label, value, valueTone);
    }

    public IUiView PropertyList(params UiPropertyItem[] items)
        => PropertyList(new UiPropertyListOptions(), items);

    public IUiView PropertyList(UiPropertyListOptions options, params UiPropertyItem[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default ? new UiPropertyListOptions() : options;
            ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(6f, MathF.Max(0f, effectiveOptions.Spacing)));
            ui.Columns(2);
            try
            {
                ui.SetColumnWidth(0, MathF.Max(1f, effectiveOptions.LabelWidth));
                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.Value is null)
                    {
                        continue;
                    }

                    Dux.Caption(item.Label, 12f).Render(ui);
                    ui.NextColumn();
                    Dux.Text(item.Value()).Tone(item.ValueTone).Render(ui);
                    ui.NextColumn();
                }
            }
            finally
            {
                ui.Columns(1);
                ui.PopStyleVar();
            }
        });
    }

    public UiSettingItem Setting(string label, IUiView content)
        => Setting(label, content, (Func<string>?)null);

    public UiSettingItem Setting(string label, IUiView content, string description)
        => Setting(label, content, () => description);

    public UiSettingItem Setting(string label, IUiView content, Func<string>? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(content);
        return new UiSettingItem(label, content, description);
    }

    public IUiView SettingsGroup(params UiSettingItem[] items)
        => SettingsGroup(new UiSettingsGroupOptions(), items);

    public IUiView SettingsGroup(UiSettingsGroupOptions options, params UiSettingItem[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var effectiveOptions = options == default ? new UiSettingsGroupOptions() : options;
        var content = new UiView(ui =>
        {
            var availableWidth = MathF.Max(1f, ui.GetContentRegionAvail().X);
            var labelWidth = MathF.Min(
                MathF.Max(1f, effectiveOptions.LabelWidth),
                MathF.Max(72f, availableWidth * 0.42f));
            var columnGap = 12f;
            var rowSpacing = MathF.Max(0f, effectiveOptions.Spacing);
            var descriptionSpacing = 4f;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];

                var rowStart = ui.GetCursorScreenPos();
                ui.SetCursorScreenPos(new UiVector2(rowStart.X + labelWidth + columnGap, rowStart.Y));
                item.Content.Render(ui);
                var contentMax = ui.GetItemRectMax();

                var controlHeight = MathF.Max(ui.GetFrameHeight(), contentMax.Y - rowStart.Y);
                ui.DrawTextAligned(
                    new UiRect(rowStart.X, rowStart.Y, labelWidth, controlHeight),
                    item.Label,
                    ui.GetColorU32(UiStyleColor.Text),
                    UiItemHorizontalAlign.Left,
                    UiItemVerticalAlign.Center,
                    clipToContainer: true);

                var rowBottom = rowStart.Y + controlHeight;
                ui.SetCursorScreenPos(new UiVector2(rowStart.X, rowBottom + descriptionSpacing));
                BuildSettingDescription(item.Description).Render(ui);
                var descriptionMax = ui.GetItemRectMax();

                ui.SetCursorScreenPos(new UiVector2(rowStart.X, MathF.Max(rowBottom, descriptionMax.Y) + rowSpacing));
            }
        });

        return effectiveOptions.Panel
            ? content.Panel(new UiPanelStyle(
                Padding: new UiEdgeInsets(MathF.Max(0f, effectiveOptions.Padding)),
                Height: MathF.Max(0f, effectiveOptions.Height)))
            : content;
    }

    public IUiView StatusRow(
        string title,
        string meta,
        Func<float> progress,
        UiBinding<bool>? selected = null,
        UiStatusRowOptions options = default)
        => StatusRow(() => title, () => meta, progress, selected, options);

    public IUiView StatusRow(
        Func<string> title,
        Func<string> meta,
        Func<float> progress,
        UiBinding<bool>? selected = null,
        UiStatusRowOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(progress);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default ? new UiStatusRowOptions() : options;
            Dux.HStack(
                MathF.Max(0f, effectiveOptions.Spacing),
                BuildStatusRowTitle(title, selected, effectiveOptions),
                Dux.Text(meta).Muted().ItemWidth(MathF.Max(1f, effectiveOptions.MetaWidth)),
                Dux.ProgressBar(
                    progress,
                    new UiVector2(MathF.Max(1f, effectiveOptions.ProgressWidth), MathF.Max(1f, effectiveOptions.ProgressHeight)),
                    () => $"{progress():P0}"))
                .Render(ui);
        });
    }

    public UiMetaItem Meta(string text, UiTextTone tone = UiTextTone.Accent)
        => new(() => text, tone);

    public UiMetaItem Meta(Func<string> text, UiTextTone tone = UiTextTone.Accent)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UiMetaItem(text, tone);
    }

    public IUiView MetaBar(params UiMetaItem[] items)
        => MetaBar(8f, items);

    public IUiView MetaBar(float spacing, params UiMetaItem[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new UiView(ui =>
        {
            var gap = MathF.Max(0f, spacing);
            var origin = ui.GetCursorScreenPos();
            var lineHeight = ui.GetTextLineHeight();
            var maxWidth = MathF.Max(1f, ui.GetContentRegionAvail().X);
            var x = origin.X;
            var rendered = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item.Text is null)
                {
                    continue;
                }

                var text = item.Text() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (rendered > 0)
                {
                    x = DrawMetaSegment(ui, "|", UiTextTone.Muted, x, origin.Y, lineHeight, gap, maxWidth);
                }

                x = DrawMetaSegment(ui, text, item.Tone, x, origin.Y, lineHeight, gap, maxWidth);
                rendered++;
            }

            ui.Dummy(new UiVector2(MathF.Max(1f, x - origin.X), lineHeight));
        });
    }

    public IUiView Header(string title, params UiMetaItem[] meta)
        => Header(() => title, meta);

    public IUiView Header(Func<string> title, params UiMetaItem[] meta)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(meta);
        return meta.Length is 0
            ? Dux.Title(title)
            : Dux.VStack(
                4f,
                Dux.Title(title),
                MetaBar(meta));
    }

    public UiShellItem NavItem(string label, IUiView content)
        => NavItem(label, content, (Func<string>?)null);

    public UiShellItem NavItem(string label, IUiView content, string detail, UiTextTone detailTone = UiTextTone.Muted)
        => NavItem(label, content, () => detail, detailTone);

    public UiShellItem NavItem(string label, IUiView content, Func<string>? detail, UiTextTone detailTone = UiTextTone.Muted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(content);
        return new UiShellItem(label, content, detail, detailTone);
    }

    public UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content)
        => NavItem(value, label, content, (Func<string>?)null);

    public UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content, string detail, UiTextTone detailTone = UiTextTone.Muted)
        => NavItem(value, label, content, () => detail, detailTone);

    public UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content, Func<string>? detail, UiTextTone detailTone = UiTextTone.Muted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(content);
        return new UiShellItem<TValue>(value, label, content, detail, detailTone);
    }

    public IUiView AppShell(
        string title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        params UiMetaItem[] meta)
        => AppShell(() => title, selectedIndex, items, new UiAppShellOptions(), meta);

    public IUiView AppShell(
        string title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        UiAppShellOptions options,
        params UiMetaItem[] meta)
        => AppShell(() => title, selectedIndex, items, options, meta);

    public IUiView AppShell(
        Func<string> title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        params UiMetaItem[] meta)
        => AppShell(title, selectedIndex, items, new UiAppShellOptions(), meta);

    public IUiView AppShell(
        Func<string> title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        UiAppShellOptions options,
        params UiMetaItem[] meta)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedIndex);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(meta);

        var effectiveOptions = options == default ? new UiAppShellOptions() : options;
        var initialTitle = title() ?? string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(initialTitle);
        var window = Dux.Window(
            effectiveOptions.WindowTitle ?? initialTitle,
            BuildAppShellContent(title, selectedIndex, items, effectiveOptions, meta),
            new UiWindowOptions(
                Position: effectiveOptions.Position,
                Size: effectiveOptions.Size,
                Open: effectiveOptions.Open,
                Focus: effectiveOptions.Focus,
                TopMost: effectiveOptions.TopMost));

        return effectiveOptions.Menu is null
            ? window
            : Dux.Group(effectiveOptions.Menu, window);
    }

    public IUiView AppShell<TValue>(
        string title,
        UiBinding<TValue> selectedValue,
        IReadOnlyList<UiShellItem<TValue>> items,
        params UiMetaItem[] meta)
        => AppShell(() => title, selectedValue, items, new UiAppShellOptions(), meta);

    public IUiView AppShell<TValue>(
        string title,
        UiBinding<TValue> selectedValue,
        IReadOnlyList<UiShellItem<TValue>> items,
        UiAppShellOptions options,
        params UiMetaItem[] meta)
        => AppShell(() => title, selectedValue, items, options, meta);

    public IUiView AppShell<TValue>(
        Func<string> title,
        UiBinding<TValue> selectedValue,
        IReadOnlyList<UiShellItem<TValue>> items,
        params UiMetaItem[] meta)
        => AppShell(title, selectedValue, items, new UiAppShellOptions(), meta);

    public IUiView AppShell<TValue>(
        Func<string> title,
        UiBinding<TValue> selectedValue,
        IReadOnlyList<UiShellItem<TValue>> items,
        UiAppShellOptions options,
        params UiMetaItem[] meta)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedValue);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(meta);

        return AppShell(
            title,
            Dux.Bind(
                () => ResolveSelectedShellIndex(selectedValue.Value, items),
                index =>
                {
                    if (index >= 0 && index < items.Count)
                    {
                        selectedValue.Value = items[index].Value;
                    }
                }),
            ProjectShellItems(items),
            options,
            meta);
    }

    public IUiView EmptyState(string title, string message, IUiView? action = null, UiEmptyStateOptions options = default)
        => EmptyState(() => title, () => message, action, options);

    public IUiView EmptyState(Func<string> title, Func<string> message, IUiView? action = null, UiEmptyStateOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        var effectiveOptions = options == default ? new UiEmptyStateOptions() : options;
        var content = action is null
            ? Dux.VStack(
                6f,
                Dux.Subtitle(title, 16f),
                Dux.TextWrapped(message).TextStyle(new UiTextStyle(Tone: effectiveOptions.MessageTone, Wrap: true)))
            : Dux.VStack(
                6f,
                Dux.Subtitle(title, 16f),
                Dux.TextWrapped(message).TextStyle(new UiTextStyle(Tone: effectiveOptions.MessageTone, Wrap: true)),
                action);

        return content
            .Panel(height: effectiveOptions.Height, padding: effectiveOptions.Padding);
    }

    public IUiView Callout(string title, string message, IUiView? action = null, UiCalloutOptions options = default)
        => Callout(() => title, () => message, action, options);

    public IUiView Callout(string title, Func<string> message, IUiView? action = null, UiCalloutOptions options = default)
        => Callout(() => title, message, action, options);

    public IUiView Callout(Func<string> title, string message, IUiView? action = null, UiCalloutOptions options = default)
        => Callout(title, () => message, action, options);

    public IUiView Callout(Func<string> title, Func<string> message, IUiView? action = null, UiCalloutOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        var effectiveOptions = options == default ? new UiCalloutOptions() : options;
        var content = action is null
            ? Dux.VStack(
                5f,
                Dux.Text(title).TextStyle(new UiTextStyle(FontSize: effectiveOptions.TitleFontSize, Tone: effectiveOptions.Tone)),
                Dux.TextWrapped(message).TextStyle(new UiTextStyle(FontSize: effectiveOptions.MessageFontSize, Tone: UiTextTone.Normal, Wrap: true)))
            : Dux.VStack(
                7f,
                Dux.Text(title).TextStyle(new UiTextStyle(FontSize: effectiveOptions.TitleFontSize, Tone: effectiveOptions.Tone)),
                Dux.TextWrapped(message).TextStyle(new UiTextStyle(FontSize: effectiveOptions.MessageFontSize, Tone: UiTextTone.Normal, Wrap: true)),
                action);

        return new UiView(ui =>
        {
            var palette = ResolveCalloutPalette(ui, effectiveOptions.Tone);
            var height = MathF.Max(0f, effectiveOptions.Height);
            if (height > 0f)
            {
                RenderFixedCallout(ui, content, palette.Background, palette.Border, height, MathF.Max(0f, effectiveOptions.Padding));
                return;
            }

            content
                .Padding(MathF.Max(0f, effectiveOptions.Padding))
                .Background(palette.Background)
                .Border(palette.Border)
                .CornerRadius(UiDesignToken.ControlCornerRadius)
                .FillFrameWidth()
                .Render(ui);
        });
    }

    public UiCommand Command(
        string label,
        Action? action = null,
        UiButtonRole role = UiButtonRole.Normal,
        Func<bool>? enabled = null,
        Func<string>? tooltip = null,
        UiVector2 size = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new UiCommand(label, action, role, enabled, tooltip, size);
    }

    public IUiView CommandBar(params UiCommand[] commands)
        => CommandBar(new UiCommandBarOptions(), commands);

    public IUiView CommandBar(UiCommandBarOptions options, params UiCommand[] commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return new UiView(ui =>
        {
            var effectiveOptions = options == default ? new UiCommandBarOptions() : options;
            var views = new IUiView[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                views[i] = BuildCommandView(commands[i], effectiveOptions);
            }

            Toolbar(MathF.Max(0f, effectiveOptions.Spacing), views).Render(ui);
        });
    }

    public IUiView Toolbar(params IUiView[] items)
        => Toolbar(8f, items);

    public IUiView Toolbar(float spacing, params IUiView[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return DuxelView.Layout.HStack(spacing, items);
    }

    private static UiVector2 ResolveSurfaceSize(UiImmediateContext ui, UiVector2 size)
    {
        var available = ui.GetContentRegionAvail();
        var width = size.X > 0f ? size.X : MathF.Max(1f, available.X);
        var height = size.Y > 0f ? size.Y : ui.GetFrameHeight() * 6f;
        return new UiVector2(width, height);
    }

    private static (UiColor Background, UiColor Foreground, UiColor Border) ResolveBadgePalette(UiImmediateContext ui, UiBadgeOptions options)
    {
        var (background, foreground, border) = options.Tone switch
        {
            UiBadgeTone.Neutral => (
                ui.GetColorU32(UiStyleColor.FrameBg),
                ui.GetColorU32(UiStyleColor.Text),
                ui.GetColorU32(UiStyleColor.Border)),
            UiBadgeTone.Success => Semantic(35, 134, 54),
            UiBadgeTone.Warning => Semantic(210, 153, 34),
            UiBadgeTone.Danger => Semantic(218, 54, 51),
            _ => Accent(ui),
        };

        return (
            options.Background ?? background,
            options.Foreground ?? foreground,
            options.Border ?? border);
    }

    private static (UiColor Background, UiColor Foreground, UiColor Border) Accent(UiImmediateContext ui)
    {
        var accent = ui.GetColorU32(UiStyleColor.CheckMark);
        return (WithAlpha(accent, 46), accent, WithAlpha(accent, 84));
    }

    private static (UiColor Background, UiColor Border) ResolveCalloutPalette(UiImmediateContext ui, UiTextTone tone)
    {
        var (_, foreground, border) = tone switch
        {
            UiTextTone.Success => Semantic(35, 134, 54),
            UiTextTone.Warning => Semantic(210, 153, 34),
            UiTextTone.Danger => Semantic(218, 54, 51),
            UiTextTone.Muted => (
                ui.GetColorU32(UiStyleColor.FrameBg),
                ui.GetColorU32(UiStyleColor.TextDisabled),
                ui.GetColorU32(UiStyleColor.Border)),
            UiTextTone.Normal => (
                ui.GetColorU32(UiStyleColor.FrameBg),
                ui.GetColorU32(UiStyleColor.Text),
                ui.GetColorU32(UiStyleColor.Border)),
            _ => Accent(ui),
        };

        return (WithAlpha(foreground, 28), border);
    }

    private static void RenderFixedCallout(
        UiImmediateContext ui,
        IUiView content,
        UiColor background,
        UiColor border,
        float height,
        float padding)
    {
        var start = ui.GetCursorScreenPos();
        var spacing = ui.GetItemSpacing();
        var width = MathF.Max(1f, ui.GetContentRegionAvail().X);
        var rect = new UiRect(start.X, start.Y, width, MathF.Max(1f, height));
        var radius = MathF.Min(
            MathF.Max(0f, ui.GetDesignToken(UiDesignToken.ControlCornerRadius)),
            MathF.Min(rect.Width, rect.Height) * 0.5f);
        var drawList = ui.GetWindowDrawList();

        drawList.AddRectFilledRounded(
            rect,
            background,
            border,
            ui.WhiteTextureId,
            radius,
            border.Rgba is 0 ? 0f : 1f,
            rect);

        var paddedRect = new UiRect(
            rect.X + padding,
            rect.Y + padding,
            MathF.Max(0f, rect.Width - (padding * 2f)),
            MathF.Max(0f, rect.Height - (padding * 2f)));

        ui.PushClipRect(paddedRect, intersect: true);
        try
        {
            ui.SetCursorScreenPos(new UiVector2(paddedRect.X, paddedRect.Y));
            content.Render(ui);
        }
        finally
        {
            ui.PopClipRect();
        }

        ui.SetCursorScreenPos(new UiVector2(start.X, rect.Y + rect.Height + spacing.Y));
    }

    private static float DrawMetaSegment(
        UiImmediateContext ui,
        string text,
        UiTextTone tone,
        float x,
        float y,
        float lineHeight,
        float gap,
        float maxWidth)
    {
        var size = ui.CalcTextSize(text);
        var remainingWidth = MathF.Max(0f, maxWidth - (x - ui.GetCursorScreenPos().X));
        if (remainingWidth <= 0f)
        {
            return x;
        }

        var width = MathF.Min(size.X, remainingWidth);
        ui.DrawTextAligned(
            new UiRect(x, y, width, MathF.Max(lineHeight, size.Y)),
            text,
            ResolveDisplayTextTone(ui, tone),
            clipToContainer: true);
        return x + width + gap;
    }

    private static UiColor ResolveDisplayTextTone(UiImmediateContext ui, UiTextTone tone)
        => tone switch
        {
            UiTextTone.Normal => ui.GetColorU32(UiStyleColor.Text),
            UiTextTone.Muted => ui.GetColorU32(UiStyleColor.TextDisabled),
            UiTextTone.Accent => ui.GetColorU32(UiStyleColor.CheckMark),
            UiTextTone.Success => new UiColor(35, 134, 54),
            UiTextTone.Warning => new UiColor(210, 153, 34),
            UiTextTone.Danger => new UiColor(218, 54, 51),
            _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
        };

    private static (UiColor Background, UiColor Foreground, UiColor Border) Semantic(byte r, byte g, byte b)
    {
        var color = new UiColor(r, g, b);
        return (new UiColor(r, g, b, 46), color, new UiColor(r, g, b, 84));
    }

    private static UiColor WithAlpha(UiColor color, byte alpha)
        => new((color.Rgba & 0x00FFFFFFu) | ((uint)alpha << 24));

    private IUiView BuildStatusRowTitle(Func<string> title, UiBinding<bool>? selected, UiStatusRowOptions options)
    {
        var view = selected is null
            ? Dux.Text(title).ItemWidth(MathF.Max(1f, options.TitleWidth))
            : Dux.Selectable(
                title(),
                selected,
                new UiVector2(MathF.Max(1f, options.TitleWidth), MathF.Max(1f, options.RowHeight)));
        return options.Tooltip is null ? view : view.Tooltip(options.Tooltip);
    }

    private static IUiView BuildCommandView(UiCommand command, UiCommandBarOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Label);
        var size = command.Size != default ? command.Size : options.ButtonSize;
        var view = options.Small
            ? Dux.SmallButton(command.Label, command.Action)
            : Dux.Button(command.Label, command.Action, size);

        if (command.Role is not UiButtonRole.Normal)
        {
            view = view.ButtonRole(command.Role);
        }

        if (command.Enabled is { } enabled)
        {
            view = view.Disabled(!enabled());
        }

        return command.Tooltip is null ? view : view.Tooltip(command.Tooltip);
    }

    private static IUiView BuildSettingDescription(Func<string>? description)
        => description is null
            ? Dux.Empty()
            : new UiView(ui =>
            {
                var text = description() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Dux.TextWrapped(text).Caption(12f).Muted().Render(ui);
                }
            });

    private static IUiView BuildMetricCardDetail(Func<string>? detail, UiMetricCardOptions options)
        => detail is null
            ? Dux.Empty()
            : new UiView(ui =>
            {
                var text = detail() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Dux.Caption(text, 12f).Tone(options.DetailTone).Render(ui);
                }
            });

    private static int ResolveSelectedShellIndex<TValue>(TValue selectedValue, IReadOnlyList<UiShellItem<TValue>> items)
    {
        var comparer = EqualityComparer<TValue>.Default;
        for (var i = 0; i < items.Count; i++)
        {
            if (comparer.Equals(items[i].Value, selectedValue))
            {
                return i;
            }
        }

        return 0;
    }

    private static UiShellItem[] ProjectShellItems<TValue>(IReadOnlyList<UiShellItem<TValue>> items)
    {
        var projected = new UiShellItem[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            projected[i] = new UiShellItem(item.Label, item.Content, item.Detail, item.DetailTone);
        }

        return projected;
    }

    private IUiView BuildAppShellContent(
        Func<string> title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        UiAppShellOptions options,
        IReadOnlyList<UiMetaItem> meta)
        => new UiView(ui =>
        {
            var sidebarWidth = MathF.Max(96f, options.SidebarWidth);
            var spacing = MathF.Max(0f, options.Spacing);
            var available = ui.GetContentRegionAvail();
            var availableWidth = MathF.Max(1f, available.X - (MathF.Max(0f, options.Padding) * 2f));
            var bodyWidth = MathF.Max(160f, availableWidth - sidebarWidth - spacing);
            Dux.View(innerUi =>
            {
                var start = innerUi.GetCursorScreenPos();
                var height = MathF.Max(1f, innerUi.GetContentRegionAvail().Y);
                var shellId = title() ?? "AppShell";

                innerUi.SetCursorScreenPos(start);
                Dux.Child($"{shellId}##duxel-appshell-sidebar", BuildAppShellSidebar(selectedIndex, items, options), new UiVector2(sidebarWidth, height))
                    .Render(innerUi);

                innerUi.SetCursorScreenPos(new UiVector2(start.X + sidebarWidth + spacing, start.Y));
                Dux.Child($"{shellId}##duxel-appshell-body", BuildAppShellBody(title, selectedIndex, items, options, meta), new UiVector2(bodyWidth, height))
                    .Render(innerUi);

                innerUi.SetCursorScreenPos(new UiVector2(start.X, start.Y + height));
            })
                .Padding(MathF.Max(0f, options.Padding))
                .Render(ui);
        });

    private IUiView BuildAppShellSidebar(UiBinding<int> selectedIndex, IReadOnlyList<UiShellItem> items, UiAppShellOptions options)
        => new UiView(ui =>
        {
            var sidebarWidth = MathF.Max(96f, options.SidebarWidth);
            var rowHeight = MathF.Max(ui.GetFrameHeight(), options.SidebarRowHeight);
            var rowWidth = MathF.Max(1f, sidebarWidth - 24f);

            Dux.VStack(
                8f,
                Dux.Caption(options.SidebarTitle),
                new UiView(innerUi =>
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        var index = i;
                        Dux.Selectable(
                            item.Label,
                            Dux.Bind(
                                () => selectedIndex.Value == index,
                                value =>
                                {
                                    if (value)
                                    {
                                        selectedIndex.Value = index;
                                    }
                                }),
                            new UiVector2(rowWidth, rowHeight))
                            .Render(innerUi);

                        var detail = item.Detail?.Invoke();
                        if (selectedIndex.Value == index && !string.IsNullOrWhiteSpace(detail))
                        {
                            Dux.Caption(detail, 12f)
                                .Tone(item.DetailTone)
                                .Padding(10f, 0f)
                                .Render(innerUi);
                        }
                    }
                }),
                options.Footer ?? Dux.Empty())
                .Panel(new UiPanelStyle(
                    Padding: new UiEdgeInsets(12f),
                    Height: 0f,
                    FillWidth: false))
                .Width(sidebarWidth)
                .Render(ui);
        });

    private IUiView BuildAppShellBody(
        Func<string> title,
        UiBinding<int> selectedIndex,
        IReadOnlyList<UiShellItem> items,
        UiAppShellOptions options,
        IReadOnlyList<UiMetaItem> meta)
        => new UiView(ui =>
        {
            if (items.Count is 0)
            {
                Dux.EmptyState(
                    title,
                    () => "No navigation items were provided.",
                    options: new UiEmptyStateOptions(Height: 140f))
                    .FillFrameWidth()
                    .Render(ui);
                return;
            }

            var index = Math.Clamp(selectedIndex.Value, 0, items.Count - 1);
            if (selectedIndex.Value != index)
            {
                selectedIndex.Value = index;
            }

            var item = items[index];
            Dux.VStack(
                MathF.Max(0f, options.Spacing),
                Header(title, [.. meta]),
                options.Commands ?? Dux.Empty(),
                item.Content)
                .FillFrameWidth()
                .Render(ui);
        });
}

public sealed class DuxelWindowFactory
{
    internal DuxelWindowFactory() { }

    public IUiView Window(string title, IUiView content, UiWindowOptions options = default)
        => new UiView(ui =>
        {
            if (options.Open is { Value: false })
            {
                ui.SetWindowOpen(title, false);
                return;
            }

            if (options.Position != default)
            {
                ui.SetNextWindowPos(options.Position);
            }

            if (options.Size != default)
            {
                ui.SetNextWindowSize(options.Size);
            }

            if (options.Open is not null)
            {
                ui.SetNextWindowOpen(options.Open.Value);
            }

            if (options.Focus)
            {
                ui.SetNextWindowFocus();
            }

            if (options.TopMost)
            {
                ui.SetNextWindowTopMost();
            }

            ui.BeginWindow(title);
            try
            {
                content.Render(ui);
            }
            finally
            {
                ui.EndWindow();
            }

            if (options.Open is not null)
            {
                options.Open.Value = ui.GetWindowOpen(title);
            }
        });
}

internal sealed class UiPaddedView : IUiView
{
    private readonly IUiView _content;
    private readonly UiEdgeInsets _padding;

    public UiPaddedView(IUiView content, UiEdgeInsets padding)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _padding = NormalizePadding(padding);
    }

    public IUiView Content => _content;

    public UiEdgeInsets Padding => _padding;

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var left = _padding.Left;
        var top = _padding.Top;
        var bottom = _padding.Bottom;
        if (top > 0f)
        {
            ui.Dummy(new UiVector2(1f, top));
        }

        if (left > 0f)
        {
            ui.Indent(left);
        }

        try
        {
            _content.Render(ui);
        }
        finally
        {
            if (left > 0f)
            {
                ui.Unindent(left);
            }
        }

        if (bottom > 0f)
        {
            ui.Dummy(new UiVector2(1f, bottom));
        }
    }

    public static UiEdgeInsets NormalizePadding(UiEdgeInsets padding)
        => new(
            MathF.Max(0f, padding.Left),
            MathF.Max(0f, padding.Top),
            MathF.Max(0f, padding.Right),
            MathF.Max(0f, padding.Bottom));

    public static UiEdgeInsets Add(UiEdgeInsets left, UiEdgeInsets right)
    {
        left = NormalizePadding(left);
        right = NormalizePadding(right);
        return new UiEdgeInsets(
            left.Left + right.Left,
            left.Top + right.Top,
            left.Right + right.Right,
            left.Bottom + right.Bottom);
    }
}

internal sealed class UiDecoratedView : IUiView
{
    private readonly IUiView _content;
    private readonly UiDecorationOptions _options;
    private readonly string _id;

    public UiDecoratedView(IUiView content, UiDecorationOptions options, string id)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _options = options;
        _id = string.IsNullOrWhiteSpace(id) ? "duxel-decorated-view" : id;
    }

    public UiDecorationOptions Options => _options;

    public IUiView Content => _content;

    public string Id => _id;

    public UiDecoratedView With(UiDecorationOptions options)
        => new(_content, options, _id);

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (_options.Size.Y > 0f)
        {
            RenderFixed(ui);
            return;
        }

        RenderMeasured(ui);
    }

    private void RenderFixed(UiImmediateContext ui)
    {
        var size = ResolveFixedSize(ui);
        var position = ui.GetCursorScreenPos();
        var rect = new UiRect(position.X, position.Y, size.X, size.Y);
        DrawDecoration(ui, rect, includeBackground: true);

        var opened = ui.BeginChild(_id, size, border: false);
        try
        {
            if (opened)
            {
                ApplyPadding(ui);
                _content.Render(ui);
            }
        }
        finally
        {
            ui.EndChild();
        }
    }

    private void RenderMeasured(UiImmediateContext ui)
    {
        var drawList = ui.GetWindowDrawList();
        var canSplitChannels = !drawList.HasChannels;
        if (canSplitChannels)
        {
            drawList.Split(2);
            drawList.SetCurrentChannel(1);
        }

        var start = ui.GetCursorScreenPos();
        var startAvailable = ui.GetContentRegionAvail();
        var padding = Padding;
        var contentStart = new UiVector2(start.X + padding.Left, start.Y + padding.Top);
        if (padding.Left > 0f || padding.Top > 0f)
        {
            ui.SetCursorScreenPos(contentStart);
        }

        ui.BeginGroup();
        try
        {
            _content.Render(ui);
        }
        finally
        {
            ui.EndGroup();
        }

        var end = ui.GetCursorScreenPos();
        var lastMax = ui.GetItemRectMax();
        var spacing = ui.GetItemSpacing();
        var width = ResolveAutoWidth(startAvailable.X, MathF.Max(0f, lastMax.X - contentStart.X) + padding.Left + padding.Right);
        var contentHeight = MathF.Max(0f, lastMax.Y - contentStart.Y);
        var measuredHeight = MathF.Max(contentHeight, MathF.Max(0f, end.Y - contentStart.Y - spacing.Y)) + padding.Top + padding.Bottom;
        var height = _options.Size.Y > 0f ? _options.Size.Y : measuredHeight;
        var rect = new UiRect(start.X, start.Y, MathF.Max(1f, width), MathF.Max(1f, height));

        if (canSplitChannels)
        {
            drawList.SetCurrentChannel(0);
        }

        DrawDecoration(ui, rect, includeBackground: canSplitChannels);

        if (canSplitChannels)
        {
            drawList.SetCurrentChannel(1);
            drawList.Merge();
        }

        var desiredY = start.Y + rect.Height + spacing.Y;
        ui.SetCursorScreenPos(new UiVector2(start.X, MathF.Max(end.Y, desiredY)));
    }

    private UiVector2 ResolveFixedSize(UiImmediateContext ui)
    {
        var available = ui.GetContentRegionAvail();
        var width = ResolveAutoWidth(available.X, ui.GetFrameHeight());
        var height = MathF.Max(1f, _options.Size.Y);
        return new UiVector2(width, height);
    }

    private float ResolveAutoWidth(float availableWidth, float measuredWidth)
    {
        if (_options.Size.X > 0f)
        {
            return _options.Size.X;
        }

        if (_options.FillWidth)
        {
            return MathF.Max(1f, availableWidth);
        }

        return MathF.Max(1f, measuredWidth);
    }

    private void DrawDecoration(UiImmediateContext ui, UiRect rect, bool includeBackground)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var radius = MathF.Min(MathF.Max(0f, ResolveDecorationRadius(ui)), MathF.Min(rect.Width, rect.Height) * 0.5f);
        var drawList = ui.GetWindowDrawList();
        var background = ResolveDecorationColor(ui, _options.Background, _options.BackgroundStyle);
        var fillColor = includeBackground && background is { Rgba: not 0 } resolvedBackground
            ? resolvedBackground
            : default;
        var border = ResolveDecorationColor(ui, _options.Border, _options.BorderStyle);
        var borderColor = border is { Rgba: not 0 } resolvedBorder && _options.BorderThickness > 0f
            ? resolvedBorder
            : default;
        var borderThickness = borderColor.Rgba is 0
            ? 0f
            : MathF.Max(0.5f, _options.BorderThickness);
        if (fillColor.Rgba is not 0 || borderThickness > 0f)
        {
            drawList.AddRectFilledRounded(
                rect,
                fillColor,
                borderColor,
                ui.WhiteTextureId,
                radius,
                borderThickness,
                rect);
        }
    }

    private static UiColor? ResolveDecorationColor(UiImmediateContext ui, UiColor? color, UiStyleColor? styleColor)
        => color ?? (styleColor is { } slot ? ui.GetColorU32(slot) : null);

    private float ResolveDecorationRadius(UiImmediateContext ui)
        => _options.RadiusToken is { } token ? ui.GetDesignToken(token) : _options.Radius;

    private UiEdgeInsets Padding => UiPaddedView.NormalizePadding(_options.Padding);

    private void ApplyPadding(UiImmediateContext ui)
    {
        var padding = Padding;
        if (padding.Left <= 0f && padding.Top <= 0f)
        {
            return;
        }

        var cursor = ui.GetCursorScreenPos();
        ui.SetCursorScreenPos(new UiVector2(cursor.X + padding.Left, cursor.Y + padding.Top));
    }
}

public static class DuxelViewExtensions
{
    public static IUiView Modifier(this IUiView view, UiModifier modifier) => modifier.Apply(view);

    public static IUiView Style<TStyle>(this IUiView view)
        where TStyle : struct, IUiViewStyle
    {
        ArgumentNullException.ThrowIfNull(view);
        return default(TStyle).Apply(view);
    }

    public static IUiView Style<TStyle>(this IUiView view, TStyle style)
        where TStyle : IUiViewStyle
    {
        ArgumentNullException.ThrowIfNull(view);
        if (style is null)
        {
            throw new ArgumentNullException(nameof(style));
        }

        return style.Apply(view);
    }

    public static IUiView Panel(this IUiView view, float height = 0f, float padding = 14f)
        => view.Panel(new UiPanelStyle(Padding: new UiEdgeInsets(padding), Height: height));

    public static IUiView Panel(this IUiView view, UiPanelStyle style)
        => view.Style(style);

    public static IUiView TextStyle(this IUiView view, UiTextStyle style)
        => view.Style(style);

    public static IUiView Title(this IUiView view, float fontSize = 28f)
        => view.TextStyle(UiTextStyle.Title with { FontSize = fontSize });

    public static IUiView Subtitle(this IUiView view, float fontSize = 20f)
        => view.TextStyle(UiTextStyle.Subtitle with { FontSize = fontSize });

    public static IUiView Caption(this IUiView view, float fontSize = 12f)
        => view.TextStyle(UiTextStyle.Caption with { FontSize = fontSize });

    public static IUiView Disabled(this IUiView view, bool disabled = true) => view.Modifier(UiModifier.Disabled(disabled));
    public static IUiView FontSize(this IUiView view, float size) => view.Modifier(UiModifier.FontSize(size));
    public static IUiView Foreground(this IUiView view, UiColor color) => view.Modifier(UiModifier.Foreground(color));
    public static IUiView Foreground(this IUiView view, UiStyleColor color) => view.Modifier(UiModifier.Foreground(color));
    public static IUiView Accent(this IUiView view) => view.Modifier(UiModifier.Accent());
    public static IUiView Muted(this IUiView view) => view.Modifier(UiModifier.Muted());
    public static IUiView Tone(this IUiView view, UiTextTone tone) => view.Modifier(UiModifier.Tone(tone));
    public static IUiView Success(this IUiView view) => view.Tone(UiTextTone.Success);
    public static IUiView Warning(this IUiView view) => view.Tone(UiTextTone.Warning);
    public static IUiView Danger(this IUiView view) => view.Tone(UiTextTone.Danger);
    public static IUiView ButtonRole(this IUiView view, UiButtonRole role) => view.Modifier(UiModifier.ButtonRole(role));
    public static IUiView PrimaryButton(this IUiView view) => view.ButtonRole(UiButtonRole.Primary);
    public static IUiView DangerButton(this IUiView view) => view.ButtonRole(UiButtonRole.Danger);
    public static IUiView ItemWidth(this IUiView view, float width) => view.Modifier(UiModifier.ItemWidth(width));
    public static IUiView FillWidth(this IUiView view) => view.Modifier(UiModifier.FillWidth());
    public static IUiView ItemSpacing(this IUiView view, UiVector2 spacing) => view.Modifier(UiModifier.ItemSpacing(spacing));
    public static IUiView TextWrap(this IUiView view, float wrapPosX = 0f) => view.Modifier(UiModifier.TextWrap(wrapPosX));
    public static IUiView Tooltip(this IUiView view, string text) => view.Modifier(UiModifier.Tooltip(text));
    public static IUiView Tooltip(this IUiView view, Func<string> text) => view.Modifier(UiModifier.Tooltip(text));
    public static IUiView StyleColor(this IUiView view, UiStyleColor color, UiColor value) => view.Modifier(UiModifier.StyleColor(color, value));
    public static IUiView StyleVar(this IUiView view, UiStyleVar styleVar, float value) => view.Modifier(UiModifier.StyleVar(styleVar, value));
    public static IUiView StyleVar(this IUiView view, UiStyleVar styleVar, UiVector2 value) => view.Modifier(UiModifier.StyleVar(styleVar, value));
    public static IUiView Key(this IUiView view, object? key) => DuxelView.Layout.Key(key, view);

    public static IUiView Frame(
        this IUiView view,
        float width = 0f,
        float height = 0f,
        bool fillWidth = false,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with
            {
                Size = new UiVector2(MathF.Max(0f, width), MathF.Max(0f, height)),
                FillWidth = fillWidth
            },
            "frame",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView FillFrameWidth(
        this IUiView view,
        float height = 0f,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Frame(
            width: 0f,
            height: height,
            fillWidth: true,
            expression: expression,
            filePath: filePath,
            lineNumber: lineNumber,
            memberName: memberName);

    public static IUiView Width(
        this IUiView view,
        float width,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with { Size = options.Size with { X = MathF.Max(0f, width) } },
            "width",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Height(
        this IUiView view,
        float height,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with { Size = options.Size with { Y = MathF.Max(0f, height) } },
            "height",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Background(
        this IUiView view,
        UiColor color,
        float radius = 0f,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with
            {
                Background = color,
                BackgroundStyle = null,
                Radius = radius > 0f ? radius : options.Radius,
                RadiusToken = radius > 0f ? null : options.RadiusToken
            },
            "background",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Background(
        this IUiView view,
        UiStyleColor color,
        float radius = 0f,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with
            {
                Background = null,
                BackgroundStyle = color,
                Radius = radius > 0f ? radius : options.Radius,
                RadiusToken = radius > 0f ? null : options.RadiusToken
            },
            "background-style",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Border(
        this IUiView view,
        UiColor color,
        float thickness = 1f,
        float radius = 0f,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with
            {
                Border = color,
                BorderStyle = null,
                BorderThickness = MathF.Max(0f, thickness),
                Radius = radius > 0f ? radius : options.Radius,
                RadiusToken = radius > 0f ? null : options.RadiusToken
            },
            "border",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Border(
        this IUiView view,
        UiStyleColor color,
        float thickness = 1f,
        float radius = 0f,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with
            {
                Border = null,
                BorderStyle = color,
                BorderThickness = MathF.Max(0f, thickness),
                Radius = radius > 0f ? radius : options.Radius,
                RadiusToken = radius > 0f ? null : options.RadiusToken
            },
            "border-style",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView CornerRadius(
        this IUiView view,
        float radius,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with { Radius = MathF.Max(0f, radius), RadiusToken = null },
            "corner-radius",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView CornerRadius(
        this IUiView view,
        UiDesignToken token,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
        => view.Decorated(
            options => options with { Radius = 0f, RadiusToken = token },
            "corner-radius-token",
            expression,
            filePath,
            lineNumber,
            memberName);

    public static IUiView Decorated(
        this IUiView view,
        UiDecorationOptions options,
        string? id = null,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(view))] string? expression = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var decorationId = string.IsNullOrWhiteSpace(id)
            ? BuildDecorationId("decorated", expression, filePath, lineNumber, memberName)
            : id;
        return view is UiDecoratedView decorated
            ? decorated.With(options)
            : view is UiPaddedView padded
                ? new UiDecoratedView(padded.Content, options with { Padding = UiPaddedView.Add(padded.Padding, options.Padding) }, decorationId)
            : new UiDecoratedView(view, options, decorationId);
    }

    public static IUiView Padding(this IUiView view, float all)
        => view.Padding(new UiEdgeInsets(all));

    public static IUiView Padding(this IUiView view, float horizontal, float vertical)
        => view.Padding(new UiEdgeInsets(horizontal, vertical));

    public static IUiView Padding(this IUiView view, UiEdgeInsets padding)
    {
        ArgumentNullException.ThrowIfNull(view);
        padding = UiPaddedView.NormalizePadding(padding);
        if (view is UiDecoratedView decorated)
        {
            return decorated.With(decorated.Options with { Padding = UiPaddedView.Add(decorated.Options.Padding, padding) });
        }

        return view is UiPaddedView padded
            ? new UiPaddedView(padded.Content, UiPaddedView.Add(padded.Padding, padding))
            : new UiPaddedView(view, padding);
    }

    private static IUiView Decorated(
        this IUiView view,
        Func<UiDecorationOptions, UiDecorationOptions> configure,
        string kind,
        string? expression,
        string? filePath,
        int lineNumber,
        string? memberName)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(configure);
        if (view is UiDecoratedView decorated)
        {
            return decorated.With(configure(decorated.Options));
        }

        if (view is UiPaddedView padded)
        {
            var paddedOptions = configure(default) with { Padding = padded.Padding };
            var paddedId = BuildDecorationId(kind, expression, filePath, lineNumber, memberName);
            return new UiDecoratedView(padded.Content, paddedOptions, paddedId);
        }

        var id = BuildDecorationId(kind, expression, filePath, lineNumber, memberName);
        return new UiDecoratedView(view, configure(default), id);
    }

    private static string BuildDecorationId(string kind, string? expression, string? filePath, int lineNumber, string? memberName)
    {
        var hash = StableHash($"{filePath}|{lineNumber}|{memberName}|{expression}");
        return $"##duxel-decorated:{kind}:{memberName}:{lineNumber}:{hash:X8}";
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        var hash = offset;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return hash;
    }

    public static IUiView VisibleIf(this IUiView view, bool visible)
        => visible ? view : Dux.Empty();

    public static IUiView VisibleIf(this IUiView view, Func<bool> visible)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(visible);
        return new UiView(ui =>
        {
            if (visible())
            {
                view.Render(ui);
            }
        });
    }
}

public static class Dux
{
    public static UiState<T> State<T>(T value) => new(value);
    public static UiBinding<T> Bind<T>(Func<T> getValue, Action<T> setValue) => new(getValue, setValue);
    public static UiBinding<T> Bind<T>(UiState<T> state) => state;
    public static IUiView Empty() => View(_ => { });
    public static IUiView View(Action<UiImmediateContext> render) => DuxelView.Custom(render);
    public static IUiView Widget(IUiCustomWidget widget) => DuxelView.Widget(widget);
    public static IUiView Text(string text) => DuxelView.Text.Block(text);
    public static IUiView Text(Func<string> text) => DuxelView.Text.Block(text);
    public static IUiView TextWrapped(string text) => DuxelView.Text.Wrapped(text);
    public static IUiView TextWrapped(Func<string> text) => DuxelView.Text.Wrapped(text);
    public static IUiView Heading(string text, float fontSize = 24f) => DuxelView.Text.Heading(text, fontSize);
    public static IUiView Heading(Func<string> text, float fontSize = 24f) => DuxelView.Text.Heading(text, fontSize);
    public static IUiView TextColored(UiColor color, string text) => DuxelView.Text.Colored(color, text);
    public static IUiView TextColored(UiColor color, Func<string> text) => DuxelView.Text.Colored(color, text);
    public static IUiView Surface(string id, IUiView content, UiSurfaceOptions options = default) => DuxelView.Display.Surface(id, content, options);
    public static IUiView Card(string id, IUiView content, UiVector2 size = default) => DuxelView.Display.Card(id, content, size);
    public static IUiView Title(string text, float fontSize = 28f) => Text(text).Title(fontSize);
    public static IUiView Title(Func<string> text, float fontSize = 28f) => Text(text).Title(fontSize);
    public static IUiView Subtitle(string text, float fontSize = 20f) => Text(text).Subtitle(fontSize);
    public static IUiView Subtitle(Func<string> text, float fontSize = 20f) => Text(text).Subtitle(fontSize);
    public static IUiView Caption(string text, float fontSize = 12f) => Text(text).Caption(fontSize);
    public static IUiView Caption(Func<string> text, float fontSize = 12f) => Text(text).Caption(fontSize);
    public static IUiView Panel(IUiView content, float height = 0f, float padding = 14f) => content.Panel(height, padding);
    public static IUiView Panel(IUiView content, UiPanelStyle style) => content.Panel(style);
    public static IUiView Badge(string text, UiBadgeOptions options = default) => DuxelView.Display.Badge(text, options);
    public static IUiView Badge(Func<string> text, UiBadgeOptions options = default) => DuxelView.Display.Badge(text, options);
    public static IUiView Metric(string label, string value, UiMetricOptions options = default) => DuxelView.Display.Metric(label, value, options);
    public static IUiView Metric(string label, Func<string> value, UiMetricOptions options = default) => DuxelView.Display.Metric(label, value, options);
    public static IUiView MetricCard(string label, string value, string? detail = null, UiMetricCardOptions options = default) => DuxelView.Display.MetricCard(label, value, detail, options);
    public static IUiView MetricCard(string label, Func<string> value, string? detail = null, UiMetricCardOptions options = default) => DuxelView.Display.MetricCard(label, value, detail, options);
    public static IUiView MetricCard(string label, Func<string> value, Func<string>? detail, UiMetricCardOptions options = default) => DuxelView.Display.MetricCard(label, value, detail, options);
    public static UiPropertyItem Property(string label, string value, UiTextTone valueTone = UiTextTone.Normal) => DuxelView.Display.Property(label, value, valueTone);
    public static UiPropertyItem Property(string label, Func<string> value, UiTextTone valueTone = UiTextTone.Normal) => DuxelView.Display.Property(label, value, valueTone);
    public static IUiView PropertyList(params UiPropertyItem[] items) => DuxelView.Display.PropertyList(items);
    public static IUiView PropertyList(UiPropertyListOptions options, params UiPropertyItem[] items) => DuxelView.Display.PropertyList(options, items);
    public static UiSettingItem Setting(string label, IUiView content) => DuxelView.Display.Setting(label, content);
    public static UiSettingItem Setting(string label, IUiView content, string description) => DuxelView.Display.Setting(label, content, description);
    public static UiSettingItem Setting(string label, IUiView content, Func<string>? description) => DuxelView.Display.Setting(label, content, description);
    public static IUiView SettingsGroup(params UiSettingItem[] items) => DuxelView.Display.SettingsGroup(items);
    public static IUiView SettingsGroup(UiSettingsGroupOptions options, params UiSettingItem[] items) => DuxelView.Display.SettingsGroup(options, items);
    public static IUiView StatusRow(string title, string meta, Func<float> progress, UiBinding<bool>? selected = null, UiStatusRowOptions options = default) => DuxelView.Display.StatusRow(title, meta, progress, selected, options);
    public static IUiView StatusRow(Func<string> title, Func<string> meta, Func<float> progress, UiBinding<bool>? selected = null, UiStatusRowOptions options = default) => DuxelView.Display.StatusRow(title, meta, progress, selected, options);
    public static UiMetaItem Meta(string text, UiTextTone tone = UiTextTone.Accent) => DuxelView.Display.Meta(text, tone);
    public static UiMetaItem Meta(Func<string> text, UiTextTone tone = UiTextTone.Accent) => DuxelView.Display.Meta(text, tone);
    public static IUiView MetaBar(params UiMetaItem[] items) => DuxelView.Display.MetaBar(items);
    public static IUiView MetaBar(float spacing, params UiMetaItem[] items) => DuxelView.Display.MetaBar(spacing, items);
    public static IUiView Header(string title, params UiMetaItem[] meta) => DuxelView.Display.Header(title, meta);
    public static IUiView Header(Func<string> title, params UiMetaItem[] meta) => DuxelView.Display.Header(title, meta);
    public static UiShellItem NavItem(string label, IUiView content) => DuxelView.Display.NavItem(label, content);
    public static UiShellItem NavItem(string label, IUiView content, string detail, UiTextTone detailTone = UiTextTone.Muted) => DuxelView.Display.NavItem(label, content, detail, detailTone);
    public static UiShellItem NavItem(string label, IUiView content, Func<string>? detail, UiTextTone detailTone = UiTextTone.Muted) => DuxelView.Display.NavItem(label, content, detail, detailTone);
    public static UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content) => DuxelView.Display.NavItem(value, label, content);
    public static UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content, string detail, UiTextTone detailTone = UiTextTone.Muted) => DuxelView.Display.NavItem(value, label, content, detail, detailTone);
    public static UiShellItem<TValue> NavItem<TValue>(TValue value, string label, IUiView content, Func<string>? detail, UiTextTone detailTone = UiTextTone.Muted) => DuxelView.Display.NavItem(value, label, content, detail, detailTone);
    public static IUiView AppShell(string title, UiBinding<int> selectedIndex, IReadOnlyList<UiShellItem> items, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedIndex, items, meta);
    public static IUiView AppShell(string title, UiBinding<int> selectedIndex, IReadOnlyList<UiShellItem> items, UiAppShellOptions options, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedIndex, items, options, meta);
    public static IUiView AppShell(Func<string> title, UiBinding<int> selectedIndex, IReadOnlyList<UiShellItem> items, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedIndex, items, meta);
    public static IUiView AppShell(Func<string> title, UiBinding<int> selectedIndex, IReadOnlyList<UiShellItem> items, UiAppShellOptions options, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedIndex, items, options, meta);
    public static IUiView AppShell<TValue>(string title, UiBinding<TValue> selectedValue, IReadOnlyList<UiShellItem<TValue>> items, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedValue, items, meta);
    public static IUiView AppShell<TValue>(string title, UiBinding<TValue> selectedValue, IReadOnlyList<UiShellItem<TValue>> items, UiAppShellOptions options, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedValue, items, options, meta);
    public static IUiView AppShell<TValue>(Func<string> title, UiBinding<TValue> selectedValue, IReadOnlyList<UiShellItem<TValue>> items, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedValue, items, meta);
    public static IUiView AppShell<TValue>(Func<string> title, UiBinding<TValue> selectedValue, IReadOnlyList<UiShellItem<TValue>> items, UiAppShellOptions options, params UiMetaItem[] meta) => DuxelView.Display.AppShell(title, selectedValue, items, options, meta);
    public static IUiView EmptyState(string title, string message, IUiView? action = null, UiEmptyStateOptions options = default) => DuxelView.Display.EmptyState(title, message, action, options);
    public static IUiView EmptyState(Func<string> title, Func<string> message, IUiView? action = null, UiEmptyStateOptions options = default) => DuxelView.Display.EmptyState(title, message, action, options);
    public static IUiView Callout(string title, string message, IUiView? action = null, UiCalloutOptions options = default) => DuxelView.Display.Callout(title, message, action, options);
    public static IUiView Callout(string title, Func<string> message, IUiView? action = null, UiCalloutOptions options = default) => DuxelView.Display.Callout(title, message, action, options);
    public static IUiView Callout(Func<string> title, string message, IUiView? action = null, UiCalloutOptions options = default) => DuxelView.Display.Callout(title, message, action, options);
    public static IUiView Callout(Func<string> title, Func<string> message, IUiView? action = null, UiCalloutOptions options = default) => DuxelView.Display.Callout(title, message, action, options);
    public static UiCommand Command(string label, Action? action = null, UiButtonRole role = UiButtonRole.Normal, Func<bool>? enabled = null, Func<string>? tooltip = null, UiVector2 size = default) => DuxelView.Display.Command(label, action, role, enabled, tooltip, size);
    public static IUiView CommandBar(params UiCommand[] commands) => DuxelView.Display.CommandBar(commands);
    public static IUiView CommandBar(UiCommandBarOptions options, params UiCommand[] commands) => DuxelView.Display.CommandBar(options, commands);
    public static IUiView Toolbar(params IUiView[] items) => DuxelView.Display.Toolbar(items);
    public static IUiView Toolbar(float spacing, params IUiView[] items) => DuxelView.Display.Toolbar(spacing, items);
    public static IUiView Button(string label, Action? clicked = null, UiVector2 size = default) => DuxelView.Controls.Button(label, clicked, size);
    public static IUiView PrimaryButton(string label, Action? clicked = null, UiVector2 size = default) => Button(label, clicked, size).PrimaryButton();
    public static IUiView DangerButton(string label, Action? clicked = null, UiVector2 size = default) => Button(label, clicked, size).DangerButton();
    public static IUiView SmallButton(string label, Action? clicked = null) => DuxelView.Controls.SmallButton(label, clicked);
    public static IUiView Checkbox(string label, UiBinding<bool> value) => DuxelView.Controls.Checkbox(label, value);
    public static IUiView Checkbox(string label, Func<bool> getValue, Action<bool> setValue) => DuxelView.Controls.Checkbox(label, getValue, setValue);
    public static IUiView Toggle(string id, UiBinding<bool> value) => DuxelView.Controls.Toggle(id, value);
    public static IUiView Toggle(string id, Func<bool> getValue, Action<bool> setValue) => DuxelView.Controls.Toggle(id, getValue, setValue);
    public static IUiView RadioButton(string label, UiBinding<int> value, int buttonValue) => DuxelView.Controls.RadioButton(label, value, buttonValue);
    public static IUiView RadioButton(string label, Func<int> getValue, Action<int> setValue, int buttonValue) => DuxelView.Controls.RadioButton(label, getValue, setValue, buttonValue);
    public static IUiView InputText(string label, UiBinding<string> value, int maxLength = 256) => DuxelView.Controls.InputText(label, value, maxLength);
    public static IUiView InputText(string label, Func<string> getValue, Action<string> setValue, int maxLength = 256) => DuxelView.Controls.InputText(label, getValue, setValue, maxLength);
    public static IUiView TextField(string id, UiBinding<string> value, int maxLength = 256) => DuxelView.Controls.TextField(id, value, maxLength);
    public static IUiView TextField(string id, Func<string> getValue, Action<string> setValue, int maxLength = 256) => DuxelView.Controls.TextField(id, getValue, setValue, maxLength);
    public static IUiView InputTextMultiline(string label, UiBinding<string> value, int maxLength = 4096, float height = 160f) => DuxelView.Controls.InputTextMultiline(label, value, maxLength, height);
    public static IUiView InputTextMultiline(string label, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f) => DuxelView.Controls.InputTextMultiline(label, getValue, setValue, maxLength, height);
    public static IUiView TextArea(string id, UiBinding<string> value, int maxLength = 4096, float height = 160f) => DuxelView.Controls.TextArea(id, value, maxLength, height);
    public static IUiView TextArea(string id, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f) => DuxelView.Controls.TextArea(id, getValue, setValue, maxLength, height);
    public static IUiView MultilineTextField(string id, UiBinding<string> value, int maxLength = 4096, float height = 160f) => DuxelView.Controls.MultilineTextField(id, value, maxLength, height);
    public static IUiView MultilineTextField(string id, Func<string> getValue, Action<string> setValue, int maxLength = 4096, float height = 160f) => DuxelView.Controls.MultilineTextField(id, getValue, setValue, maxLength, height);
    public static IUiView InputInt(string label, UiBinding<int> value) => DuxelView.Controls.InputInt(label, value);
    public static IUiView InputInt(string label, Func<int> getValue, Action<int> setValue) => DuxelView.Controls.InputInt(label, getValue, setValue);
    public static IUiView NumberField(string id, UiBinding<int> value) => DuxelView.Controls.NumberField(id, value);
    public static IUiView NumberField(string id, Func<int> getValue, Action<int> setValue) => DuxelView.Controls.NumberField(id, getValue, setValue);
    public static IUiView IntField(string id, UiBinding<int> value) => DuxelView.Controls.IntField(id, value);
    public static IUiView IntField(string id, Func<int> getValue, Action<int> setValue) => DuxelView.Controls.IntField(id, getValue, setValue);
    public static IUiView SliderFloat(string label, UiBinding<float> value, float min, float max) => DuxelView.Controls.SliderFloat(label, value, min, max);
    public static IUiView SliderFloat(string label, Func<float> getValue, Action<float> setValue, float min, float max) => DuxelView.Controls.SliderFloat(label, getValue, setValue, min, max);
    public static IUiView Slider(string id, UiBinding<float> value, float min, float max) => DuxelView.Controls.Slider(id, value, min, max);
    public static IUiView Slider(string id, Func<float> getValue, Action<float> setValue, float min, float max) => DuxelView.Controls.Slider(id, getValue, setValue, min, max);
    public static IUiView SliderInt(string label, UiBinding<int> value, int min, int max) => DuxelView.Controls.SliderInt(label, value, min, max);
    public static IUiView SliderInt(string label, Func<int> getValue, Action<int> setValue, int min, int max) => DuxelView.Controls.SliderInt(label, getValue, setValue, min, max);
    public static IUiView Slider(string id, UiBinding<int> value, int min, int max) => DuxelView.Controls.Slider(id, value, min, max);
    public static IUiView Slider(string id, Func<int> getValue, Action<int> setValue, int min, int max) => DuxelView.Controls.Slider(id, getValue, setValue, min, max);
    public static IUiView ProgressBar(Func<float> fraction, UiVector2 size = default, Func<string?>? overlay = null) => DuxelView.Controls.ProgressBar(fraction, size, overlay);
    public static IUiView Combo(string label, UiBinding<int> selectedIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8) => DuxelView.Controls.Combo(label, selectedIndex, items, popupMaxHeightInItems);
    public static IUiView Combo(string label, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, int popupMaxHeightInItems = 8) => DuxelView.Controls.Combo(label, getValue, setValue, items, popupMaxHeightInItems);
    public static IUiView Segmented(string id, UiBinding<int> selectedIndex, IReadOnlyList<string> items, UiSegmentedOptions options = default) => DuxelView.Controls.Segmented(id, selectedIndex, items, options);
    public static IUiView Segmented(string id, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, UiSegmentedOptions options = default) => DuxelView.Controls.Segmented(id, getValue, setValue, items, options);
    public static UiChoice<T> Choice<T>(T value, string label) => DuxelView.Controls.Choice(value, label);
    public static IUiView Segmented<T>(string id, UiBinding<T> selectedValue, IReadOnlyList<UiChoice<T>> choices, UiSegmentedOptions options = default) => DuxelView.Controls.Segmented(id, selectedValue, choices, options);
    public static IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, IReadOnlyList<UiChoice<T>> choices, UiSegmentedOptions options = default) => DuxelView.Controls.Segmented(id, getValue, setValue, choices, options);
    public static IUiView Segmented<T>(string id, UiBinding<T> selectedValue, params UiChoice<T>[] choices) => DuxelView.Controls.Segmented(id, selectedValue, choices);
    public static IUiView Segmented<T>(string id, UiBinding<T> selectedValue, UiSegmentedOptions options, params UiChoice<T>[] choices) => DuxelView.Controls.Segmented(id, selectedValue, options, choices);
    public static IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, params UiChoice<T>[] choices) => DuxelView.Controls.Segmented(id, getValue, setValue, choices);
    public static IUiView Segmented<T>(string id, Func<T> getValue, Action<T> setValue, UiSegmentedOptions options, params UiChoice<T>[] choices) => DuxelView.Controls.Segmented(id, getValue, setValue, options, choices);
    public static IUiView EnumSegmented<TEnum>(string id, UiBinding<TEnum> selectedValue, UiSegmentedOptions options = default) where TEnum : struct, Enum => DuxelView.Controls.EnumSegmented(id, selectedValue, options);
    public static IUiView EnumSegmented<TEnum>(string id, Func<TEnum> getValue, Action<TEnum> setValue, UiSegmentedOptions options = default) where TEnum : struct, Enum => DuxelView.Controls.EnumSegmented(id, getValue, setValue, options);
    public static IUiView EnumSegmented<TEnum>(string id, UiBinding<TEnum> selectedValue, Func<TEnum, string> label, UiSegmentedOptions options = default) where TEnum : struct, Enum => DuxelView.Controls.EnumSegmented(id, selectedValue, label, options);
    public static IUiView EnumSegmented<TEnum>(string id, Func<TEnum> getValue, Action<TEnum> setValue, Func<TEnum, string> label, UiSegmentedOptions options = default) where TEnum : struct, Enum => DuxelView.Controls.EnumSegmented(id, getValue, setValue, label, options);
    public static IUiView ListBox(string label, UiBinding<int> selectedIndex, IReadOnlyList<string> items, int visibleItems = 6) => DuxelView.Controls.ListBox(label, selectedIndex, items, visibleItems);
    public static IUiView ListBox(string label, Func<int> getValue, Action<int> setValue, IReadOnlyList<string> items, int visibleItems = 6) => DuxelView.Controls.ListBox(label, getValue, setValue, items, visibleItems);
    public static IUiView Selectable(string label, UiBinding<bool> selected, UiVector2 size = default) => DuxelView.Controls.Selectable(label, selected, size);
    public static IUiView Selectable(string label, Func<bool> getValue, Action<bool> setValue, UiVector2 size = default) => DuxelView.Controls.Selectable(label, getValue, setValue, size);
    public static IUiView MainMenuBar(params IUiView[] menus) => DuxelView.Menus.MainMenuBar(menus);
    public static IUiView Menu(string label, params IUiView[] items) => DuxelView.Menus.Menu(label, items);
    public static IUiView MenuItem(string label, Action? clicked = null, Func<bool>? selected = null, Func<bool>? enabled = null) => DuxelView.Menus.MenuItem(label, clicked, selected, enabled);
    public static IUiView Spacer(float height = 0f) => DuxelView.Layout.Spacer(height);
    public static IUiView Separator() => DuxelView.Layout.Separator();
    public static IUiView Separator(string text) => DuxelView.Layout.Separator(text);
    public static IUiView Column(params IUiView[] children) => DuxelView.Layout.Column(children);
    public static IUiView Column(float spacing, params IUiView[] children) => DuxelView.Layout.Column(spacing, children);
    public static IUiView VStack(params IUiView[] children) => DuxelView.Layout.VStack(children);
    public static IUiView VStack(float spacing, params IUiView[] children) => DuxelView.Layout.VStack(spacing, children);
    public static IUiView Row(params IUiView[] children) => DuxelView.Layout.Row(children);
    public static IUiView Row(float spacing, params IUiView[] children) => DuxelView.Layout.Row(spacing, children);
    public static IUiView HStack(params IUiView[] children) => DuxelView.Layout.HStack(children);
    public static IUiView HStack(float spacing, params IUiView[] children) => DuxelView.Layout.HStack(spacing, children);
    public static IUiView Group(params IUiView[] children) => DuxelView.Layout.Group(children);
    public static IUiView Child(string id, IUiView content, UiVector2 size = default, bool border = false) => DuxelView.Layout.Child(id, content, size, border);
    public static IUiView Key(object? key, IUiView content) => DuxelView.Layout.Key(key, content);
    public static IUiView ScrollView(string id, IUiView content, UiVector2 size = default, bool border = false) => DuxelView.Layout.ScrollView(id, content, size, border);
    public static IUiView Section(string title, IUiView content, UiSectionOptions options = default) => DuxelView.Layout.Section(title, content, options);
    public static UiFormRow Field(string label, IUiView content, float labelWidth = 140f) => DuxelView.Layout.Field(label, content, labelWidth);
    public static IUiView Form(params UiFormRow[] rows) => DuxelView.Layout.Form(rows);
    public static IUiView Form(float labelWidth, params UiFormRow[] rows) => DuxelView.Layout.Form(labelWidth, rows);
    public static IUiView FormRow(string label, IUiView content, float labelWidth = 140f) => DuxelView.Layout.FormRow(label, content, labelWidth);
    public static IUiView List<T>(string id, IEnumerable<T> source, Func<T, IUiView> row, UiVector2 size = default, bool border = false) => DuxelView.Layout.List(id, source, row, size, border);
    public static IUiView List<T>(string id, IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> row, UiVector2 size = default, bool border = false) => DuxelView.Layout.List(id, source, key, row, size, border);
    public static IUiView List<T>(string id, Func<IEnumerable<T>> source, Func<T, IUiView> row, UiVector2 size = default, bool border = false) => DuxelView.Layout.List(id, source, row, size, border);
    public static IUiView List<T>(string id, Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> row, UiVector2 size = default, bool border = false) => DuxelView.Layout.List(id, source, key, row, size, border);
    public static IUiView Grid(params IUiView[] children) => DuxelView.Layout.Grid(children);
    public static IUiView Grid(int columns, params IUiView[] children) => DuxelView.Layout.Grid(columns, children);
    public static IUiView Grid(UiGridOptions options, params IUiView[] children) => DuxelView.Layout.Grid(options, children);
    public static IUiView Grid<T>(IEnumerable<T> source, Func<T, IUiView> cell, UiGridOptions options = default) => DuxelView.Layout.Grid(source, cell, options);
    public static IUiView Grid<T>(IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> cell, UiGridOptions options = default) => DuxelView.Layout.Grid(source, key, cell, options);
    public static IUiView Grid<T>(Func<IEnumerable<T>> source, Func<T, IUiView> cell, UiGridOptions options = default) => DuxelView.Layout.Grid(source, cell, options);
    public static IUiView Grid<T>(Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> cell, UiGridOptions options = default) => DuxelView.Layout.Grid(source, key, cell, options);
    public static IUiView Split(params IUiView[] columns) => DuxelView.Layout.Split(columns);
    public static UiTab Tab(string label, IUiView content) => DuxelView.Layout.Tab(label, content);
    public static IUiView Tabs(string id, params UiTab[] tabs) => DuxelView.Layout.Tabs(id, tabs);
    public static IUiView Tree(string label, IUiView content, bool defaultOpen = false) => DuxelView.Layout.Tree(label, content, defaultOpen);
    public static UiTableColumn TableColumn(string header, float width = 0f, float alignX = 0f) => new(header, width, alignX);
    public static IUiView Table<T>(string id, IReadOnlyList<UiTableColumn> columns, IEnumerable<T> rows, Func<T, IReadOnlyList<IUiView>> cells, UiTableFlags flags = UiTableFlags.Borders | UiTableFlags.RowBg) => DuxelView.Layout.Table(id, columns, rows, cells, flags);
    public static IUiView If(bool condition, IUiView content) => DuxelView.Layout.If(condition, content);
    public static IUiView If(Func<bool> condition, IUiView content) => DuxelView.Layout.If(condition, content);
    public static IUiView If(Func<bool> condition, IUiView whenTrue, IUiView whenFalse) => DuxelView.Layout.If(condition, whenTrue, whenFalse);
    public static IUiView When(bool condition, IUiView content) => DuxelView.Layout.When(condition, content);
    public static IUiView When(Func<bool> condition, IUiView content) => DuxelView.Layout.When(condition, content);
    public static IUiView Unless(bool condition, IUiView content) => DuxelView.Layout.Unless(condition, content);
    public static IUiView Unless(Func<bool> condition, IUiView content) => DuxelView.Layout.Unless(condition, content);
    public static UiSwitchCase<T> Case<T>(T value, IUiView content) => DuxelView.Layout.Case(value, content);
    public static IUiView Switch<T>(T value, params UiSwitchCase<T>[] cases) => DuxelView.Layout.Switch(value, cases);
    public static IUiView Switch<T>(T value, IUiView defaultContent, params UiSwitchCase<T>[] cases) => DuxelView.Layout.Switch(value, defaultContent, cases);
    public static IUiView Switch<T>(Func<T> value, params UiSwitchCase<T>[] cases) => DuxelView.Layout.Switch(value, cases);
    public static IUiView Switch<T>(Func<T> value, IUiView defaultContent, params UiSwitchCase<T>[] cases) => DuxelView.Layout.Switch(value, defaultContent, cases);
    public static IUiView ForEach<T>(IEnumerable<T> source, Func<T, IUiView> content) => DuxelView.Layout.ForEach(source, content);
    public static IUiView ForEach<T>(IEnumerable<T> source, Func<T, object?> key, Func<T, IUiView> content) => DuxelView.Layout.ForEach(source, key, content);
    public static IUiView ForEach<T>(Func<IEnumerable<T>> source, Func<T, IUiView> content) => DuxelView.Layout.ForEach(source, content);
    public static IUiView ForEach<T>(Func<IEnumerable<T>> source, Func<T, object?> key, Func<T, IUiView> content) => DuxelView.Layout.ForEach(source, key, content);
    public static IUiView ForEachIndexed<T>(IEnumerable<T> source, Func<int, T, IUiView> content) => DuxelView.Layout.ForEachIndexed(source, content);
    public static IUiView ForEachIndexed<T>(IEnumerable<T> source, Func<int, T, object?> key, Func<int, T, IUiView> content) => DuxelView.Layout.ForEachIndexed(source, key, content);
    public static IUiView ForEachIndexed<T>(Func<IEnumerable<T>> source, Func<int, T, IUiView> content) => DuxelView.Layout.ForEachIndexed(source, content);
    public static IUiView ForEachIndexed<T>(Func<IEnumerable<T>> source, Func<int, T, object?> key, Func<int, T, IUiView> content) => DuxelView.Layout.ForEachIndexed(source, key, content);
    public static IUiView Window(string title, IUiView content, UiWindowOptions options = default) => DuxelView.Windows.Window(title, content, options);
    public static UiScreen App(IUiView root) => DuxelView.App(root);
    public static UiScreen Screen(IUiView root) => DuxelView.Screen(root);
}
