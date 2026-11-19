namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    // Physical selection fragments are sorted and merged by the line geometry.
    // Paint each disjoint clip once, preserving the complete shaped text run.
    private void PaintSelectedInputRun(string text, UiVector2 position, UiColor foreground,
        UiColor selectedForeground, UiRect clip, bool typography,
        ReadOnlySpan<PlatformTextSelectionFragment> fragments, float scale = 1)
    {
        foreach (var region in new UiTextSelectionPaintRegions(clip, position.X, fragments, scale))
            PaintInputRun(text, position, region.Selected ? selectedForeground : foreground, region.Clip, typography);
    }
}
