using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabOverflowBodies : UiWidget
{
    private readonly UiFluentTabList _tabs;
    private readonly (string Key, UiWidget Body)[] _bodies;
    internal TabOverflowBodies(UiFluentTabList tabs, UiTabOption[] items, UiState<int>[] counts)
    {
        if (counts.Length != items.Length)
            throw new ArgumentException("The tab example requires one counter per panel.", nameof(counts));
        _tabs = tabs;
        var counterFormat = GalleryText.Get("Tab panel {0}: {1}");
        _bodies = items.Select((item, index) =>
        {
            var count = counts[index];
            var button = Fluent.Button(string.Format(counterFormat, item.Label, count.Value));
            button.Activated = () => button.Label = string.Format(counterFormat, item.Label, ++count.Value);
            OwnChild(item.Value, button);
            return (item.Value, (UiWidget)button);
        }).ToArray();
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var size = default(UiVector2);
        foreach (var entry in _bodies)
        {
            var measured = context.MeasureConstrained(entry.Body, context.LayoutConstraints);
            size = new(MathF.Max(size.X, measured.X), MathF.Max(size.Y, measured.Y));
        }
        return size;
    }
    protected override IUiView Build(UiBuildContext context)
    {
        foreach (var entry in _bodies)
            context.Child(entry.Key, () => entry.Body);
        return new View(this);
    }
    private sealed class View(TabOverflowBodies owner) : IUiView
    {
        public void Render(UiImmediateContext ui)
        {
            foreach (var entry in owner._bodies)
                owner._tabs.RenderPanel(ui, entry.Key, entry.Body);
        }
    }
}
