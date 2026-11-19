using System.Text.Json;
using Duxel.Core;

internal static class CardSelectionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiFluentColorScheme scheme, UiContext context, Action<UiVector2, bool, bool> pointer)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CardInteractionPalette.json")));
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = false;
        using var card = Fluent.Card("Selectable pixels", Fluent.Body1(""), new(Width: 96, Height: 64),
            selection: new(() => selected, value => selected = value));
        UiRasterTextureStatistics shadows = default;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16)); card.Render(ui); shadows = ui.ShadowTextureStatistics;
        }
        int At(int x, int y) => (y * stride + x) * 4;
        byte[] Color(UiCardAppearance appearance, string state)
        {
            var value = reference.RootElement.GetProperty(scheme.ToString()).GetProperty(appearance.ToString()).GetProperty(state).GetString()!;
            return value == "transparent" ? [30, 70, 90] : Convert.FromHexString(value.AsSpan(1));
        }
        UiAccessibilityElement Root()
            => tree.TryGet(tree.Root.First!, out var node) ? node : throw new InvalidOperationException("Selected Card GPU root missing.");
        try
        {
            foreach (var appearance in Enum.GetValues<UiCardAppearance>())
            {
                context.State.NavCursorVisible = false;
                pointer(new(-100, -100), false, false);
                selected = false;
                card.Options = new(Appearance: appearance, Width: 96, Height: 64);
                var resting = capture("selection-off-" + appearance, Draw);
                var root = Root();
                var body = root.Node.Bounds;
                if (!tree.TryGet(root.FirstChild!, out var checkbox)) throw new InvalidOperationException("Selected Card GPU checkbox missing.");
                check(!root.Node.Focusable && checkbox.Node.Role == UiAccessibilityRole.Checkbox && checkbox.Node.Focusable,
                    "GPU selectable Card publishes the real checkbox focus target instead of a group trap : " + appearance);
                check(tree.RequestAction(checkbox.Node.Id, checkbox.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
                    "GPU selectable Card accepts the actual automation Toggle : " + appearance);
                capture("selection-toggle-event-" + appearance, Draw);
                var pixels = capture("selection-on-" + appearance, Draw);
                var x = (int)body.X;
                var y = (int)body.Y;
                check(selected && pixels.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(Color(appearance, "selected")),
                    "GPU requested followup frame paints the committed selected background from pinned tokens : " + appearance);
                check(pixels.AsSpan(At(x + 48, y), 3).SequenceEqual(Color(appearance, "selectedStroke")),
                    "GPU selected Card top border matches the independent pinned stroke : " + appearance);
                var uploads = shadows.Uploads;
                check(pixels.AsSpan().SequenceEqual(capture("selection-on-replay-" + appearance, Draw)) && shadows.Uploads == uploads,
                    "GPU selected Card replays unchanged pixels without another shadow upload : " + appearance);
                check(Root().Node.Bounds == body && context.State.NextScheduledFrameTimeSeconds is null,
                    "GPU Card selection preserves body allocation and schedules no periodic timer : " + appearance);
                var point = new UiVector2(body.X + body.Width - 5, body.Y + body.Height - 5);
                pointer(point, false, false);
                var hover = capture("selection-on-hover-" + appearance, Draw);
                check(hover.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(Color(appearance, "selected"))
                    && hover.AsSpan(At(x + 48, y), 3).SequenceEqual(Color(appearance, "selectedHoverStroke")),
                    "GPU selected Card hover preserves selected background and source-specific stroke priority : " + appearance);
                pointer(point, true, true);
                var pressed = capture("selection-on-pressed-" + appearance, Draw);
                check(pressed.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(Color(appearance, "selected"))
                    && pressed.AsSpan(At(x + 48, y), 3).SequenceEqual(Color(appearance, "selectedPressedStroke")),
                    "GPU selected Card pressed state follows its pinned selected styling : " + appearance);
                pointer(new(-100, -100), false, false);
                selected = false;
                check(resting.AsSpan().SequenceEqual(capture("selection-off-restored-" + appearance, Draw)),
                    "GPU external selection reset restores exact original unselected pixels : " + appearance);
            }
        }
        finally { pointer(new(-100, -100), false, false); context.SetAccessibilityTree(null); }
    }
}
