namespace Duxel.Core;

/// <summary>Numeric control state in logical value units, independently of screen direction.</summary>
public readonly record struct UiAccessibilityRangeInfo(double Value, double Minimum, double Maximum,
    double SmallChange, double LargeChange)
{
    public bool Vertical { get; init; }

    internal void Validate()
    {
        if (!double.IsFinite(Value) || !double.IsFinite(Minimum) || !double.IsFinite(Maximum) ||
            Minimum > Maximum || Value < Minimum || Value > Maximum || !double.IsFinite(SmallChange) ||
            SmallChange < 0 || !double.IsFinite(LargeChange) || LargeChange < 0)
            throw new ArgumentOutOfRangeException(nameof(Value), "Accessibility range values and changes must be finite and within their declared bounds.");
    }
}

public readonly record struct UiAccessibilityRangeChange(UiAccessibilityElement Element,
    UiAccessibilityRangeInfo? Previous, bool WasReadOnly);

public sealed partial class UiAccessibilityTree
{
    public event Action<UiAccessibilityRangeChange>? RangeChanged;
    private List<UiAccessibilityRangeChange>? _rangeChanges;

    public UiAccessibilityResult RequestRangeValue(string id, long runtimeId, double value, bool wait = true) =>
        QueueAction(id, runtimeId, UiAccessibilityAction.SetRangeValue, null, null, wait, null, out _, value);
}
