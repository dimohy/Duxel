using Duxel.Core;

internal static class ProgressAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        using var progress = new UiProgress<UiFluentProgressBehavior, UiFluentProgressPresentation>(1, 4)
        {
            AccessibleName = "진행 상태",
            Size = new(200, 4),
            Shape = UiProgressShape.Square
        };
        void Frame(Action<UiImmediateContext>? render = null)
        {
            context.NewFrame(new(.1f, new(320, 200), new(1, 1)), idle, new(0, 0, 320, 200), UiTextSettings.Default);
            context.Render(new Screen(render ?? progress.Render));
        }
        void Release() => context.GetDrawData().ReleasePooled();
        UiAccessibilityElement Element() => tree.CaptureSnapshot().Elements.Single();
        Frame();
        Release();
        var first = Element();
        check(first.Node is { Role: UiAccessibilityRole.ProgressBar, Name: "진행 상태", ReadOnly: true, Focusable: false, ValuePattern: false }
            && first.Node.Range == new UiAccessibilityRangeInfo(1, 0, 4, 0, 0),
            "Progress publishes its logical range and readonly nonfocusable semantic identity");
        check(tree.RequestRangeValue(first.Node.Id, first.RuntimeId, 2, wait: false) == UiAccessibilityResult.ReadOnly,
            "Progress rejects numeric writes before queueing a render action");
        var rangeEvents = 0;
        tree.RangeChanged += _ => rangeEvents++;
        progress.Value = 3;
        Frame();
        var afterChange = Element();
        using (var data = context.GetDrawData())
        {
            var fills = data.DrawLists.SelectMany(list => list.RectFilledPrimitives?.ToArray() ?? [])
                .Where(rect => rect.Color == context.GetStyle().ProgressBarFill).ToArray();
            check(afterChange.Node.Range?.Value == 3 && fills.Length == 1 && Math.Abs(fills[0].Rect.Width - 50) < .001,
                "Animated progress exposes its target while the visual fill still starts at the previous value");
        }
        var revision = tree.Revision;
        Frame();
        Release();
        check(tree.Revision == revision && rangeEvents == 1 && Element().RuntimeId == first.RuntimeId,
            "Visual progress interpolation does not publish repeated numeric events or change native identity");
        for (var i = 0; i < 4; i++)
        {
            Frame();
            Release();
        }
        requests = 0;
        Frame();
        Release();
        check(!context.State.HasActiveAnimations && requests == 0, "A completed accessible progress transition leaves the event loop idle");
        progress.Value = null;
        Frame();
        Release();
        check(Element().RuntimeId == first.RuntimeId && Element().Node.Range is null && Element().Node.ReadOnly,
            "Indeterminate progress retains identity without inventing a numeric zero");
        check(tree.RequestRangeValue(first.Node.Id, first.RuntimeId, 2, wait: false) == UiAccessibilityResult.InvalidOperation,
            "Indeterminate progress omits the numeric write contract");
        progress.Value = 10;
        Frame();
        Release();
        check(Element().Node.Range?.Value == 4 && progress.Value == 10,
            "Progress semantics clamp above the maximum without changing the source");
        progress.Value = -2;
        Frame();
        Release();
        check(Element().Node.Range?.Value == 0 && progress.Value == -2,
            "Progress semantics clamp below zero without changing the source");
        var reads = 0;
        using var dynamic = Fluent.ProgressBar(() => { reads++; return new(2, 4); });
        Frame(dynamic.Render);
        Release();
        check(reads == 1 && Element().Node.Range?.Value == 2, "Progress accessibility reuses the frame's one source evaluation");
        Frame(ui =>
        {
            ui.ProgressBar<UiFluentProgressPresentation>("first", new(1, 4), new(120, 4));
            ui.ProgressBar<UiFluentProgressPresentation>("second", new(3, 4), new(120, 4));
        });
        Release();
        check(tree.CaptureSnapshot().Elements.Count == 2 && tree.CaptureSnapshot().Elements.Select(e => e.RuntimeId).Distinct().Count() == 2,
            "Explicit immediate progress identifiers publish independent accessible elements");
        Frame(ui =>
        {
            ui.ProgressBar<UiFluentProgressPresentation>(new(1, 4), new(120, 4));
            ui.ProgressBar<UiFluentProgressPresentation>(new(3, 4), new(120, 4));
        });
        Release();
        check(tree.CaptureSnapshot().Elements.Count == 0, "Existing id-free paint calls preserve their identity-free contract without colliding");
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                ui.ProgressBar<UiFluentProgressPresentation>("", new(1, 4));
            }
            catch (ArgumentException) { rejected = true; }
        });
        Release();
        check(rejected && tree.CaptureSnapshot().Elements.Count == 0, "Explicit progress identity rejects an empty identifier before publication");
        var behavior = default(UiFluentProgressBehavior);
        Frame(ui => ui.ProgressBar<UiFluentProgressBehavior, UiFluentProgressPresentation>("animated", ref behavior, new(1, 4), new(120, 4)));
        Release();
        var animatedId = Element().RuntimeId;
        Frame(ui => ui.ProgressBar<UiFluentProgressBehavior, UiFluentProgressPresentation>("animated", ref behavior, new(3, 4), new(120, 4)));
        Release();
        check(Element().RuntimeId == animatedId && Element().Node.Range?.Value == 3,
            "Explicit immediate animated progress preserves its identifier and exposes the target value");
        using var firstChild = new UiProgress<UiFluentProgressPresentation>(1, 4, UiWidgetIdentity.Parent) { Size = new(60, 4) };
        using var secondChild = new UiProgress<UiFluentProgressPresentation>(3, 4, UiWidgetIdentity.Parent) { Size = new(90, 4) };
        using var pair = new UiStackWidget([firstChild, secondChild]);
        Frame(pair.Render);
        Release();
        var children = tree.CaptureSnapshot().Elements.ToArray();
        check(children.Length == 2 && children[0].Node.Id != children[1].Node.Id
            && children[0].Node.Range?.Value == 1 && children[1].Node.Range?.Value == 3,
            "Parent-owned progress siblings retain independent semantic identities and values");
        Frame(pair.Render);
        Release();
        check(tree.CaptureSnapshot().Elements.Select(e => e.RuntimeId).SequenceEqual(children.Select(e => e.RuntimeId)),
            "Parent-owned progress identities remain stable across retained layout frames");
        requests = 0;
        Frame(ui => { ui.SetCursorScreenPos(new(1000, 1000)); pair.Render(ui); });
        Release();
        check(tree.CaptureSnapshot().Elements.All(e => e.Node.Offscreen && e.Node.Range.HasValue) && requests == 0,
            "Clipped parent-owned progress retains numeric metadata without scheduling frames");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
