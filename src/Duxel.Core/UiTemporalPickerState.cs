using System.Globalization;
using System.Text;

namespace Duxel.Core;

public sealed record UiPickerLabels(string ChooseDate = "Choose date", string ChooseTime = "Choose time", string Hour = "Hour",
    string Minute = "Minute", string Second = "Second", string Period = "AM/PM", string Done = "Done",
    string InvalidDate = "Enter a valid date.", string InvalidTime = "Enter a valid time.", string Rejected = "The value was not accepted.");

internal abstract class UiTemporalPickerState<T>(string label, UiBinding<T?> value) where T : struct
{
    private T? _last;
    private bool _initialized;
    private string _text = string.Empty;
    internal UiBinding<T?> Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
    internal UiFluentInput Input { get; private set; } = null!;
    internal UiFluentField Field { get; private set; } = null!;
    internal string Text => _text;
    internal string? Error { get; private set; }
    protected abstract CultureInfo Culture { get; }
    protected abstract UiPickerLabels Labels { get; }
    protected abstract string InvalidMessage { get; }
    protected abstract bool TryParse(string text, out T value);
    protected abstract string Format(T value);
    protected virtual bool Allowed(T value) => true;
    protected virtual void ExternalValueChanged(T? value) { }
    protected void Initialize(UiWidget trigger, float width)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Input = Fluent.Input(label, new UiBinding<string>(() => _text, text => Edit(text)), 128, width);
        Input.AfterSlot = new("picker", trigger, new(28, 28));
        Field = Fluent.Field(Input);
        Field.Width = width;
        Field.Options = new() { Label = label };
        Synchronize(force : true);
    }
    protected void Fail(string? error)
    {
        Error = error;
        if (Field is null) return;
        Field.Options = Field.Options with { ValidationMessage = error };
        Input.Options = Input.Options with { Invalid = error is not null };
    }
    internal void Synchronize(bool force = false)
    {
        var actual = Value.Value;
        if (actual is { } date && !Allowed(date)) throw new ArgumentOutOfRangeException(nameof(Value), "Bound picker value is outside its supported range.");
        if (!force && _initialized && EqualityComparer<T?>.Default.Equals(actual, _last)) return;
        _initialized = true; _last = actual;
        _text = actual is { } current ? Format(current) : string.Empty;
        Fail(null);
        ExternalValueChanged(actual);
    }
    internal bool Accept(T? value, bool preserveText = false)
    {
        if (value is { } candidate && !Allowed(candidate)) { Fail(InvalidMessage); return false; }
        Value.Value = value;
        if (!EqualityComparer<T?>.Default.Equals(Value.Value, value)) { Fail(Labels.Rejected); return false; }
        _last = value; _initialized = true;
        if (!preserveText) _text = value is { } current ? Format(current) : string.Empty;
        Fail(null);
        return true;
    }
    internal bool Edit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        if (string.IsNullOrWhiteSpace(text)) return Accept(null, preserveText : true);
        var normalized = UiPickerText.NormalizeDigits(text, Culture);
        if (!TryParse(normalized, out var value)) { Fail(InvalidMessage); return false; }
        return Accept(value, preserveText : true);
    }
}
internal static class UiPickerText
{
    internal static string NormalizeDigits(string text, CultureInfo culture)
    {
        var digits = culture.NumberFormat.NativeDigits;
        for (var i = 0; i < 10; i++)
            if (digits[i] != i.ToString(CultureInfo.InvariantCulture)) text = text.Replace(digits[i], i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return text;
    }
    internal static string ShapeDigits(string text, CultureInfo culture)
    {
        var digits = culture.NumberFormat.NativeDigits;
        var result = new StringBuilder(text.Length);
        foreach (var c in text) result.Append(c is >= '0' and <= '9' ? digits[c - '0'] : c.ToString());
        return result.ToString();
    }
    internal static void ValidateLabels(UiPickerLabels labels)
    {
        foreach (var value in new[] { labels.ChooseDate, labels.ChooseTime, labels.Hour, labels.Minute, labels.Second,
            labels.Period, labels.Done, labels.InvalidDate, labels.InvalidTime, labels.Rejected })
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
    }
    internal static bool HasToken(string pattern, char token)
    {
        var quote = '\0';
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\') { i++; continue; }
            if (quote != 0) { if (c == quote) quote = '\0'; continue; }
            if (c is '\'' or '"') { quote = c; continue; }
            if (c == token) return true;
        }
        return false;
    }
    internal static void Width(float width)
    {
        if (!float.IsFinite(width) || width < 80) throw new ArgumentOutOfRangeException(nameof(width));
    }
}

[UiFluentIcon("calendar", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiDatePickerIcon;
[UiFluentIcon("clock", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiTimePickerIcon;
internal readonly struct UiPickerTrigger<TIcon> : IUiPopoverPresentation where TIcon : IUiButtonIcon
{
    public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => BeginTriggerText(context);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => new(28, 28);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
    {
        if (interaction.Hovered || interaction.Expanded) context.Frame(bounds, context.Theme.ButtonHovered, default, 4, 0);
        TIcon.Paint(context, new(bounds.X + (bounds.Width - 20) / 2, bounds.Y + (bounds.Height - 20) / 2, 20, 20), context.Theme.Text, interaction);
        if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
    }
    public static float Padding(UiPopoverOptions options) => UiFluentPopoverPresentation.Padding(options);
    public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => context.Theme.Text;
    public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
        => UiFluentPopoverPresentation.PaintSurface(context, surface, layout, options);
}
