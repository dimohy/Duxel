using System.Globalization;
using System.Text;

namespace Duxel.Core;

/// <summary>Explicit neighboring values and stable identities for a finite or cyclic wheel.</summary>
public interface IUiWheelSequence<T> where T : struct
{
    bool TryOffset(T value, int offset, out T target);
    string Format(T value);
    string Key(T value);
}
public sealed record UiWheelOptions(string Label, float Width = 160, float RowHeight = 32, int VisibleRows = 7);
public class UiWheelPicker<T> : UiControlWidget where T : struct
{
    private IUiWheelSequence<T> _sequence;
    private UiWheelOptions _options;
    private T? _anchor;
    private UiWheelItem<T>[] _items = [];
    internal float WheelRemainder, DragRemainder, DragDistance, LastPointerY;
    internal bool Dragged;
    public UiBinding<T> Value { get; }
    public Action<T>? Chosen { get; set; }
    public IUiWheelSequence<T> Sequence
    {
        get => _sequence;
        set { ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); if (ReferenceEquals(value, _sequence)) return; ThrowIfBuilding(); _sequence = value; Refresh(); }
    }
    public UiWheelOptions Options
    {
        get => _options;
        set { ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); Validate(value); if (value == _options) return; ThrowIfBuilding(); _options = value; Refresh(); }
    }
    public UiWheelPicker(UiBinding<T> value, IUiWheelSequence<T> sequence, UiWheelOptions options,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Validate(options);
    }
    private static void Validate(UiWheelOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Label);
        if (!float.IsFinite(options.Width) || options.Width <= 0 || !float.IsFinite(options.RowHeight) || options.RowHeight < 24
            || options.VisibleRows is < 3 or > 15 || options.VisibleRows % 2 != 1) throw new ArgumentOutOfRangeException(nameof(options));
    }
    public void Refresh() { ThrowIfDisposed(); _anchor = null; RequestRebuild(); }
    internal ReadOnlySpan<UiWheelItem<T>> Items
    {
        get
        {
            var current = Value.Value;
            if (_anchor is { } anchor && EqualityComparer<T>.Default.Equals(anchor, current)) return _items;
            if (!_sequence.TryOffset(current, 0, out _)) throw new ArgumentOutOfRangeException(nameof(Value), "Wheel value is outside its sequence.");
            _anchor = current;
            _items = new UiWheelItem<T>[_options.VisibleRows];
            var keys = new Dictionary<string, T>(StringComparer.Ordinal);
            for (var visit = 0; visit < _items.Length; visit++)
            {
                var i = visit == 0 ? _items.Length / 2 : visit % 2 == 1 ? _items.Length / 2 - (visit + 1) / 2 : _items.Length / 2 + visit / 2;
                if (_sequence.TryOffset(current, i - _items.Length / 2, out var value))
                {
                    var key = _sequence.Key(value);
                    if (keys.TryGetValue(key, out var prior))
                    {
                        if (!EqualityComparer<T>.Default.Equals(prior, value)) throw new InvalidOperationException("Wheel keys must identify exactly one value.");
                        continue; // A short cyclic sequence exposes each value only once, nearest the center.
                    }
                    keys.Add(key, value);
                    _items[i] = new(value, _sequence.Format(value), "##wheel-" + key);
                }
            }
            return _items;
        }
    }
    internal bool MoveTo(T target)
    {
        Value.Value = target;
        var accepted = EqualityComparer<T>.Default.Equals(Value.Value, target);
        if (accepted) RequestRebuild();
        return accepted;
    }
    public bool Move(int offset)
    {
        ThrowIfDisposed();
        if (!_sequence.TryOffset(Value.Value, offset, out var target)) return false;
        return MoveTo(target);
    }
    public bool Choose(T value)
    {
        ThrowIfDisposed();
        if (!_sequence.TryOffset(value, 0, out var normalized) || !EqualityComparer<T>.Default.Equals(normalized, value)) return false;
        Value.Value = value;
        if (!EqualityComparer<T>.Default.Equals(Value.Value, value)) return false;
        Chosen?.Invoke(value);
        RequestRebuild();
        return true;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        using var font = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        return context.ConstrainToLayout(new(_options.Width, MathF.Max(_options.RowHeight, font.LineHeight + 8) * _options.VisibleRows));
    }
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderWheelPicker(this, PrimaryControlId);
    private protected override void ReleaseOwnedResources() { Chosen = null; base.ReleaseOwnedResources(); }
}
internal readonly record struct UiWheelItem<T>(T? Value, string? Label, string? Key) where T : struct;

public sealed class UiIntegerWheelSequence : IUiWheelSequence<int>
{
    private readonly int _minimum, _maximum;
    private readonly bool _wrap;
    private readonly string _format;
    private readonly CultureInfo _culture;
    private readonly string[] _digits;
    public UiIntegerWheelSequence(int minimum, int maximum, CultureInfo? culture = null, string format = "D2", bool wrap = false)
    {
        if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
        _minimum = minimum; _maximum = maximum; _wrap = wrap;
        _culture = CultureInfo.ReadOnly((CultureInfo)(culture ?? CultureInfo.CurrentCulture).Clone());
        _digits = _culture.NumberFormat.NativeDigits;
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _ = minimum.ToString(format, _culture);
    }
    public bool TryOffset(int value, int offset, out int target)
    {
        if (value < _minimum || value > _maximum) { target = default; return false; }
        var next = (long)value + offset;
        var count = (long)_maximum - _minimum + 1;
        if (_wrap) next = _minimum + ((next - _minimum) % count + count) % count;
        if (next < _minimum || next > _maximum) { target = default; return false; }
        target = (int)next; return true;
    }
    public string Format(int value)
    {
        if (value < _minimum || value > _maximum) throw new ArgumentOutOfRangeException(nameof(value));
        var text = value.ToString(_format, _culture);
        var result = new StringBuilder(text.Length);
        foreach (var c in text) result.Append(c is >= '0' and <= '9' ? _digits[c - '0'] : c.ToString());
        return result.ToString();
    }
    public string Key(int value) => value.ToString(CultureInfo.InvariantCulture);
}
public static partial class Dux
{
    public static UiWheelPicker<T> WheelPicker<T>(UiBinding<T> value, IUiWheelSequence<T> sequence, UiWheelOptions options) where T : struct
        => new(value, sequence, options);
}
