using Duxel.Core;

internal static class MessageBarTests
{
    internal static void Run(Action<bool, string> check)
    {
        MessageBarReference.Check(check);
        check(Fluent.ComponentNames.Contains("MessageBar") && Fluent.ComponentNames.Contains("MessageBarActions"),
            $"Actual MessageBar factories are cataloged : names = {Fluent.ComponentNames.Length}, missing parents = {Fluent.MissingComponentNames.Length}");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiTextSettings? settings = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), settings ?? UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T) { return true; }
        }
        var body = new Probe(new(40, 20));
        var actions = new Probe(new(50, 24));
        var dismiss = new Probe(new(24, 24));
        using var bar = Fluent.MessageBar("Status", body, actions, dismiss);
        Frame(ui => check(bar.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, 480)) == new UiVector2(192, 36),
            "MessageBar natural width includes the actual body,icon,actions,container action and source padding"));
        check(body.Paints + actions.Paints + dismiss.Paints == 0, "MessageBar pure measurement never paints or processes child input");
        using var parent = Dux.SizedBox(bar, 300, 36);
        Frame(parent.Render);
        check(body.Bounds == new UiRect(41, 8, 148, 20), "MessageBar body grows inside the remaining single-line column");
        check(actions.Bounds == new UiRect(201, 6, 50, 24) && dismiss.Bounds == new UiRect(263, 6, 24, 24),
            "MessageBar secondary and container actions retain independent trailing margins and vertical centers");
        check(bar.LastLayout.Icon == new UiRect(13, 8, 20, 20) && !bar.LastLayout.Multiline,
            "MessageBar uses the official20px intent icon and36px single-line root");
        check(ReferenceEquals(bar.Body, body) && ReferenceEquals(bar.Actions, actions) && ReferenceEquals(bar.ContainerAction, dismiss),
            "MessageBar exposes actual owned slot instances");
        var measures = body.Measures + actions.Measures + dismiss.Measures;
        Frame(parent.Render);
        check(body.Measures + actions.Measures + dismiss.Measures == measures,
            "MessageBar stable replay retains the children's real measurements");
        parent.Options = new(160, 78);
        Frame(parent.Render);
        check(bar.LastLayout.Multiline && body.Bounds == new UiRect(41, 11, 70, 20),
            "Automatic MessageBar reflows from actual natural content widths");
        check(actions.Bounds == new UiRect(97, 45, 50, 24) && dismiss.Bounds == new UiRect(123, 11, 24, 24),
            "Multiline moves secondary actions below the body and keeps the container action at the top");
        bar.Options = new(RightToLeft: true);
        Frame(parent.Render);
        check(body.Bounds == new UiRect(49, 11, 70, 20) && actions.Bounds == new UiRect(13, 45, 50, 24)
            && dismiss.Bounds == new UiRect(13, 11, 24, 24), "RTL mirrors actual slots without changing source navigation order");
        bar.Options = new(Layout: UiMessageBarLayoutMode.SingleLine);
        Frame(ui => check(Throws<InvalidOperationException>(() => bar.Measure(ui, new(160, 300))),
            "An impossible forced single line fails explicitly instead of silently losing content"));
        bar.Options = new(HideIcon: true);
        Frame(ui => check(bar.Measure(ui, new(170, 300)).Y == 36 && !bar.LastLayout.Multiline && bar.LastLayout.Icon.Width == 0,
            "Removing the icon removes its actual gap and allows a narrower single line"));
        bar.Options = new(Layout: UiMessageBarLayoutMode.MultiLine);
        parent.Options = new(300, 78);
        Frame(parent.Render);
        check(bar.LastLayout.Multiline, "Explicit multiline layout remains multiline at a wide allocation");
        body.Resize(new(60, 40));
        parent.Options = new(300, 94);
        Frame(parent.Render);
        check(bar.LastLayout.Body.Height == 40 && bar.LastLayout.Actions.Y == 61,
            "Child invalidation updates multiline height and action placement");
        Frame(ui => check(Throws<InvalidOperationException>(() => bar.Measure(ui, new(20, 300))),
            "Insufficient fixed slot capacity is rejected before rendering"));
        Frame(ui => check(Throws<InvalidOperationException>(() => bar.Measure(ui, new(300, 20))),
            "Insufficient height is rejected rather than clipping required content"));

        var changingBody = new Probe(new(200, 20));
        using var changingWidth = Fluent.MessageBar("Changing width", changingBody, options : new(Width : 270));
        Frame(changingWidth.Render);
        var intrinsicMeasures = changingBody.IntrinsicMeasures;
        changingWidth.Options = changingWidth.Options with { Width = 245 };
        Frame(changingWidth.Render);
        check(intrinsicMeasures > 0 && changingBody.IntrinsicMeasures == intrinsicMeasures
            && changingWidth.LastLayout.Size.X == 245 && changingWidth.LastLayout.Multiline,
            "Changing MessageBar width rearranges the body without repeating intrinsic measurement");
        changingBody.Resize(new(120, 20));
        Frame(changingWidth.Render);
        check(changingBody.IntrinsicMeasures > intrinsicMeasures && !changingWidth.LastLayout.Multiline,
            "Changing the owned body invalidates the retained intrinsic measurement and updates wrapping");

        using var shortBody = new UiInlineText([new("Title ", UiInlineTextWeight.Semibold), new("body")]);
        using var noActions = Fluent.MessageBar("No actions", shortBody);
        Frame(ui => check(noActions.Measure(ui, new(300, 300)).Y == 36, "A short literal body needs no phantom action row"));
        Frame(ui => check(noActions.Measure(ui, new(500, 300)).Y == 42 && !noActions.LastLayout.Multiline,
            "Text scale uses the actual Body1 line height when detecting automatic multiline"), UiTextSettings.Default with
            {
                Scale = 2
            });
        noActions.Options = new(Layout: UiMessageBarLayoutMode.MultiLine);
        Frame(ui => check(noActions.Measure(ui, new(300, 300)).Y == 40,
            "Multiline without actions retains the source bottom spacer only"));
        using var emptyActions = Fluent.MessageBarActions([]);
        using var emptyBody = new UiInlineText([new("Body")]);
        using var empty = Fluent.MessageBar("Empty actions", emptyBody, emptyActions,
            options: new(Layout: UiMessageBarLayoutMode.MultiLine));
        Frame(ui => check(empty.Measure(ui, new(300, 300)).Y == 40,
            "An empty action collection does not create phantom padding or an action row"));

        var retryCalls = 0;
        var closeCalls = 0;
        using var retry = Fluent.Button("Retry", () => retryCalls++);
        using var close = Fluent.Button("Dismiss", () => closeCalls++);
        using var actionGroup = Fluent.MessageBarActions([retry]);
        using var literalBody = new UiInlineText([new("한글 제목 ", UiInlineTextWeight.Semibold), new("본문")]);
        using var actual = Fluent.MessageBar("연결 상태", literalBody, actionGroup, close, new(Width: 400));
        Frame(actual.Render);
        var snapshot = tree.CaptureSnapshot().Elements.ToArray();
        var root = snapshot.Single(x => x.Node.Name == "연결 상태");
        check(root.Node.Role == UiAccessibilityRole.Group && !root.Node.Focusable && !root.Node.ValuePattern,
            "MessageBar is an explicitly named passive group rather than an editable input or alert dialog");
        check(snapshot.Where(x => x.Node.Name is "한글 제목 본문" or "Retry" or "Dismiss").All(x => x.Node.ParentId == root.Node.Id),
            "Real body and action controls retain the MessageBar accessibility parent");
        var retryNode = snapshot.Single(x => x.Node.Name == "Retry");
        tree.RequestAction(retryNode.Node.Id, retryNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(actual.Render);
        check(retryCalls == 1 && closeCalls == 0, "Secondary actions execute the actual owned button behavior independently");
        var closeNode = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Dismiss");
        tree.RequestAction(closeNode.Node.Id, closeNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(actual.Render);
        check(closeCalls == 1 && retryCalls == 1, "Container action executes its own control behavior independently");
        var textBuilds = literalBody.VisualLines.Builds;
        Frame(actual.Render);
        Frame(actual.Render);
        check(literalBody.VisualLines.Builds == textBuilds, "An unchanged MessageBar retains the actual mixed-weight body layout");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "Static MessageBar slots,icons and layout do not force recurring frames");

        using var wrapActions = Fluent.MessageBarActions([Fluent.Button("First action"), Fluent.Button("Second action")]);
        using var wrapBody = new UiInlineText([new("Body")]);
        using var wrapping = Fluent.MessageBar("Wrapping actions", wrapBody, wrapActions, options: new(Width: 160));
        Frame(wrapping.Render);
        var wrappedButtons = tree.CaptureSnapshot().Elements.Where(x => x.Node.Role == UiAccessibilityRole.Button).ToArray();
        check(wrapping.LastLayout.Multiline && wrappedButtons.Length == 2
            && wrappedButtons[0].Node.Name == "First action" && wrappedButtons[1].Node.Name == "Second action"
            && wrappedButtons[1].Node.Bounds.Y > wrappedButtons[0].Node.Bounds.Y,
            "Narrow MessageBar action rows really wrap owned buttons while preserving source navigation order");
        check(wrappedButtons.All(x => x.Node.Bounds.X >= 13 && x.Node.Bounds.X + x.Node.Bounds.Width <= 147)
            && wrapping.LastLayout.Size.Y >= wrappedButtons[1].Node.Bounds.Y + wrappedButtons[1].Node.Bounds.Height + 9,
            "Wrapped actions fit actual horizontal padding and expand the root instead of being clipped");

        using var directionalActions = Fluent.MessageBarActions([
            Fluent.Button("One"), Fluent.Button("Two"), Fluent.Button("Three")]);
        using var directional = Fluent.MessageBar("Directional actions", Fluent.MessageBarBody("Body"), directionalActions,
            options: new(Width: 400, RightToLeft: true));
        UiAccessibilityElement[] ActionNodes()
        {
            var snapshot = tree.CaptureSnapshot();
            var nodes = snapshot.Elements.ToDictionary(x => x.Node.Id);
            var root = snapshot.Elements.Single(x => x.Node.Name == "Directional actions");
            var actions = new List<UiAccessibilityElement>();
            for (var id = root.FirstChild; id is not null; id = nodes[id].Next)
                if (nodes[id].Node.Role == UiAccessibilityRole.Button)
                    actions.Add(nodes[id]);
            return actions.ToArray();
        }
        Frame(directional.Render);
        var rtlActions = ActionNodes();
        var rtlIds = rtlActions.Select(x => x.Node.Id).ToArray();
        check(rtlActions.Select(x => x.Node.Name).SequenceEqual(new[] { "One", "Two", "Three" })
            && rtlActions[0].Node.Bounds.X > rtlActions[1].Node.Bounds.X
            && rtlActions[1].Node.Bounds.X > rtlActions[2].Node.Bounds.X,
            "RTL MessageBar mirrors real action positions while retaining source accessibility order");
        directional.Options = directional.Options with
        {
            RightToLeft = false
        };
        Frame(directional.Render);
        var ltrActions = ActionNodes();
        check(ltrActions.Select(x => x.Node.Id).SequenceEqual(rtlIds)
            && ltrActions[0].Node.Bounds.X < ltrActions[1].Node.Bounds.X
            && ltrActions[1].Node.Bounds.X < ltrActions[2].Node.Bounds.X,
            "Toggling MessageBar direction preserves actual action identity and reverses placement");
        var narrowWidth = ltrActions[0].Node.Bounds.Width + ltrActions[1].Node.Bounds.Width + 12 + 26;
        directional.Options = directional.Options with
        {
            Width = narrowWidth,
            RightToLeft = true
        };
        Frame(directional.Render);
        var narrowActions = ActionNodes();
        check(directional.LastLayout.Multiline && narrowActions[0].Node.Bounds.Y == narrowActions[1].Node.Bounds.Y
            && narrowActions[0].Node.Bounds.X > narrowActions[1].Node.Bounds.X
            && narrowActions[2].Node.Bounds.Y > narrowActions[1].Node.Bounds.Y
            && narrowActions.All(x => x.Node.Bounds.X >= 13 && x.Node.Bounds.X + x.Node.Bounds.Width <= narrowWidth - 13),
            $"RTL wrapped actions mirror each row without reversing rows or exceeding MessageBar padding : width = {narrowWidth}, bounds = {string.Join(';', narrowActions.Select(x => x.Node.Bounds))}");

        var wrapFirst = new Probe(new(20, 10));
        var wrapSecond = new Probe(new(30, 15));
        using var directionalWrap = Dux.Wrap(new(Spacing: 5)
        {
            RightToLeft = true
        }, wrapFirst, wrapSecond);
        using var wrapParent = Dux.SizedBox(directionalWrap, 100, 30);
        Frame(wrapParent.Render);
        check(wrapFirst.Bounds == new UiRect(80, 0, 20, 10) && wrapSecond.Bounds == new UiRect(45, 0, 30, 15),
            "Common horizontal Wrap mirrors positions across the actual assigned width");
        var columnFirst = new Probe(new(20, 10));
        var columnSecond = new Probe(new(30, 15));
        using var directionalColumn = new UiWrapWidget<UiVerticalAxis>([columnFirst, columnSecond],
            new(Spacing: 5)
            {
                RightToLeft = true
            });
        using var columnParent = Dux.SizedBox(directionalColumn, 100, 40);
        Frame(columnParent.Render);
        check(columnFirst.Bounds == new UiRect(80, 0, 20, 10) && columnSecond.Bounds == new UiRect(70, 15, 30, 15),
            "Common vertical Wrap mirrors its cross axis without reversing vertical reading order");

        using var shared = new Probe(new(8, 8));
        check(Throws<ArgumentException>(() => _ = Fluent.MessageBar("Duplicate", shared, shared)),
            "MessageBar rejects duplicate child instances before adoption");
        using var owner = Fluent.MessageBar("Owner", shared);
        using var available = new Probe(new(8, 8));
        check(Throws<InvalidOperationException>(() => _ = Fluent.MessageBar("Invalid owner", available, shared)),
            "MessageBar preflights all slots before adopting an earlier available child");
        using var availableOwner = Fluent.MessageBar("Available", available);
        bar.Dispose();
        check(body.Released && actions.Released && dismiss.Released, "MessageBar disposal releases all exclusively owned slots");
        foreach (var invalid in new[] { new UiMessageBarOptions(Intent: (UiMessageBarIntent)4), new(Layout: (UiMessageBarLayoutMode)3),
            new(Shape: (UiMessageBarShape)2), new(Width: float.NaN) })
            check(Throws<ArgumentOutOfRangeException>(invalid.Validate), "Invalid MessageBar options fail explicitly");
    }

    private sealed class Probe(UiVector2 size) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal UiRect Bounds;
        internal int Measures, Paints, IntrinsicMeasures;
        internal bool Released;
        internal void Resize(UiVector2 value)
        {
            _size = value;
            RequestRebuild();
        }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measures++;
            if (float.IsPositiveInfinity(context.LayoutConstraints.MaxWidth))
                IntrinsicMeasures++;
            return context.LayoutConstraints.Constrain(_size);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var actual = new UiControlContext(ui).ConstrainToLayout(_size);
            var origin = ui.GetCursorScreenPos();
            Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            ui.Dummy(actual);
        }
        private protected override void ReleaseOwnedResources()
        {
            Released = true;
            base.ReleaseOwnedResources();
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
