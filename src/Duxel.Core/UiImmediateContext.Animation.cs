namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public float AnimateFloat(string id, float target, float durationSeconds = 0.16f, UiAnimationEasing easing = UiAnimationEasing.OutCubic)
    {
        id ??= string.Empty;
        var key = ResolveId($"anim/{id}");
        return _state.AnimateFloat(key, target, durationSeconds, easing);
    }

    public float AnimateToggleRotationDegrees(
        string id,
        bool expanded,
        float collapsedDegrees = 0f,
        float expandedDegrees = 90f,
        float durationSeconds = 0.16f,
        UiAnimationEasing easing = UiAnimationEasing.OutCubic)
    {
        var target = expanded ? expandedDegrees : collapsedDegrees;
        return AnimateFloat($"{id}##rotate", target, durationSeconds, easing);
    }
}
