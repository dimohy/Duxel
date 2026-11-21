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
    public static UiCodeViewLabels Default { get; } = new("C# 소스", "{0}줄", "줄바꿈",
        "줄 번호", "전체 표시", "소스 복사", "복사됨", "C# 예제 소스");
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
    internal int[] LineStarts { get; private set; } = [];
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
            Dux.Positioned(new CopyAction(this), new(Right : 8, Top : 8)));
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
        Owner.Dispose();
        Measurements.Clear();
        IndependentPaintLines.Clear();
        base.ReleaseOwnedResources();
    }
    private sealed class CopyAction : UiWidget
    {
        private readonly UiFluentTooltip _hint;
        private readonly string _copyLabel;
        private double? _until;
        internal CopyAction(UiCodeView code)
        {
            _copyLabel = code.Labels.Copy;
            var button = new UiButton<UiReleaseButtonBehavior, CopyPresentation>(_copyLabel, () =>
            {
                var ui = RenderingContext!;
                ui.SetClipboardText(code.Code);
                _until = ui.GetTime() + 1.2;
                _hint!.Text = code.Labels.Copied;
                _hint.Visible = true;
                ui.RequestFrameAt(ui.GetTime());
            });
            _hint = Fluent.Tooltip(button, _copyLabel, new()
            {
                Positioning = new() { Position = UiPopupPosition.AboveEnd },
                ShowDelaySeconds = 0,
                HideDelaySeconds = 0
            });
            _hint.Visible = false;
        }
        protected override IUiView Build(UiBuildContext context) => context.Child("hint", () => _hint);
        protected override void RenderContent(UiImmediateContext ui)
        {
            var now = ui.GetTime();
            if (_until is { } until)
            {
                _hint.Visible = now < until;
                if (now < until) ui.RequestFrameAt(until);
                else { _until = null; _hint.Text = _copyLabel; }
            }
            base.RenderContent(ui);
        }
    }
    private readonly struct CopyPresentation : IUiButtonPresentation
    {
        public static bool CanRetainMeasurement => true;
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => new(32, 32);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
        {
            var foreground = context.Theme.ForcedColors?.CanvasText ?? context.Theme.Text;
            var active = interaction.Hovered || interaction.Focused;
            var icon = active || context.Theme.ForcedColors is not null ? foreground : new UiColor((foreground.Rgba & 0x00ffffffu) | 0x99000000u);
            context.Frame(bounds, active ? context.Theme.ButtonHovered : default, default, 4, 0);
            context.Frame(new(bounds.X + 13, bounds.Y + 9, 10, 10), default, icon, 2, 1.5f);
            context.Frame(new(bounds.X + 9, bounds.Y + 13, 10, 10), context.Theme.WindowBg, icon, 2, 1.5f);
            if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
        }
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
