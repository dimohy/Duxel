using Duxel.Core;

internal static class DynamicPresenceGroupTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, float delta = 1f / 60, UiInputState? input = null)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name);
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T) { return true; }
        }
        using var a = new Probe("Alpha");
        using var b = new Probe("Beta");
        using var c = new Probe("Gamma");
        using var group = new UiPresenceGroupWidget([a, b, c], new(Gap: 10, AnimateEnter: true), keys: ["a", "b", "c"]);
        Frame(group.Render);
        var aNode = Find("Alpha");
        var bNode = Find("Beta");
        var cNode = Find("Gamma");
        var oldY = cNode.Node.Bounds.Y;
        group.SetItems([new("c", c), new("a", a), new("b", b)]);
        check(ReferenceEquals(group.Children[0], a) && group.Items[0].Key == "c",
            "A requested keyed reorder preserves the already prepared physical snapshot");
        Frame(group.Render);
        check(Find("Gamma").Node.Bounds.Y == oldY && Find("Gamma").Node.Id == cNode.Node.Id,
            "Reordering starts at the actual previous position and retains keyed parent identity");
        Frame(group.Render, .3f);
        check(Find("Gamma").Node.Bounds.Y == 0 && Find("Alpha").Node.Id == aNode.Node.Id && Find("Beta").Node.Id == bNode.Node.Id,
            "A keyed reorder reaches its new order without replacing surviving identities");
        using var d = new Probe("Delta");
        group.SetItems([new("d", d), new("c", c), new("a", a), new("b", b)]);
        check(Throws<InvalidOperationException>(() => _ = Dux.SizedBox(d, 80, 32)),
            "New collection items transfer exclusive ownership when the update is accepted");
        Frame(group.Render);
        check(group.Children.Length == 4 && group.OpacityAt(0) == 0,
            "Dynamic insertion after mount receives the configured entry animation");
        Frame(group.Render, .3f);
        check(Find("Delta").Node.Bounds.Y == 0 && d.Disposals == 0,
            "An inserted real widget becomes live at its actual final allocation");
        tree.RequestAction(aNode.Node.Id, aNode.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(group.Render);
        var paints = a.Paints;
        group.SetItems([new("d", d), new("c", c), new("b", b)]);
        Frame(group.Render);
        check(a.Disposals == 0 && a.Paints == paints && tree.CaptureSnapshot().Elements.All(x => x.Node.Name != "Alpha"),
            "Actual collection removal immediately stops the child while retaining only its exit paint");
        check(context.State.FocusedId == bNode.Node.Id,
            "Keyed removal moves focus to the next surviving control in the previous order");
        Frame(group.Render, .125f);
        check(a.Disposals == 0, "A removed instance stays owned until its fade completes");
        Frame(group.Render, .15f);
        check(a.Disposals == 1 && group.Children.ToArray().All(child => !ReferenceEquals(child, a)),
            "Fade completion physically releases and disposes the removed child exactly once");
        Frame(group.Render, .3f);
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "Collection retirement and subsequent reflow end without recurring idle frames");
        using var replacement = new Probe("Replacement Alpha");
        group.SetItems([new("a", replacement), new("d", d), new("c", c), new("b", b)]);
        Frame(group.Render);
        Frame(group.Render, .3f);
        check(Find("Replacement Alpha").Node.Name == "Replacement Alpha" && a.Disposals == 1 && replacement.Disposals == 0,
            "A released key can own a new instance after the previous lifetime has ended");
        group.SetItems([new("a", replacement), new("d", d), new("b", b)]);
        Frame(group.Render);
        Frame(group.Render, .08f);
        group.SetItems([new("a", replacement), new("d", d), new("c", c), new("b", b)]);
        Frame(group.Render);
        check(c.Disposals == 0 && Find("Gamma").Node.Id == cNode.Node.Id,
            "The same key and instance can reverse an unfinished removal without losing identity");
        Frame(group.Render, .3f);
        using var spare = new Probe("Spare");
        check(Throws<ArgumentException>(() => group.SetItems([new("spare", spare), new("b", b), new("b", c)]))
            && group.Items.Length == 4,
            "Duplicate desired keys reject the entire update before changing the requested collection");
        using var spareOwner = Dux.SizedBox(spare, 100, 32);
        check(ReferenceEquals(spareOwner.Child, spare), "Failed collection validation does not adopt an earlier valid candidate");
        using var wrong = new Probe("Wrong Alpha");
        check(Throws<InvalidOperationException>(() => group.SetItems([new("a", wrong)])) && wrong.Disposals == 0,
            "An owned key cannot silently replace its live or exiting instance");
        using var e = new Probe("Never rendered");
        using var f = new Probe("Final request");
        group.SetItems([new("e", e), new("a", replacement), new("d", d), new("c", c), new("b", b)]);
        group.SetItems([new("f", f), new("a", replacement), new("d", d), new("c", c), new("b", b)]);
        Frame(group.Render);
        check(e.Paints == 0 && e.Disposals == 1 && group.Items[0].Key == "f",
            "An adopted but superseded pending item is retired without a visual flash or a lifetime leak");
        Frame(group.Render, .3f);

        using var trigger = new Probe("Mutator");
        using var later = new Probe("Later");
        using var changing = new UiPresenceGroupWidget([trigger, later], keys: ["trigger", "later"]);
        trigger.Activated = () => changing.SetItems([new("trigger", trigger)]);
        Frame(changing.Render);
        var mutator = Find("Mutator");
        var beforeLater = Find("Later");
        var beforePaints = later.Paints;
        tree.RequestAction(mutator.Node.Id, mutator.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(changing.Render);
        check(changing.Items.Length == 1 && later.Paints == beforePaints + 1 && Find("Later").Node.Id == beforeLater.Node.Id,
            "A callback mutating the collection preserves later children in the current prepared frame");
        Frame(changing.Render);
        check(later.Disposals == 0 && tree.CaptureSnapshot().Elements.All(x => x.Node.Name != "Later"),
            "The next frame applies the queued removal and immediately retires its native node");
        trigger.Activated = () => changing.SetItems([new("trigger", trigger), new("later", later)]);
        mutator = Find("Mutator");
        tree.RequestAction(mutator.Node.Id, mutator.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(changing.Render, .3f);
        check(later.Disposals == 0,
            "A revival accepted during the completion frame prevents disposal of the newly requested lifetime");
        Frame(changing.Render);
        check(Find("Later").Node.Id == beforeLater.Node.Id, "Completion-frame revival keeps the original child identity");
        changing.Options = changing.Options with
        {
            ReducedMotion = true
        };
        changing.SetItems([]);
        Frame(changing.Render);
        check(trigger.Disposals == 1 && later.Disposals == 1 && changing.Children.Length == 0,
            "Reduced motion physically retires all removed children after the coherent render pass");
        Frame(changing.Render);
        check(context.State.NextScheduledFrameTimeSeconds is null, "An empty dynamic group stops scheduling after its cleanup frame");
        using var pending = new Probe("Pending ownership");
        var pendingGroup = new UiPresenceGroupWidget([]);
        pendingGroup.SetItems([new("pending", pending)]);
        pendingGroup.Dispose();
        check(pending.Disposals == 1, "Disposing before traversal still releases every accepted pending child");
        using var message = Fluent.MessageBar("Actual message", Fluent.MessageBarBody("Body"));
        using var messages = Fluent.MessageBarGroup([message], keys: ["message"]);
        check(Throws<ArgumentException>(() => messages.SetItems([new("wrong", wrong)])) && messages.Items.Length == 1,
            "Dynamic MessageBarGroup updates retain the actual-message content contract");
        foreach (var key in new[] { UiKey.Enter, UiKey.Space })
        {
            using var close = new Probe("Dismiss " + key);
            using var after = new Probe("After " + key);
            using var keyboard = new UiPresenceGroupWidget([close, after], keys: ["close", "after"]);
            var activations = 0;
            close.Activated = () => { activations++; keyboard.SetItems([new("after", after)]); };
            Frame(keyboard.Render);
            var node = Find("Dismiss " + key);
            tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            Frame(keyboard.Render);
            Frame(keyboard.Render, input: idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            if (key == UiKey.Space)
                check(activations == 0, "Keyboard dismissal waits for Space release before changing the collection");
            Frame(keyboard.Render, input: idle with
            {
                KeyEvents = [new(key, false, default)]
            });
            Frame(keyboard.Render);
            check(activations == 1 && keyboard.Items.Length == 1 && context.State.FocusedId == Find("After " + key).Node.Id,
                "Real keyboard dismissal invokes once and restores surviving focus : " + key);
            Frame(keyboard.Render, .3f);
            check(close.Disposals == 1, "Keyboard dismissal ends the actual removed lifetime : " + key);
        }
        var shortChild = Dux.SizedBox(Fluent.Button("Short reordered child"), 120, 32);
        var tallChild = Dux.SizedBox(Fluent.Button("Tall reordered child"), 120, 68);
        var unequal = new UiPresenceGroupWidget([shortChild, tallChild], new(Gap : 5, ReducedMotion : true), keys : ["short", "tall"]);
        using (var parent = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), unequal))
        {
            Frame(parent.Render); Frame(parent.Render);
            unequal.SetItems([new("tall", tallChild), new("short", shortChild)]);
            Frame(parent.Render); Frame(parent.Render);
            check(ReferenceEquals(unequal.Children[0], tallChild) && unequal.LastBounds[0].Height == 68
                && unequal.LastBounds[1].Height == 32 && unequal.LastBounds[1].Y == 73,
                "Same-count heterogeneous reorder invalidates prepared child bounds beneath a retained parent");
            var replacementChild = Dux.SizedBox(Fluent.Button("Replacement reordered child"), 120, 52);
            unequal.SetItems([new("replacement", replacementChild), new("tall", tallChild)]);
            Frame(parent.Render); Frame(parent.Render);
            check(unequal.Children.Length == 2 && unequal.LastBounds[0].Height == 52 && unequal.LastBounds[1].Height == 68,
                "Equal-count keyed replacement also rebuilds allocation geometry for actual child identities");
        }
        group.Dispose();
        check(a.Disposals == 1 && b.Disposals == 1 && c.Disposals == 1 && d.Disposals == 1
            && replacement.Disposals == 1 && f.Disposals == 1 && e.Disposals == 1,
            "Group disposal releases active and previously retired instances exactly once");
    }
    private sealed class Probe(string label) : UiFluentButton(label, identity: UiWidgetIdentity.Parent)
    {
        internal int Disposals;
        internal int Paints;
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            base.RenderControl(ui);
        }
        protected override void OnDispose()
        {
            Disposals++;
            base.OnDispose();
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
