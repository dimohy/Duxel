using Duxel.Core;

internal static class ScrollViewTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var thickness = context.GetStyleData().ScrollbarSize;
        var idle = new UiInputState(new(30, 30), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null, UiVector2? display = null, UiTextSettings? textSettings = null)
        {
            var viewport = display ?? new UiVector2(320, 240);
            context.NewFrame(new(1f / 60, viewport, new(1, 1)), input ?? idle, new(0, 0, viewport.X, viewport.Y), textSettings ?? UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(10, 10));
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T)
            {
                return true;
            }
        }

        var child = new Probe(new(40, 200));
        using var scroll = Dux.ScrollView(child, new(Width : 100, Height : 60));
        scroll.Offset = new(0, 10000);
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            var storage = context.State.ScrollStorage;
            check(scroll.MeasureConstrained(ui, new()) == new UiVector2(100, 60), "scroll pure measurement reports its explicit finite viewport");
            check(ui.GetCursorScreenPos() == cursor && context.State.ScrollStorage == storage && child.Renders == 0 && scroll.Offset.Y == 10000,
                "scroll measurement preserves cursor, input storage, rendering and requested offset");
            check(child.LastConstraints == new UiBoxConstraints(100 - thickness, 100 - thickness, 0, float.PositiveInfinity),
                "vertical scrolling remeasures the natural child at the actual gutter-reduced width");
            scroll.Render(ui);
            check(ui.GetItemRectMin() == new UiVector2(10, 10) && ui.GetItemRectMax() == new UiVector2(110, 70),
                "scroll root consumes exactly one finite viewport allocation");
        });
        check(scroll.Offset == new UiVector2(0, 140) && child.Bounds == new UiRect(10, -130, 100 - thickness, 200),
            "rendering clamps the requested offset and places actual content in world coordinates");
        var measured = child.Measures;
        Frame(scroll.Render);
        check(child.Measures == measured, "unchanged owned scroll content reuses its retained measurement");
        scroll.Offset = default;
        Frame(scroll.Render);
        measured = child.Measures;
        Frame(scroll.Render, idle with { MouseWheel = -1 });
        check(scroll.Offset.Y > 0 && child.Measures == measured, "wheel movement changes owned offset without invalidating layout measurement");
        Frame(scroll.Render);
        check(child.Bounds.Y == 10 - scroll.Offset.Y, "the requested next frame places content at the wheel-updated offset");
        child.Change(new(40, 40));
        Frame(scroll.Render);
        check(scroll.Offset == default && child.Bounds == new UiRect(10, 10, 100, 40),
            "child invalidation shrinks content, removes the gutter and clamps the owned offset");
        check(context.State.ScrollStorage == (0, 0), "disappearing scrollbars release their auxiliary storage");
        scroll.Options = new(UiScrollAxis.Horizontal, 100, 60);
        child.Change(new(300, 40));
        Frame(scroll.Render, idle with { MouseWheelHorizontal = -1 });
        check(scroll.Offset.X > 0 && scroll.Offset.Y == 0 && child.LastConstraints == new UiBoxConstraints(0, float.PositiveInfinity, 60 - thickness, 60 - thickness),
            "horizontal scrolling gives its child an unbounded main axis and a gutter-reduced tight cross axis");
        scroll.Options = new(UiScrollAxis.Both, 100, 60);
        child.Change(new(300, 200));
        scroll.Offset = new(10000, 10000);
        Frame(scroll.Render);
        check(scroll.Offset == new UiVector2(200 + thickness, 140 + thickness) && child.LastConstraints == new UiBoxConstraints(),
            "two-axis scrolling reserves both gutters and clamps each owned offset independently");
        scroll.Options = new(UiScrollAxis.Both, 0, 0);
        Frame(scroll.Render, idle with { MouseWheel = -1, LeftMouseDown = true, LeftMousePressed = true });
        check(child.Clip.Width == 0 && child.Clip.Height == 0 && context.State.ActiveId is null,
            "zero scroll allocation clips content and cannot capture a scrollbar");
        scroll.Options = new(UiScrollAxis.Vertical, 100, 60);
        scroll.Offset = default;
        Frame(scroll.Render, idle with { MousePosition = new(102, 14), LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.ActiveId is not null, "owned scrollbar captures its own visible thumb");
        var oldId = context.State.ActiveId;
        Frame(scroll.Render, idle with { MousePosition = new(102, 40), LeftMouseDown = true });
        check(scroll.Offset.Y > 0 && context.State.ActiveId == oldId, "owned thumb dragging retains identity while moving content");
        scroll.Dispose();
        check(child.Disposals == 1 && context.State.ActiveId is null && context.State.ScrollStorage == (0, 0),
            "disposing a scroll view disposes its child once and releases drag ownership and auxiliary storage");
        scroll.Dispose();
        check(child.Disposals == 1 && Throws<ObjectDisposedException>(() => _ = scroll.Child),
            "scroll disposal is idempotent and rejects disposed child access");

        using var invalidChild = new Probe(new(1, 1));
        using var unbounded = Dux.ScrollView(invalidChild);
        Frame(ui => check(Throws<InvalidOperationException>(() => unbounded.MeasureConstrained(ui, new())) && invalidChild.Measures == 0,
            "an unbounded viewport fails before measuring or reading its child"));
        check(Throws<ArgumentOutOfRangeException>(() => unbounded.Options = new((UiScrollAxis)999))
            && Throws<ArgumentOutOfRangeException>(() => unbounded.Options = new(Width : float.NaN))
            && Throws<ArgumentOutOfRangeException>(() => unbounded.Offset = new(-1, 0)),
            "invalid axes, viewport dimensions and offsets fail explicitly");
        check(Throws<InvalidOperationException>(() => Dux.ScrollView(invalidChild)), "a child cannot be owned by two scroll views");

        var nestedChild = new Probe(new(30, 200));
        var inner = Dux.ScrollView(nestedChild, new(Height : 80));
        using var outer = Dux.ScrollView(inner, new(Width : 120, Height : 60));
        Frame(outer.Render, idle with { MouseWheel = -1 });
        check(inner.Offset.Y > 0 && outer.Offset.Y == 0, "nested owned scrolling selects the innermost movable viewport");
        inner.Offset = new(0, 10000);
        Frame(outer.Render);
        var innerBottom = inner.Offset.Y;
        Frame(outer.Render, idle with { MouseWheel = -1 });
        check(inner.Offset.Y == innerBottom && outer.Offset.Y == 20, "an owned inner viewport at its boundary hands wheel to its owned parent");
        var restored = false;
        nestedChild.ThrowOnRender = true;
        Frame(ui =>
        {
            var clip = new UiControlContext(ui).ClipRect;
            check(Throws<InvalidOperationException>(() => outer.Render(ui)), "owned child render failures remain explicit");
            restored = new UiControlContext(ui).ClipRect == clip;
            ui.Dummy(new(10, 10));
        });
        check(restored, "scroll child failure restores the caller clip and layout scopes");
        nestedChild.ThrowOnRender = false;
        Frame(outer.Render);
        check(nestedChild.Renders > 0, "owned scroll content recovers on a subsequent balanced render");

        var tableChild = new Probe(new(30, 200));
        using var tableScroll = Dux.SizedBox(Dux.ScrollView(tableChild), 80, 64);
        Frame(ui =>
        {
            ui.Columns(2);
            ui.NextColumn();
            var anchor = ui.GetCursorScreenPos();
            tableScroll.Render(ui);
            check(tableChild.Bounds.X == anchor.X && tableChild.Bounds.Width == 80 - thickness,
                "owned scroll content preserves exact parent coordinates inside legacy columns");
            ui.Columns(1);
        });

        using var feedback = Dux.ScrollView(new Probe(new(95, 70)), new(UiScrollAxis.Both, 100, 60));
        feedback.Offset = new(10000, 10000);
        Frame(feedback.Render);
        check(feedback.Offset == new UiVector2(thickness - 5, thickness + 10),
            "reserving a vertical gutter can introduce and correctly resolve the horizontal scrollbar");
        using var unstable = Dux.ScrollView(new UnstableProbe(), new(Width : 100, Height : 60));
        Frame(ui => check(Throws<InvalidOperationException>(() => unstable.MeasureConstrained(ui, new())),
            "nonconverging content geometry fails explicitly without rendering or inventing a fallback size"));
        var largeChild = new Probe(new(20, 1_000_000_000));
        using var large = Dux.ScrollView(largeChild, new(Width : 100, Height : 60));
        Frame(large.Render);
        check(largeChild.Renders == 1 && largeChild.Clip.Height == 60,
            "a large document retains its representable initial viewport before scrolling to distant coordinates");
        var renderedBeforeDistantScroll = largeChild.Renders;
        large.Offset = new(0, 1_000_000_000);
        Frame(ui => check(Throws<NotSupportedException>(() => large.Render(ui)) && largeChild.Renders == renderedBeforeDistantScroll,
            "unrepresentable full-content coordinates fail before rendering or input instead of silently shortening the viewport clip"));
        largeChild.Change(new(20, 1_000_000));
        large.Offset = new(0, 1_000_000);
        Frame(large.Render);
        check(float.IsFinite(largeChild.Bounds.Y) && largeChild.Clip.Height == 60 && large.Offset.Y == 999940,
            "representable million-unit content preserves the full viewport and finite content coordinates");

        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var scrollChanges = new List<UiAccessibilityScrollChange>();
        var geometryChanges = new List<UiAccessibilityGeometryChange>();
        var structureChanges = new List<UiAccessibilityElement>();
        var scrollChangesPublished = true;
        accessibility.ScrollChanged += change =>
        {
            scrollChanges.Add(change);
            scrollChangesPublished &= accessibility.TryGet(change.Element.Node.Id, out var published) && published == change.Element;
        };
        accessibility.ScrollGeometryChanged += change =>
        {
            geometryChanges.Add(change);
            scrollChangesPublished &= accessibility.TryGet(change.Element.Node.Id, out var published) && published == change.Element;
        };
        accessibility.PaneChildrenChanged += pane =>
        {
            structureChanges.Add(pane);
            scrollChangesPublished &= accessibility.TryGet(pane.Node.Id, out var published) && published == pane;
        };
        var reads = 0;
        var writes = 0;
        var value = "a";
        var editor = Fluent.Input("Scroll editor", new UiBinding<string>(
            () =>
            {
                reads++;
                return value;
            },
            next =>
            {
                writes++;
                value = next;
            }), 128);
        using var editorScroll = Dux.ScrollView(new UiColumn(
            [Dux.SizedBox(height : 100), editor, Dux.SizedBox(height : 200)],
            new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min)), new(Width : 180, Height : 80));
        editorScroll.AccessibleName = "Editor viewport";
        Frame(ui =>
        {
            editorScroll.MeasureConstrained(ui, new());
            check(reads == 0 && writes == 0, "owned scroll measurement never reads or writes its real editor binding");
            editorScroll.Render(ui);
        });
        check(accessibility.Root.First is { } paneId && accessibility.TryGet(paneId, out var pane)
            && pane.Node.Role == UiAccessibilityRole.Pane && pane.Node.Name == "Editor viewport" && !pane.Node.Focusable
            && pane.Node.Scroll is { HorizontallyScrollable : false, VerticallyScrollable : true },
            "the named scroll pane exposes its supported accessibility axis");
        accessibility.TryGet(accessibility.Root.First!, out var editorPane);
        check(scrollChanges.Count == 0, "initial publication does not issue a scroll property change");
        check(geometryChanges.Count == 0 && structureChanges.Count == 0, "initial pane and child publication does not issue retained changes");
        var accessibleId = editorPane.FirstChild;
        check(accessibleId is not null && accessibility.TryGet(accessibleId, out var offscreen)
            && offscreen.Node.Role == UiAccessibilityRole.Edit && offscreen.Node.ParentId == editorPane.Node.Id && offscreen.Node.Offscreen,
            "a scrolled-out editor publishes its actual offscreen Core accessibility state");
        var scrollInfo = editorPane.Node.Scroll.GetValueOrDefault();
        var percent = 100d * 100d / (scrollInfo.Content.Y - scrollInfo.Viewport.Y);
        check(accessibility.RequestScroll(editorPane.Node.Id, editorPane.RuntimeId,
            UiAccessibilityScrollCommand.ByPercent(-1, percent), wait : false) == UiAccessibilityResult.Success,
            "the scroll pane accepts a vertical accessibility request");
        Frame(editorScroll.Render);
        check(MathF.Abs(editorScroll.Offset.Y - 100) < 0.001f
            && accessibility.TryGet(editorPane.Node.Id, out var movedPane) && movedPane.Node.Scroll?.Offset == editorScroll.Offset
            && accessibleId is not null && accessibility.TryGet(accessibleId, out var visibleEditor) && !visibleEditor.Node.Offscreen,
            "the accessibility request moves and exposes the real editor in the same frame");
        check(scrollChanges.Count == 1 && scrollChanges[0].Previous == scrollInfo
            && scrollChanges[0].Element.Node.Scroll?.Offset == editorScroll.Offset && scrollChangesPublished,
            "the scroll change carries both snapshots and observes the already published element");
        check(geometryChanges.Any(change => change.Element.Node.Id == accessibleId && change.WasOffscreen
            && !change.Element.Node.Offscreen && change.PreviousBounds != change.Element.Node.Bounds)
            && structureChanges.Count == 0 && scrollChangesPublished,
            "scrolling into view publishes the retained child's new clip and visibility without a structure event");
        geometryChanges.Clear();
        Frame(editorScroll.Render);
        check(scrollChanges.Count == 1, "an unchanged offset and viewport do not publish duplicate scroll changes");
        check(geometryChanges.Count == 0 && structureChanges.Count == 0, "stable layout does not repeat geometry or structure events");
        check(accessibility.RequestScroll(editorPane.Node.Id, editorPane.RuntimeId,
            UiAccessibilityScrollCommand.ByPercent(50, -1), wait : false) == UiAccessibilityResult.InvalidOperation,
            "the scroll pane rejects movement on an unsupported axis");
        Frame(editorScroll.Render, idle with { MousePosition = new(30, 20), LeftMouseDown = true, LeftMousePressed = true });
        var editorId = context.State.ActiveId;
        check(editorId is not null, "scrolling a real editor into view makes its actual visible bounds interactive");
        Frame(editorScroll.Render, idle with { LeftMouseReleased = true });
        Frame(editorScroll.Render, idle with { KeyEvents = [new(UiKey.End, true, default)], CharEvents = [new('x')] });
        check(value == "ax" && writes == 1 && context.State.ActiveId == editorId,
            "owned scrolling preserves editor identity and performs one real binding edit");
        editorScroll.Offset = new(0, 105);
        Frame(editorScroll.Render, idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(value == "a", "scroll offset changes preserve the editor undo history");
        editorScroll.Options = editorScroll.Options with { Height = 400 };
        scrollChanges.Clear();
        geometryChanges.Clear();
        Frame(editorScroll.Render, display : new(320, 440));
        check(accessibility.TryGet(editorPane.Node.Id, out var fittedPane) && fittedPane.RuntimeId == editorPane.RuntimeId
            && fittedPane.Node.Scroll is null && fittedPane.FirstChild == accessibleId && editorScroll.Offset == default
            && accessibility.TryGet(accessibleId!, out var fittedEditor) && !fittedEditor.Node.CanScrollIntoView,
            "a fitted scroll pane keeps its child hierarchy and withdraws the scroll pattern");
        check(scrollChanges.Count == 1 && scrollChanges[0].Element.RuntimeId == editorPane.RuntimeId
            && scrollChanges[0].Previous is { VerticallyScrollable : true } && scrollChanges[0].Element.Node.Scroll is null && scrollChangesPublished,
            "fitting the content reports pattern withdrawal on the retained pane");
        check(geometryChanges.Any(change => change.Element.RuntimeId == editorPane.RuntimeId
            && change.PreviousBounds.Height == 80 && change.Element.Node.Bounds.Height == 400) && scrollChangesPublished,
            "a changed viewport publishes the retained pane's old and new bounds");
        editorScroll.Dispose();
        check(context.State.ActiveId is null && context.State.TextInputStorage == (0, 0),
            "disposing a scroll subtree releases its real editor buffers, undo history and input owner");

        var reversed = false;
        var includeMiddle = true;
        using var structureView = Dux.ScrollView(new PaneRows(ui =>
        {
            ui.Button("First pane row");
            if (includeMiddle)
            {
                ui.Button(reversed ? "Third pane row" : "Second pane row");
                ui.Button(reversed ? "Second pane row" : "Third pane row");
            }
            ui.Button("Last pane row");
        }), new(Width : 180, Height : 80));
        Frame(structureView.Render);
        accessibility.TryGet(accessibility.Root.First!, out var originalStructure);
        reversed = true;
        Frame(structureView.Render);
        check(structureChanges.Count == 1 && structureChanges[0].RuntimeId == originalStructure.RuntimeId
            && structureChanges[0].FirstChild == originalStructure.FirstChild && structureChanges[0].LastChild == originalStructure.LastChild,
            "a middle reorder invalidates a retained pane once even when its first and last children stay the same");
        structureChanges.Clear();
        includeMiddle = false;
        Frame(structureView.Render);
        check(structureChanges.Count == 1 && structureChanges[0].RuntimeId == originalStructure.RuntimeId,
            "removing multiple direct children issues one pane invalidation");
        structureChanges.Clear();
        includeMiddle = true;
        Frame(structureView.Render);
        check(structureChanges.Count == 1 && structureChanges[0].RuntimeId == originalStructure.RuntimeId && scrollChangesPublished,
            "adding children invalidates the retained pane after the new hierarchy is published");

        var nestedTarget = Fluent.Button("Reveal target");
        var revealInner = Dux.ScrollView(Dux.Padding(nestedTarget, new(Left : 0, Top : 160, Right : 160, Bottom : 0)),
            new UiScrollOptions(UiScrollAxis.Both, Width : 104, Height : 80) { RightToLeft = true });
        using var revealOuter = Dux.ScrollView(new UiColumn(
            [Dux.SizedBox(height : 180), revealInner], new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min)),
            new(Width : 140, Height : 100));
        Frame(revealOuter.Render);
        accessibility.TryGet(accessibility.Root.First!, out var outerPane);
        accessibility.TryGet(outerPane.FirstChild!, out var innerPane);
        accessibility.TryGet(innerPane.FirstChild!, out var hiddenTarget);
        check(innerPane.Node.Offscreen && innerPane.Node.Scroll is null && hiddenTarget.Node.Offscreen && hiddenTarget.Node.CanScrollIntoView,
            "an offscreen nested viewport preserves ScrollItem for its offscreen target");
        using var queued = new ManualResetEventSlim();
        accessibility.SetRequestFrameCallback(queued.Set);
        var reveal = Task.Run(() => accessibility.RequestAction(hiddenTarget.Node.Id, hiddenTarget.RuntimeId, UiAccessibilityAction.ScrollIntoView));
        check(queued.Wait(TimeSpan.FromSeconds(1)), "an external ScrollItem request wakes the render writer");
        Frame(revealOuter.Render);
        check(!reveal.IsCompleted && revealInner.Offset == default && revealOuter.Offset == default,
            "the scroll action remains pending while the frame captures nested geometry");
        Frame(revealOuter.Render);
        check(reveal.GetAwaiter().GetResult() == UiAccessibilityResult.Success
            && revealInner.Offset is { X : > 0, Y : > 0 } && revealOuter.Offset.Y > 0
            && accessibility.TryGet(hiddenTarget.Node.Id, out var revealedTarget) && !revealedTarget.Node.Offscreen
            && revealedTarget.RuntimeId == hiddenTarget.RuntimeId && accessibility.Root.Focus is null,
            "both nested owners reveal the original child before completing the request and preserve focus");
        queued.Reset();
        var removal = Task.Run(() => accessibility.RequestAction(hiddenTarget.Node.Id, hiddenTarget.RuntimeId, UiAccessibilityAction.ScrollIntoView));
        check(queued.Wait(TimeSpan.FromSeconds(1)), "a second ScrollItem action is queued before its target is removed");
        Frame(static _ => { });
        check(removal.GetAwaiter().GetResult() == UiAccessibilityResult.Unavailable,
            "removal before reveal planning completes the request with an unavailable element");
        accessibility.SetRequestFrameCallback(null);

        var rtlChild = new Probe(new(300, 200));
        using var rtl = Dux.ScrollView(rtlChild, new UiScrollOptions(UiScrollAxis.Both, 100, 60) { RightToLeft = true });
        Frame(rtl.Render);
        accessibility.TryGet(accessibility.Root.First!, out var rtlPane);
        check(rtl.Offset == default && rtlChild.Bounds.X == -190 && rtlChild.Clip.X == 10 + thickness
            && rtlPane.Node.Scroll is { RightToLeft : true, HorizontalScrollPercent : 0 },
            "RTL starts at the physical right edge and reserves the vertical gutter on the left");
        var rtlMaximum = 200 + thickness;
        accessibility.RequestScroll(rtlPane.Node.Id, rtlPane.RuntimeId, UiAccessibilityScrollCommand.ByPercent(100, -1), wait : false);
        Frame(rtl.Render);
        check(rtl.Offset == new UiVector2(rtlMaximum, 0) && rtlChild.Bounds.X == 10 + thickness
            && accessibility.TryGet(rtlPane.Node.Id, out var rtlLeft) && rtlLeft.Node.Scroll is { HorizontalScrollPercent : 100 },
            "RTL UIA 100 percent exposes the physical left edge and keeps the vertical offset");
        accessibility.RequestScroll(rtlPane.Node.Id, rtlPane.RuntimeId, UiAccessibilityScrollCommand.ByAmount(
            UiAccessibilityScrollAmount.SmallDecrement, UiAccessibilityScrollAmount.NoAmount), wait : false);
        Frame(rtl.Render);
        check(rtl.Offset.X > 0 && rtl.Offset.X < rtlMaximum && rtlChild.Bounds.X < 10 + thickness,
            "a horizontal RTL decrement moves the viewport physically to the right");
        rtl.Offset = default;
        Frame(rtl.Render, idle with { MouseWheelHorizontal = 1 });
        check(rtl.Offset.X > 0, "the horizontal wheel moves left from the RTL start");
        rtl.Offset = default;
        Frame(rtl.Render, idle with { MousePosition = new(104, 66), LeftMousePressed = true, LeftMouseDown = true });
        var rtlThumb = context.State.ActiveId;
        Frame(rtl.Render, idle with { MousePosition = new(45, 66), LeftMouseDown = true });
        check(rtlThumb is not null && context.State.ActiveId == rtlThumb && rtl.Offset.X > 0,
            "dragging the rightmost RTL thumb left advances the logical offset");
        rtl.Options = rtl.Options with { RightToLeft = false };
        check(context.State.ActiveId is null, "changing direction releases the old scrollbar capture");

        var storageBeforeTransfer = context.State.ScrollStorage;
        var motionBeforeTransfer = context.State.ScrollbarMotionCount;
        var transferChild = new Probe(new(30, 200));
        using var transfer = Dux.ScrollView(transferChild, new(Width : 100, Height : 60));
        Frame(transfer.Render, idle with { MousePosition = new(102, 14), LeftMouseDown = true, LeftMousePressed = true });
        using var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(other);
        other.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        other.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            transfer.Render(ui);
        }));
        using var otherData = other.GetDrawData();
        check(context.State.ActiveId is null && context.State.ScrollStorage == storageBeforeTransfer
            && context.State.ScrollbarMotionCount == motionBeforeTransfer && other.State.ScrollbarMotionCount > 0,
            "moving a scroll view to another UI context releases the previous context's drag and scrollbar storage");
        transfer.Dispose();
        check(transferChild.Disposals == 1 && other.State.ScrollStorage == (0, 0) && other.State.ScrollbarMotionCount == 0,
            "transferred scroll ownership is released from the current context on disposal");

        var styleChild = new Probe(new(20, 200));
        using var styleScroll = Dux.ScrollView(styleChild, new(Width : 100, Height : 60));
        var originalStyle = context.GetStyleData();
        Frame(styleScroll.Render);
        context.SetStyle(originalStyle with { ScrollbarSize = 20 });
        Frame(styleScroll.Render);
        check(styleChild.Bounds.Width == 80,
            "a changed scrollbar metric invalidates the scroll layout even when the text measurement key is unchanged");
        context.SetStyle(originalStyle);

        using var fractional = Dux.ScrollView(new Probe(new(300, 200)), new(UiScrollAxis.Both, 100, 60));
        fractional.Offset = new(31.3f, 41.7f);
        Frame(ui =>
        {
            ui.SetCursorScreenPos(new(10.3f, 10.7f));
            check(!Throws<NotSupportedException>(() => fractional.Render(ui)),
                "ordinary fractional world coordinates do not trigger the large-content precision guard");
        });
        var dynamicChild = new Probe(new(20, 200), retain : false);
        using var dynamicScroll = Dux.ScrollView(dynamicChild, new(Width : 100, Height : 60));
        Frame(dynamicScroll.Render);
        dynamicChild.ChangeUnobserved(new(20, 40));
        Frame(dynamicScroll.Render);
        check(dynamicChild.Bounds.Width == 100 && dynamicChild.Bounds.Height == 40,
            "non-retained content is remeasured instead of reusing a stale scroll layout");

        var left = Dux.ScrollView(new UiColumn(
            Enumerable.Range(0, 12).Select(index => (UiWidget)Fluent.Button($"Row : {index}")).ToArray(),
            new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)), new(Width : 300, Height : 280));
        var rightView = Dux.ScrollView(Dux.SizedBox(Fluent.Body1("Two axes"), 620, 900), new(UiScrollAxis.Both, 300, 280));
        var widthLabel = Fluent.Button("Width : 300");
        var heightLabel = Fluent.Button("Height : 280");
        using var gallery = Dux.Padding(new UiColumn(
        [
            Fluent.Title2("Owned scroll views"),
            Fluent.Body1("Edit, scroll, resize the viewport, then return. Child identity and undo stay intact."),
            Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), widthLabel, heightLabel, Fluent.Button("Top"), Fluent.Button("End")),
            Dux.Row(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), left, rightView),
            Fluent.Caption1("Left : vertical / Right : both axes.")
        ], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));
        var galleryBalanced = true;
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
        foreach (var compact in new[] { false, true, false })
        {
            context.SetContentScale(scale);
            var widthValue = compact ? 180 : 300;
            var heightValue = compact ? 160 : 280;
            left.Options = new(Width : widthValue, Height : heightValue);
            rightView.Options = new(UiScrollAxis.Both, widthValue, heightValue);
            widthLabel.Label = $"Width : {widthValue}";
            heightLabel.Label = $"Height : {heightValue}";
            Frame(ui => galleryBalanced &= !Throws<InvalidOperationException>(() => gallery.Render(ui)), display : new(1200, 900));
        }
        check(galleryBalanced, "paired scroll gallery stays balanced across twelve live size, label and content-scale transitions");
        context.SetContentScale(1);

        var firstChild = new Probe(new(20, 200));
        var firstScroll = Dux.ScrollView(firstChild, new(Width : 180, Height : 60));
        var secondScroll = Dux.ScrollView(new Probe(new(20, 200)), new(Width : 180, Height : 60));
        var growDuringRender = false;
        using var mutationTree = new UiColumn(
        [
            new Pulse(() =>
            {
                if (!growDuringRender)
                    return;
                firstScroll.Options = firstScroll.Options with { Width = 300 };
                secondScroll.Options = secondScroll.Options with { Width = 300 };
            }),
            Dux.Row(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), firstScroll, secondScroll)
        ], new(MainAxisSize : UiMainAxisSize.Min));
        Frame(mutationTree.Render, display : new(1200, 900));
        Frame(mutationTree.Render, display : new(1200, 900));
        growDuringRender = true;
        Frame(ui => check(!Throws<InvalidOperationException>(() => mutationTree.Render(ui)) && firstChild.Bounds.Width == 180 - thickness,
            "an earlier callback can grow a later scroll sibling without replacing this frame's already measured allocation"), display : new(1200, 900));
        Frame(mutationTree.Render, display : new(1200, 900));
        check(firstChild.Bounds.Width == 300 - thickness, "the next frame applies a callback's larger scroll allocation");

        var observedText = UiTextSettings.Default with { MissingGlyphObserver = static _ => { } };
        growDuringRender = false;
        firstScroll.Options = firstScroll.Options with { Width = 180 };
        secondScroll.Options = secondScroll.Options with { Width = 180 };
        Frame(mutationTree.Render, display : new(1200, 900), textSettings : observedText);
        growDuringRender = true;
        Frame(ui => check(!Throws<InvalidOperationException>(() => mutationTree.Render(ui)) && firstChild.Bounds.Width == 180 - thickness,
            "a glyph observer disables cross-frame retention without discarding this frame's assigned sibling geometry"),
            display : new(1200, 900), textSettings : observedText);
        Frame(mutationTree.Render, display : new(1200, 900), textSettings : observedText);
        check(firstChild.Bounds.Width == 300 - thickness, "observed layouts remeasure the grown sibling on the following frame");

        var directChild = new Probe(new(20, 200));
        using var direct = Dux.ScrollView(directChild, new(Width : 100, Height : 60));
        Frame(ui =>
        {
            direct.MeasureConstrained(ui, new());
            direct.Options = direct.Options with { Width = 180 };
            direct.Render(ui);
            check(directChild.Bounds.Width == 180 - thickness,
                "an unassigned direct render measures changed options instead of reusing invalidated parent arrangement");
        });
    }

    private sealed class Pulse(Action action) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => default;
        protected override void RenderControl(UiImmediateContext ui) => action();
    }

    private sealed class UnstableProbe : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) =>
            context.LayoutConstraints.Constrain(new(1, context.LayoutConstraints.MaxWidth > 90 ? 200 : 20));
        protected override void RenderControl(UiImmediateContext ui) => throw new InvalidOperationException("Unstable content must not render.");
    }

    internal sealed class Probe(UiVector2 size, bool retain = true) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal int Measures, Renders, Disposals;
        internal UiBoxConstraints LastConstraints;
        internal UiRect Bounds, Clip;
        internal bool ThrowOnRender;
        protected override bool CanRetainMeasurement => retain;
        internal void ChangeUnobserved(UiVector2 value) => _size = value;
        internal void Change(UiVector2 value)
        {
            _size = value;
            RequestRebuild();
        }
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measures++;
            LastConstraints = context.LayoutConstraints;
            return LastConstraints.Constrain(_size);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            if (ThrowOnRender)
                throw new InvalidOperationException("Deliberate scroll content failure.");
            Renders++;
            var context = new UiControlContext(ui);
            var actual = context.LayoutConstraints.Constrain(_size);
            var origin = ui.AllocateLayoutBox(actual);
            Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            Clip = context.ClipRect;
        }
        protected override void OnDispose() => Disposals++;
    }

    private sealed class PaneRows(Action<UiImmediateContext> draw) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(160, 160);
        protected override void RenderControl(UiImmediateContext ui) => draw(ui);
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
