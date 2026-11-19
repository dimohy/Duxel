namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool ApplyAccessibilityRangeValue(string id, UiRect bounds, UiSliderOptions options, ref float value)
    {
        var request = TakeAccessibilityRangeValue(id, bounds, options.Minimum, options.Maximum);
        if (request is null) return false;
        var amount = request.RangeValue!.Value;
        var next = amount == value ? value : options.Snap(amount);
        var changed = next != value;
        value = next;
        request.Result = UiAccessibilityResult.Success;
        return changed;
    }

    private UiAccessibilityTree.Request? TakeAccessibilityRangeValue(string id, UiRect bounds, double minimum, double maximum)
    {
        var request = _accessibility?.Take(id, UiAccessibilityAction.SetRangeValue);
        if (request is null) return null;
        if (!IsControlEnabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return null;
        }
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return null;
        }
        if (request.RangeValue is not { } amount || !double.IsFinite(amount) || amount < minimum || amount > maximum)
        {
            request.Result = UiAccessibilityResult.InvalidArgument;
            return null;
        }
        return request;
    }

    private bool ApplyLegacyAccessibilityRangeValue(string id, UiRect bounds, float minimum, float maximum,
        bool integer, ref float value)
    {
        var request = TakeAccessibilityRangeValue(id, bounds, minimum, maximum);
        if (request is null) return false;
        // Legacy floats are continuous even with a keyboard step; integer wrappers round ties to even.
        var next = (float)request.RangeValue!.Value;
        if (integer) next = Math.Clamp(MathF.Round(next), minimum, maximum);
        var changed = next != value;
        value = next;
        request.Result = UiAccessibilityResult.Success;
        if (changed)
        {
            _lastItemEdited = true;
            _requestFrame?.Invoke();
        }
        return changed;
    }

    private void ExposeLegacySlider(string id, string label, UiRect bounds, float value, float minimum, float maximum,
        float smallChange, bool vertical, bool integer)
    {
        var publishedValue = integer ? Math.Clamp(MathF.Round(value), minimum, maximum) : value;
        ExposeAccessibility(id, _accessibilityParent, UiAccessibilityRole.Slider, label, bounds,
            focusable : false, valuePattern : false,
            range : new UiAccessibilityRangeInfo(publishedValue, minimum, maximum, smallChange,
                ((double)maximum - minimum) / 10) { Vertical = vertical });
    }
}
