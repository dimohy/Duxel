using System.Globalization;

namespace Duxel.Core;

/// <summary>Bounded numeric commits and decimal stepping, independent of editor and pointer state.</summary>
internal readonly record struct UiSpinNumberPolicy(double Minimum, double Maximum, double Step, double PageStep, int Precision)
{
    internal static UiSpinNumberPolicy Create(double? minimum = null, double? maximum = null,
        double step = 1, double pageStep = 1, int? precision = null)
    {
        if (minimum is { } min && !double.IsFinite(min) || maximum is { } max && !double.IsFinite(max)
            || minimum > maximum || !double.IsFinite(step) || step <= 0 || !double.IsFinite(pageStep) || pageStep <= 0
            || precision is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(step));
        var digits = precision ?? InferPrecision(step);
        if (digits > 15)
            throw new NotSupportedException("SpinButton supports at most15 decimal places; provide a representable step.");
        return new(minimum ?? double.MinValue, maximum ?? double.MaxValue, step, pageStep, digits);
    }

    private static int InferPrecision(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture).AsSpan();
        var exponentAt = text.IndexOfAny('e', 'E');
        var exponent = exponentAt < 0 ? 0 : int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var coefficient = exponentAt < 0 ? text : text[..exponentAt];
        var dot = coefficient.IndexOf('.');
        var fractional = dot < 0 ? 0 : coefficient.Length - dot - 1;
        return Math.Max(0, fractional - exponent);
    }

    internal void ValidateValue(double? value)
    {
        if (value is { } number && (!double.IsFinite(number) || number < Minimum || number > Maximum))
            throw new ArgumentOutOfRangeException(nameof(value), "The bound SpinButton value must be finite and within its declared range.");
    }

    internal double Normalize(double value)
    {
        if (double.IsNaN(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        var rounded = Math.Round(value, Precision, MidpointRounding.AwayFromZero);
        var bounded = Math.Clamp(rounded, Minimum, Maximum);
        return bounded == 0 ? 0 : bounded;
    }

    internal bool TryCommit(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
        {
            value = null;
            return false;
        }
        value = Normalize(number);
        return true;
    }

    internal double Move(double? value, int direction, bool page = false)
    {
        ValidateValue(value);
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
        var start = value ?? (Minimum == double.MinValue ? 0 : Minimum);
        return Normalize(start + (page ? PageStep : Step) * direction);
    }

    internal string Format(double? value)
    {
        ValidateValue(value);
        return value is { } number ? number.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    internal bool CanMove(double? value, int direction)
    {
        ValidateValue(value);
        var next = Move(value, direction);
        return value is null || next != value;
    }
}
