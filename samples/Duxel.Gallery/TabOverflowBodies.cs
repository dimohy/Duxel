using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabOverflowBodies : UiWidget
{
    private readonly UiFluentTabList _tabs;
    private readonly (string Key, UiWidget Body)[] _bodies;
    internal TabOverflowBodies(UiFluentTabList tabs, UiState<int>[] counts)
    {
        if (counts.Length != 5)
            throw new ArgumentException("The tab example requires one counter for each of its five panels.", nameof(counts));
        _tabs = tabs;
        _bodies = new[] { ("home", "홈"), ("document", "문서"), ("analysis", "분석"), ("settings", "설정"), ("help", "도움말") }
            .Select((item, index) =>
            {
                var count = counts[index];
                var button = Fluent.Button($"넘침 본문 {item.Item2} : {count.Value}");
                button.Activated = () => button.Label = $"넘침 본문 {item.Item2} : {++count.Value}";
                OwnChild(item.Item1, button);
                return (item.Item1, (UiWidget)button);
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
