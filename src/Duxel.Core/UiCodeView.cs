namespace Duxel.Core;

public readonly record struct UiCodeViewOptions(bool Wrap = true, bool LineNumbers = true, int? MaximumVisibleLines = 32, bool InitiallyExpanded = true, bool ShowToolbar = true)
{
    public UiCodeViewOptions() : this(true, true, 32, true, true) { }
    internal void Validate()
    {
        if (MaximumVisibleLines is <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumVisibleLines));
    }
}

public sealed record UiCodeViewLabels(string Title, string LineCountFormat, string Wrap, string LineNumbers,
    string ShowAll, string Copy, string Copied, string AccessibleName)
{
    public string ContextMenuName { get; init; } = "Text actions";
    public string CopySelection { get; init; } = "Copy";
    public string SelectAll { get; init; } = "Select All";

    public static UiCodeViewLabels Default { get; } = new("C# 소스", "{0}줄", "줄바꿈",
        "줄 번호", "전체 표시", "소스 복사", "복사됨", "C# 예제 소스")
    {
        ContextMenuName = "텍스트 명령",
        CopySelection = "복사",
        SelectAll = "모두 선택"
    };
}

public readonly record struct UiCodeViewMetrics(int MeasurementBuilds, int PaintBuilds, int SyntaxParses);

/// <summary>A selectable, read-only C# block with cached syntax, line numbers and shared text navigation.</summary>
public sealed class UiCodeView : UiWidget
{
    private string _code;
    private UiCodeViewOptions _options;
    private bool _expanded;
    internal readonly UiCodeDocument Document = new();
    internal readonly UiMultilineInputOwner Owner = new();
    internal readonly UiVisualTextVariants Measurements = new();
    internal readonly UiVisualTextLines IndependentPaintLines = new();
    internal SelectionPopup? SelectionMenu { get; set; }
    internal int[] LineStarts { get; private set; } = [];
    // Cache a viewport's worth of labels without retaining a string for each source line.
    private const int LineLabelCapacity = 128;
    private (int Number, string? Text)[]? _lineLabels;

    internal string GetLineNumberText(int number)
    {
        // The runtime already reuses decimal strings below 300 on CoreCLR and NativeAOT.
        if (number < 300)
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var entries = _lineLabels ??= new (int, string?)[LineLabelCapacity];
        ref var entry = ref entries[number & (LineLabelCapacity - 1)];
        if (entry.Number == number)
            return entry.Text!;

        var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        entry = (number, text);
        return text;
    }

    public string Code
    {
        get => _code;
        set
        {
            ThrowIfDisposed();
            var normalized = Normalize(value);
            if (_code == normalized) return;
            ThrowIfBuilding();
            _code = normalized;
            SetLineStarts();
            Owner.ClearLines();
            RequestRebuild();
        }
    }
    public UiCodeViewOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); value.Validate();
            if (_options == value) return;
            ThrowIfBuilding(); _options = value; RequestRebuild();
        }
    }
    public UiCodeViewLabels Labels { get; }

    public UiCodeView(string code, UiCodeViewOptions? options = null) : this(code, options, UiCodeViewLabels.Default) { }

    public UiCodeView(string code, UiCodeViewOptions? options, UiCodeViewLabels labels)
    {
        Labels = labels ?? throw new ArgumentNullException(nameof(labels));
        _code = Normalize(code);
        _options = options ?? new();
        _options.Validate();
        _expanded = _options.InitiallyExpanded;
        SetLineStarts();
    }
    internal bool Expanded => _expanded;
    public UiCodeViewMetrics Metrics => new(Measurements.Builds, IndependentPaintLines.Builds, Document.ParseCount);
    private static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length > 1_048_576) throw new ArgumentOutOfRangeException(nameof(code));
        return code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
    private void SetLineStarts()
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < _code.Length; index++) if (_code[index] == '\n') starts.Add(index + 1);
        LineStarts = starts.ToArray();
    }
    protected override IUiView Build(UiBuildContext context) => context.Child<UiWidget>("root", () =>
    {
        var source = Dux.Stack(new UiStackOptions(), new Surface(this),
            Dux.Positioned(new UiCopyButton(() => Code, Labels.Copy, Labels.Copied), new(Right : 8, Top : 8)));
        if (!Options.ShowToolbar) return source;
        return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8, CrossAlignment : UiWrapCrossAlignment.Center),
                Fluent.Subtitle2(Labels.Title),
                Fluent.Caption1(() => string.Format(System.Globalization.CultureInfo.CurrentCulture, Labels.LineCountFormat, LineStarts.Length)),
                Fluent.Switch(Labels.Wrap, new UiBinding<bool>(() => Options.Wrap, value => Options = Options with { Wrap = value })),
                Fluent.Switch(Labels.LineNumbers, new UiBinding<bool>(() => Options.LineNumbers, value => Options = Options with { LineNumbers = value })),
                Fluent.Switch(Labels.ShowAll, new UiBinding<bool>(() => _expanded, value => { _expanded = value; RequestRebuild(); }))), source);
    });
    private protected override void ReleaseOwnedResources()
    {
        if (SelectionMenu is { } popup)
        {
            popup.Input.Entrance?.Reset();
            popup.Input.ScrollOwner.Release();
            SelectionMenu = null;
        }
        Owner.Dispose();
        Measurements.Clear();
        IndependentPaintLines.Clear();
        _lineLabels = null;
        base.ReleaseOwnedResources();
    }

    internal sealed class SelectionPopup(UiCodeViewLabels labels)
    {
        internal readonly UiMenuInputState Input = new() { Entrance = new() };
        internal readonly UiMenuOption[] CopyItems = [new("copy", labels.CopySelection)];
        internal readonly UiMenuOption[] SelectAllItems = [new("select-all", labels.SelectAll)];
        internal UiRect Anchor;
        internal string? Source;
        internal UiTextSelection Selection;
    }

    private sealed class Surface(UiCodeView code) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureCodeView(code);
        protected override void RenderControl(UiImmediateContext ui) => ui.RenderCodeView(code, PrimaryInputId("code-source"));
    }
}

public readonly partial struct UiControlContext
{
    internal UiVector2 MeasureCodeView(UiCodeView code) => _ui.MeasureCodeView(code);
}
