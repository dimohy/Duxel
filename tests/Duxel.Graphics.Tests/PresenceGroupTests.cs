using Duxel.Core;

internal static class PresenceGroupTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, float delta = 1f / 60)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name);
        var calls = 0;
        using var first = Fluent.Button("First", () => calls++);
        using var second = Fluent.Button("Second");
        using var outside = Fluent.Button("Outside");
        using var group = new UiPresenceGroupWidget([first, second], new(Gap: 10));
        void Draw(UiImmediateContext ui)
        {
            group.Render(ui);
            outside.Render(ui);
        }
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            group.Measure(ui, new(500, 400));
            check(cursor == ui.GetCursorScreenPos() && tree.CaptureSnapshot().Elements.Count == 0,
                "Presence measurement neither paints nor publishes native controls");
        });
        Frame(Draw);
        var initial = Find("First");
        var secondId = Find("Second").Node.Id;
        var originalY = group.LastBounds[1].Y;
        check(group.OpacityAt(0) == 1 && group.OpacityAt(1) == 1 && context.State.NextScheduledFrameTimeSeconds is null,
            "Initial presence has no entry animation or recurring idle deadline");
        tree.RequestAction(initial.Node.Id, initial.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(Draw);
        check(context.State.FocusedId == initial.Node.Id, "A live presence child retains native keyboard focus");
        group.SetVisible(0, false);
        Frame(Draw);
        check(tree.CaptureSnapshot().Elements.All(x => x.Node.Name != "First") && context.State.FocusedId == secondId,
            "Dismissal immediately retires live accessibility and moves focus to the next surviving control");
        check(group.OpacityAt(0) == 1 && group.LastBounds[1].Y == originalY && context.State.NextScheduledFrameTimeSeconds is not null,
            "Exit starts from the last paint while preserving its space for the finite fade");
        Frame(Draw, .125f);
        check(MathF.Abs(group.OpacityAt(0) - .5f) < .001f && calls == 0,
            "Exit samples the shared linear timeline without executing the frozen child");
        Frame(Draw, .15f);
        check(group.OpacityAt(0) == 0 && group.LastBounds[0] == default && group.LastBounds[1].Y == originalY,
            "Completed fade removes layout occupancy and starts reflow at the previous visible position");
        Frame(Draw, .125f);
        check(group.LastBounds[1].Y > 0 && group.LastBounds[1].Y < originalY,
            "The surviving child moves through intermediate coordinates using the common reflow transition");
        Frame(Draw, .15f);
        check(group.LastBounds[1].Y == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "Settled reflow has exact final bounds and no idle render schedule");
        group.SetVisible(0, true);
        Frame(Draw);
        check(Find("First").Node.Id == initial.Node.Id && group.OpacityAt(0) == 1,
            "Default exit-only reopening retains the actual child identity without an entry fade");
        group.Options = group.Options with
        {
            ReducedMotion = true
        };
        group.SetVisible(0, false);
        Frame(Draw);
        check(group.LastBounds[1].Y == 0 && group.OpacityAt(0) == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "Reduced motion applies removal and reflow immediately without recurring frames");
        var last = Find("Second");
        tree.RequestAction(last.Node.Id, last.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(Draw);
        group.SetVisible(1, false);
        Frame(Draw);
        check(context.State.FocusedId == Find("Outside").Node.Id,
            "Removing the last focused message chooses a surviving control outside the group");
        group.Options = new(Gap: 10, AnimateEnter: true);
        group.SetVisible(0, true);
        Frame(Draw);
        check(group.OpacityAt(0) == 0 && group.LastBounds[0].Y < 0,
            "Explicit entry motion begins transparent above the allocated row");
        Frame(Draw, .125f);
        var entryAlpha = group.OpacityAt(0);
        group.SetVisible(0, false);
        Frame(Draw, .01f);
        check(group.OpacityAt(0) >= entryAlpha && group.OpacityAt(0) < 1,
            "Reversing entry to exit starts at the sampled opacity rather than snapping");
        Frame(Draw, .3f);
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "Interrupted presence also reaches a finite idle state");
        using var bar = Fluent.MessageBar("Message", Fluent.MessageBarBody("Body", Fluent.MessageBarTitle("Title")));
        using var bars = Fluent.MessageBarGroup([bar]);
        Frame(bars.Render);
        check(bars.Children.Length == 1 && ReferenceEquals(bars.Children[0], bar),
            "Generated MessageBarGroup owns actual MessageBar roots");
        using var invalid = Fluent.Button("Not a message");
        var rejected = false;
        try
        {
            using var wrong = Fluent.MessageBarGroup([invalid]);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "MessageBarGroup rejects wrapped or unrelated children before adoption");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
