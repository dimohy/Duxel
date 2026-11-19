using System.Globalization;

namespace Duxel.Core;

public enum UiTimeHourCycle { Culture, Hours12, Hours24 }
public sealed record UiTimePickerOptions(CultureInfo? Culture = null, UiTimeHourCycle HourCycle = UiTimeHourCycle.Culture,
    bool ShowSeconds = false, float Width = 240, TimeOnly? InitialTime = null, UiPickerLabels? Labels = null,
    string? AMDesignator = null, string? PMDesignator = null);

/// <summary>Editable local time with independent hour/minute/second/period wheels.</summary>
public class UiTimePickerWidget : UiContainerBox<UiBoxContainerLayout, UiPlainContainerPresentation>
{
    private readonly UiTimePickerState _state;
    public UiBinding<TimeOnly?> Value => _state.Value;
    public string Text => _state.Text;
    public string? ValidationMessage => _state.Error;
    public bool IsOpen => _state.Popup.IsOpen;
    internal void ValidatePickerOptions(UiTimePickerOptions options) => _state.ValidateChange(options);
    public string DisplayPattern => _state.Pattern;
    public new UiTimePickerOptions Options
    {
        get => _state.Options;
        set { ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); ThrowIfBuilding(); _state.Configure(value); RequestRebuild(); }
    }
    public bool TrySetText(string text) { ThrowIfDisposed(); var accepted = _state.Edit(text); RequestRebuild(); return accepted; }
    public void ShowTime() { ThrowIfDisposed(); _state.Synchronize(); _state.Popup.Show(); RequestRebuild(); }
    public bool Close() => _state.Popup.Close();
    public UiTimePickerWidget(string label, UiBinding<TimeOnly?> value, UiTimePickerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(new UiTimePickerState(label, value, options ?? new()), identity) { }
    private UiTimePickerWidget(UiTimePickerState state, UiWidgetIdentity identity) : base(state.Field, identity : identity) => _state = state;
    protected override UiVector2 MeasureContent(UiControlContext context) { _state.Synchronize(); return base.MeasureContent(context); }
    protected override void RenderControl(UiImmediateContext ui) { _state.Synchronize(); base.RenderControl(ui); }
}
internal sealed class UiTimePickerState : UiTemporalPickerState<TimeOnly>
{
    private readonly CultureInfo _defaultCulture = CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());
    private CultureInfo _culture = CultureInfo.InvariantCulture;
    private TimeOnly _candidate;
    internal UiTimePickerOptions Options { get; private set; }
    protected override CultureInfo Culture => _culture;
    protected override UiPickerLabels Labels => Options.Labels ?? Defaults;
    private static readonly UiPickerLabels Defaults = new();
    private const float PopupPadding = 16;
    protected override string InvalidMessage => Labels.InvalidTime;
    internal string Pattern { get; private set; } = string.Empty;
    internal bool Hours12 { get; private set; }
    internal UiWheelPicker<int> Hour { get; }
    internal UiWheelPicker<int> Minute { get; }
    internal UiWheelPicker<int> Second { get; }
    internal UiWheelPicker<int> Period { get; }
    internal UiPopover<UiPickerTrigger<UiTimePickerIcon>> Popup { get; }
    private readonly TimePanel _panel;
    internal UiTimePickerState(string label, UiBinding<TimeOnly?> value, UiTimePickerOptions options) : base(label, value)
    {
        Options = options;
        var prepared = Prepare(options);
        _culture = prepared.Culture; Hours12 = prepared.Hours12; Pattern = prepared.Pattern;
        _candidate = value.Value ?? options.InitialTime ?? TimeOnly.MinValue;
        Hour = Dux.WheelPicker(new UiBinding<int>(() => Hours12 ? (_candidate.Hour + 11) % 12 + 1 : _candidate.Hour,
            hour => Change(Hours12 ? hour % 12 + _candidate.Hour / 12 * 12 : hour, _candidate.Minute, _candidate.Second)),
            Hours(), new(Labels.Hour, Width : 72));
        Minute = Dux.WheelPicker(new UiBinding<int>(() => _candidate.Minute, minute => Change(_candidate.Hour, minute, _candidate.Second)),
            new UiIntegerWheelSequence(0, 59, _culture, wrap : true), new(Labels.Minute, Width : 72));
        Second = Dux.WheelPicker(new UiBinding<int>(() => _candidate.Second, second => Change(_candidate.Hour, _candidate.Minute, second)),
            new UiIntegerWheelSequence(0, 59, _culture, wrap : true), new(Labels.Second, Width : 72));
        Period = Dux.WheelPicker(new UiBinding<int>(() => _candidate.Hour / 12, period => Change(_candidate.Hour % 12 + period * 12, _candidate.Minute, _candidate.Second)),
            new PeriodSequence(_culture), new(Labels.Period, Width : 88));
        _panel = new(this);
        Popup = new(Labels.ChooseTime, _panel, new(Width : PopupWidth, MaximumHeight : 480) { ContentPadding = PopupPadding });
        Popup.OpenChanged = open => { if (open) { _candidate = Value.Value ?? Options.InitialTime ?? TimeOnly.MinValue; RefreshWheels(); } };
        Initialize(Popup, options.Width);
    }
    private UiIntegerWheelSequence Hours() => new(Hours12 ? 1 : 0, Hours12 ? 12 : 23, _culture, wrap : true);
    private (CultureInfo Culture, bool Hours12, string Pattern) Prepare(UiTimePickerOptions options)
    {
        UiPickerText.Width(options.Width);
        UiPickerText.ValidateLabels(options.Labels ?? Defaults);
        if ((uint)options.HourCycle > 2) throw new ArgumentOutOfRangeException(nameof(options));
        var culture = (CultureInfo)(options.Culture ?? _defaultCulture).Clone();
        if (options.AMDesignator is { } am) culture.DateTimeFormat.AMDesignator = am;
        if (options.PMDesignator is { } pm) culture.DateTimeFormat.PMDesignator = pm;
        var shortPattern = culture.DateTimeFormat.ShortTimePattern;
        var hours12 = options.HourCycle == UiTimeHourCycle.Hours12 || options.HourCycle == UiTimeHourCycle.Culture && UiPickerText.HasToken(shortPattern, 'h');
        if (hours12 && (string.IsNullOrWhiteSpace(culture.DateTimeFormat.AMDesignator) || string.IsNullOrWhiteSpace(culture.DateTimeFormat.PMDesignator)))
            throw new ArgumentException("A12hour picker requires explicit nonempty AM and PM designators.", nameof(options));
        var pattern = (hours12 ? "hh:mm" : "HH:mm") + (options.ShowSeconds ? ":ss" : string.Empty);
        if (hours12)
            pattern = shortPattern.IndexOf("tt", StringComparison.Ordinal) < shortPattern.IndexOf('h') && shortPattern.Contains("tt", StringComparison.Ordinal)
                ? "tt " + pattern : pattern + " tt";
        return (CultureInfo.ReadOnly(culture), hours12, pattern);
    }
    internal void ValidateChange(UiTimePickerOptions options) => _ = Prepare(options);
    internal void Configure(UiTimePickerOptions options)
    {
        var prepared = Prepare(options);
        if (Options == options) return;
        Popup.Close(); Options = options; _culture = prepared.Culture; Hours12 = prepared.Hours12; Pattern = prepared.Pattern;
        Hour.Sequence = Hours(); Minute.Sequence = new UiIntegerWheelSequence(0, 59, _culture, wrap : true);
        Second.Sequence = new UiIntegerWheelSequence(0, 59, _culture, wrap : true); Period.Sequence = new PeriodSequence(_culture);
        Hour.Options = Hour.Options with { Label = Labels.Hour }; Minute.Options = Minute.Options with { Label = Labels.Minute };
        Second.Options = Second.Options with { Label = Labels.Second }; Period.Options = Period.Options with { Label = Labels.Period };
        Popup.Label = Labels.ChooseTime; Popup.Options = Popup.Options with { Width = PopupWidth };
        Input.Width = options.Width; Field.Width = options.Width; _panel.Refresh(); Synchronize(force : true);
    }
    private void RefreshWheels() { Hour.Refresh(); Minute.Refresh(); Second.Refresh(); Period.Refresh(); }
    private void Change(int hour, int minute, int second)
    {
        var previous = _candidate;
        var target = new TimeOnly(hour, minute, second).Add(TimeSpan.FromTicks(_candidate.Ticks % TimeSpan.TicksPerSecond));
        if (Accept(target)) _candidate = target; else _candidate = previous;
    }
    protected override string Format(TimeOnly value) => UiPickerText.ShapeDigits(value.ToString(Pattern, _culture), _culture);
    protected override bool TryParse(string text, out TimeOnly value)
    {
        if (!TimeOnly.TryParseExact(text, Pattern, _culture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) { value = default; return false; }
        var previous = Value.Value ?? TimeOnly.MinValue;
        value = new TimeOnly(parsed.Hour, parsed.Minute, Options.ShowSeconds ? parsed.Second : previous.Second)
            .Add(TimeSpan.FromTicks(previous.Ticks % TimeSpan.TicksPerSecond));
        return true;
    }
    protected override void ExternalValueChanged(TimeOnly? value) => _candidate = value ?? Options.InitialTime ?? TimeOnly.MinValue;
    private const float WheelGap = 8;
    private float WheelWidths => Hour.Options.Width + Minute.Options.Width
        + (Options.ShowSeconds ? Second.Options.Width : 0) + (Hours12 ? Period.Options.Width : 0);
    private int WheelGapCount => 1 + (Options.ShowSeconds ? 1 : 0) + (Hours12 ? 1 : 0);
    private float PanelWidth => WheelWidths + WheelGapCount * WheelGap;
    private float PopupWidth => PanelWidth + PopupPadding * 2;
    private sealed class PeriodSequence(CultureInfo culture) : IUiWheelSequence<int>
    {
        public bool TryOffset(int value, int offset, out int target) { var next = (long)value + offset; target = (int)next; return value is >= 0 and <= 1 && next is >= 0 and <= 1; }
        public string Format(int value) => value switch { 0 => culture.DateTimeFormat.AMDesignator, 1 => culture.DateTimeFormat.PMDesignator, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
        public string Key(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
    private sealed class TimePanel(UiTimePickerState state) : UiArrangedChildrenWidget(Create(state))
    {
        private static UiWidget[] Create(UiTimePickerState state)
        {
            UiWidget Column(Func<string> label, UiWidget wheel) => Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                Fluent.Caption1(label), wheel);
            return [Column(() => state.Labels.Hour, state.Hour), Column(() => state.Labels.Minute, state.Minute),
                Column(() => state.Labels.Second, state.Second), Column(() => state.Labels.Period, state.Period),
                Fluent.Button(state.Labels.Done, () => state.Popup.Close())];
        }
        internal void Refresh()
        {
            ((UiFluentButton)Children[4]).Label = state.Labels.Done;
            RequestRebuild();
        }
        protected override bool ShouldRenderChild(int index) => index != 2 && index != 3 || index == 2 && state.Options.ShowSeconds || index == 3 && state.Hours12;
        protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
        {
            var width = context.LayoutConstraints.Constrain(new(state.PanelWidth, 0)).X;
            var gap = MathF.Min(WheelGap, width / state.WheelGapCount);
            var scale = (width - gap * state.WheelGapCount) / state.WheelWidths;
            var x = 0f; var height = 0f;
            for (var i = 0; i < 4; i++)
            {
                if (!ShouldRenderChild(i)) { bounds[i] = default; continue; }
                var wheelWidth = i switch
                {
                    0 => state.Hour.Options.Width,
                    1 => state.Minute.Options.Width,
                    2 => state.Second.Options.Width,
                    _ => state.Period.Options.Width
                } * scale;
                var size = context.MeasureConstrained(Children[i], new(wheelWidth, wheelWidth, 0, float.PositiveInfinity));
                bounds[i] = new(x, 0, size.X, size.Y); x += size.X + gap; height = MathF.Max(height, size.Y);
            }
            if (state.Culture.TextInfo.IsRightToLeft)
                for (var i = 0; i < 4; i++) if (ShouldRenderChild(i)) bounds[i] = bounds[i] with { X = width - bounds[i].X - bounds[i].Width };
            var done = context.MeasureConstrained(Children[4], new(width, width, 0, float.PositiveInfinity));
            bounds[4] = new(0, height + 8, width, done.Y);
            return context.LayoutConstraints.Constrain(new(width, height + 8 + done.Y));
        }
    }
}
public static partial class Dux
{
    public static UiTimePickerWidget TimePicker(string label, UiBinding<TimeOnly?> value, UiTimePickerOptions? options = null) => new(label, value, options);
}
