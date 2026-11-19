using Duxel.Core;

internal static class OwnedInlineTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
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

        using var title = Fluent.MessageBarTitle("한글 제목");
        using var body = Fluent.MessageBarBody("본문 and English", title);
        Frame(ui =>
        {
            var position = ui.GetCursorScreenPos();
            check(body.Measure(ui, new(400, 300)).Y == 20 && ui.GetCursorScreenPos() == position,
                "Owned title and body measure as one pure inline line");
            check(tree.CaptureSnapshot().Elements.Count == 0, "Owned inline measurement publishes no fake accessibility nodes");
        });
        using var sized = Dux.SizedBox(body, 300, 140);
        Frame(sized.Render);
        check(body.VisualLines.NativeParagraph is not null,
            "Text-only owned MessageBar children use one native styled paragraph");
        var initialTitle = Find("한글 제목");
        var initialBody = Find("본문 and English");
        check(title.Weight == UiInlineTextWeight.Semibold && ReferenceEquals(body.Title, title)
            && body.Children.Length == 2 && ReferenceEquals(body.Children[0], title),
            "MessageBar owns its actual semibold title and body span rather than copying a metadata-only title");
        check(initialTitle.Node.Bounds.Y == initialBody.Node.Bounds.Y && initialBody.Node.Bounds.X > initialTitle.Node.Bounds.X,
            "Owned title and body continue along the same baseline");
        check(initialTitle.Node.Id == title.LastAccessibilityId && initialTitle.Node.Role == UiAccessibilityRole.Label
            && !initialTitle.Node.Focusable && !initialTitle.Node.ValuePattern,
            "The actual title widget publishes its own passive identity through its normal render lifetime");
        var beforeDirection = body.VisualLines.NativeParagraph;
        body.RightToLeft = true;
        Frame(sized.Render);
        var directed = body.VisualLines.NativeParagraph;
        check(directed is not null && !ReferenceEquals(beforeDirection, directed)
            && Find(title.Text).Node.Id == initialTitle.Node.Id,
            "Owned paragraph direction replaces layout while preserving actual child identity");
        body.RightToLeft = true;
        Frame(sized.Render);
        check(ReferenceEquals(directed, body.VisualLines.NativeParagraph),
            "Unchanged owned paragraph direction retains native layout");
        body.RightToLeft = false;
        Frame(sized.Render);
        var builds = body.VisualLines.Builds;
        Frame(sized.Render);
        Frame(sized.Render);
        check(body.VisualLines.Builds == builds, "Stable owned inline replay retains projection and visual line measurements");
        body.Text = "Changed 본문";
        Frame(sized.Render);
        check(Find("Changed 본문").Node.Id == initialBody.Node.Id && Find("한글 제목").Node.Id == initialTitle.Node.Id,
            "Body text replacement invalidates its owner while preserving actual child identities");
        title.Text = "더 긴 제목으로 바꿉니다";
        sized.Options = new(90, 240);
        Frame(sized.Render);
        check(body.VisualLines.Count > 1 && Find(title.Text).Node.Bounds.Height >= 40,
            "A long owned title wraps into real fragments instead of becoming an indivisible metadata label");
        check(Find(title.Text).Node.Id == initialTitle.Node.Id && body.ChildBounds(1).Y >= body.ChildBounds(0).Y,
            "Wrapped title fragments retain identity and the following body source order");
        sized.Options = new(300, 300);
        Frame(sized.Render);
        var stableTitleId = Find(title.Text).Node.Id;
        title.Margin = new(7, 11, 13, 17);
        title.Transform = new(RotationDegrees: 90);
        UiVector2 extent = default;
        Frame(ui => { extent = title.MeasureConstrained(ui, new(0, 300, 0, float.PositiveInfinity)); sized.Render(ui); });
        check(body.VisualLines.NativeParagraph is { Objects.Length: 1 },
            "Promoting a transformed title to an atomic widget reserves a real native paragraph object");
        check(MathF.Abs(body.ChildBounds(0).Width - extent.X) < .01f && MathF.Abs(body.ChildBounds(0).Height - extent.Y) < .01f,
            "A transformed and margined inline title reserves the common widget's actual measured bounds");
        check(body.ChildBounds(1).X >= body.ChildBounds(0).X + body.ChildBounds(0).Width - .01f,
            "Following text advances beyond the transformed title's reserved width");
        check(Find(title.Text).Node.Id == stableTitleId, "Switching a title between batched text and atomic widget modes preserves native identity");
        title.Margin = default;
        title.Transform = UiWidgetTransform.Identity;
        Frame(sized.Render);
        check(Find(title.Text).Node.Id == stableTitleId, "Returning to inline text keeps the same native title identity");
        check(body.VisualLines.NativeParagraph is not null,
            "Restoring a text-only owned flow rebuilds native paragraph geometry after atomic composition");

        using var a = new UiInlineTextSpan("A");
        using var v = new UiInlineTextSpan("V");
        using var kerning = new UiInlineFlowWidget([a, v]);
        Frame(ui =>
        {
            var natural = kerning.Measure(ui, new(300, 100));
            var expected = new UiControlContext(ui).MeasureTypography<UiRegularTypeface, UiFluentType300>("AV");
            check(natural == expected && kerning.VisualLines.StyledSource!.Projection.Runs.Length == 1,
                "Equal-face owned spans share shaping and kerning across real child boundaries");
            kerning.Render(ui);
        });
        check(Find("A").Node.Id != Find("V").Node.Id, "Merged shaping does not merge the owned children's distinct accessibility identities");
        using var invalidBoundary = new UiInlineFlowWidget([new UiInlineTextSpan("A"), new UiInlineTextSpan("\u0301")]);
        Frame(ui => check(Throws<InvalidOperationException>(() => invalidBoundary.Measure(ui, new(300, 100))),
            "Owned native child boundaries cannot split one Unicode text element"));

        var linkCalls = 0;
        using var link = Fluent.Link("Learn more", new Uri("https://example.com/"), _ => linkCalls++);
        using var linked = Fluent.MessageBarBody("Details", Fluent.MessageBarTitle("Notice"), link);
        using var bar = Fluent.MessageBar("Status", linked, options: new(Width: 400));
        Frame(bar.Render);
        var group = Find("Status");
        check(Find("Notice").Node.ParentId == group.Node.Id && Find("Details").Node.ParentId == group.Node.Id
            && Find("Learn more").Node.ParentId == group.Node.Id,
            "Real inline title,body and link preserve the actual MessageBar native parent");
        var linkNode = Find("Learn more");
        tree.RequestAction(linkNode.Node.Id, linkNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(bar.Render);
        check(linkCalls == 1 && ReferenceEquals(linked.TrailingContent, link),
            "Trailing inline link invokes its actual owned control rather than painted pseudo text");
        linked.Text = string.Empty;
        Frame(bar.Render);
        check(linked.VisualLines.StyledSource!.Source == "Notice \uFFFC",
            "An empty body does not insert a second separator before its trailing widget");
        linked.Text = "Details";
        Frame(bar.Render);
        check(linked.VisualLines.StyledSource!.Source == "Notice Details \uFFFC",
            "Restoring body text restores exactly one separator before the trailing widget");

        var value = "base";
        using var input = Fluent.Input("Inline input", new UiBinding<string>(() => value, x => value = x), 100, 120);
        using var interactive = new UiInlineFlowWidget([new UiInlineTextSpan("Edit "), input, new UiInlineTextSpan(" after")]);
        Frame(interactive.Render);
        var inputNode = Find("Inline input");
        tree.RequestAction(inputNode.Node.Id, inputNode.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(interactive.Render);
        Frame(interactive.Render, idle with
        {
            CharEvents = [new('x')]
        });
        check(value.Contains('x') && Find("Inline input").Node.Value == value,
            "An arbitrary inline input retains real focus,editing and native value behavior");
        check(interactive.ChildBounds(2).X >= interactive.ChildBounds(1).X + interactive.ChildBounds(1).Width,
            "Literal text resumes beyond the actual inline input allocation");
        var label = Find("Edit ").Node.Bounds;
        var inputBounds = interactive.ChildBounds(1);
        check(label.Height == 20 && MathF.Abs(label.Y - (inputBounds.Height - 20) / 2) < .01f,
            "Native inline text uses its actual20px line box centered beside a taller widget");

        using var later = new UiInlineTextSpan("Before");
        using var mutate = Fluent.Button("Mutate later", () => { later.Text = "After"; later.Transform = new(RotationDegrees: 90); });
        using var changing = new UiInlineFlowWidget([mutate, later]);
        Frame(changing.Render);
        var before = Find("Before");
        var button = Find("Mutate later");
        tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(changing.Render);
        check(later.Text == "After" && Find("Before").Node.Id == before.Node.Id,
            "A callback changing a later span retains this frame's coherent batched text and native snapshot");
        Frame(changing.Render);
        check(Find("After").Node.Id == before.Node.Id && changing.ChildBounds(1).Height > 20,
            "The next frame applies the new text and rotated geometry without replacing native identity");

        using var shared = new UiInlineTextSpan("Shared");
        foreach (var height in new[] { 0f, 4f })
        {
            using var tiny = new UiInlineFlowWidget([Dux.SizedBox(width: 4, height: height)]);
            Frame(tiny.Render);
            check(tiny.VisualLines.NativeParagraph is { Objects.Length: 1 }
                && MathF.Abs(tiny.ChildBounds(0).Y - (tiny.VisualLines.LineHeight(0) - height) / 2) < .002f,
                "Native object-only flow centers tiny and zero-height widgets in the UI line : " + height);
        }
        check(Throws<ArgumentException>(() => _ = new UiInlineFlowWidget([shared, shared])),
            "Inline flow rejects duplicate ownership before adopting any child");
        using var owner = new UiInlineFlowWidget([shared]);
        using var available = new UiInlineTextSpan("Available");
        check(Throws<InvalidOperationException>(() => _ = new UiInlineFlowWidget([available, shared])),
            "Inline flow validates all existing owners before adopting an earlier available child");
        using var availableOwner = new UiInlineFlowWidget([available]);
        check(Throws<ArgumentOutOfRangeException>(() => later.Weight = (UiInlineTextWeight)3)
            && later.Text == "After" && later.Weight == UiInlineTextWeight.Regular,
            "Invalid span face replacement is atomic");
        Frame(_ => { });
        check(tree.CaptureSnapshot().Elements.Count == 0, "Unmounted owned inline children retire from accessibility");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "Owned inline text and objects create no recurring animation or idle frame schedule");
        sized.Dispose();
        check(Throws<ObjectDisposedException>(() => title.Text = "Disposed") && Throws<ObjectDisposedException>(() => body.Text = "Disposed"),
            "Disposing the body owner releases its actual title and body children");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
