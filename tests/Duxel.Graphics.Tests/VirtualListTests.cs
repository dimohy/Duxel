using Duxel.Core;

internal static class VirtualListTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
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

        var source = new Source();
        using var list = Dux.VirtualList(source, 1_000_000_000, new(32, 100, 100));
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            check(list.MeasureConstrained(ui, new()) == new UiVector2(100, 100), "virtual pure measurement returns a finite viewport");
            check(source.Keys == 0 && source.Created == 0 && source.Binds == 0 && ui.GetCursorScreenPos() == cursor,
                "virtual measurement never enumerates keys, creates widgets, binds data or moves the cursor");
            list.Render(ui);
        });
        var thickness = context.GetStyleData().ScrollbarSize;
        check(list.RealizedCount == 5 && source.Live.Count == 5 && source.Created == 5,
            "a billion-row list realizes only its four intersecting rows and one trailing overscan row");
        check(source.Live[0].Bounds == new UiRect(10, 10, 100 - thickness, 32), "realized rows receive actual tight viewport-width slots");
        var first = source.Live[0];
        var keyCalls = source.Keys;
        Frame(list.Render);
        check(source.Keys == keyCalls && source.Created == 5 && first.Renders == 2,
            "an unchanged virtual range renders live rows without rebinding or creating them");
        list.Offset = 32;
        Frame(list.Render);
        check(ReferenceEquals(first, source.Live[0]) && list.RealizedCount == 6 && source.Created == 6,
            "one-row scrolling keeps overlapping keyed widgets and realizes only the newly exposed edge");
        list.Offset = 64;
        Frame(list.Render);
        check(first.Disposals == 1 && !source.Live.ContainsKey(0) && source.Live.Count == 6,
            "a row leaving the overscan range is disposed once through normal widget ownership");
        var maximum = 32_000_000_000d - 100;
        list.Offset = double.MaxValue;
        Frame(list.Render);
        check(list.Offset == maximum && source.Live[999_999_999].Bounds == new UiRect(10, 78, 100 - thickness, 32),
            "the last billion-row item is rebased exactly to the viewport bottom without a huge world rectangle");
        check(source.Live.Count == 5 && source.MaxLive <= 12 && source.Created < 20,
            "realized and transient owned storage stay proportional to the window rather than the billion-item count");
        list.Offset = maximum - 0.25;
        Frame(list.Render);
        check(list.Offset == maximum - 0.25 && source.Live[999_999_999].Bounds.Y == 78.25f,
            "double offsets preserve fractional movement beyond float's billion-coordinate precision");
        list.Offset = maximum - 1000;
        Frame(list.Render);
        var beforeWheel = list.Offset;
        var step = 0f;
        Frame(ui =>
        {
            step = ui.GetFrameHeight() * 3;
            list.Render(ui);
        }, idle with { MousePosition = new(30, 30), MouseWheel = -1 });
        check(list.Offset == beforeWheel + step, "wheel movement retains its exact local step at a thirty-two-billion offset");

        var horizontalSource = new Source();
        using var horizontal = Dux.VirtualList(horizontalSource, 1_000_000_000,
            new UiVirtualListOptions(32, 100, 80) { Axis = UiScrollAxis.Horizontal });
        horizontal.Offset = maximum - .25;
        Frame(horizontal.Render);
        check(horizontalSource.Live[999_999_999].Bounds == new UiRect(78.25f, 10, 32, 80 - thickness)
            && horizontal.RealizedCount <= 6, "horizontal virtualization rebases only visible and overscan items at the fractional tail");
        var horizontalThumb = idle with { MousePosition = new(102, 90 - thickness / 2), LeftMouseDown = true };
        Frame(horizontal.Render, horizontalThumb with { LeftMousePressed = true });
        check(context.State.PreciseScrollbarDrag is not null && horizontal.Offset == maximum - .25,
            "the horizontal thumb keeps its exact offset when pressed");
        Frame(horizontal.Render, horizontalThumb with { MousePosition = new(94, horizontalThumb.MousePosition.Y) });
        check(horizontal.Offset < maximum - .25, "moving the horizontal thumb left scrolls toward the start");
        Frame(horizontal.Render, horizontalThumb);
        check(horizontal.Offset == maximum - .25, "returning the horizontal thumb restores the fractional offset");
        Frame(horizontal.Render, idle with { LeftMouseReleased = true });
        horizontal.Offset = maximum - 1000;
        var hover = idle with { MousePosition = new(30, 30) };
        Frame(horizontal.Render, hover with { MouseWheel = -1 });
        check(horizontal.Offset == maximum - 1000, "a vertical wheel does not move the horizontal list");
        Frame(horizontal.Render, hover with { MouseWheelHorizontal = -1 });
        check(horizontal.Offset == maximum - 1000 + step, "the horizontal wheel moves by an exact local step");
        check(Throws<ArgumentOutOfRangeException>(() => horizontal.Options = horizontal.Options with { Axis = UiScrollAxis.Both }),
            "a one-axis virtual list rejects the two-axis option");

        var rtlSource = new AutomationSource();
        using var rtl = Dux.VirtualList(rtlSource, 1_000_000_000,
            new UiVirtualListOptions(32, 100, 80) { Axis = UiScrollAxis.Horizontal, FlowDirection = UiFlowDirection.RightToLeft });
        Frame(rtl.Render);
        check(rtlSource.Live[0].Bounds.X == 78 && rtlSource.Live[1].Bounds.X == 46,
            "RTL virtual items begin at the right and follow model indices to the left");
        rtl.Offset = maximum - .25;
        Frame(rtl.Render);
        check(rtlSource.Live[999_999_999].Bounds.X == 9.75f && rtl.RealizedCount <= 6,
            "RTL rebases the billionth item against the left with the exact fractional offset");
        rtl.Offset = .000001;
        Frame(rtl.Render, hover with { MouseWheelHorizontal = 1 });
        check(rtl.Offset == .000001 + step, "RTL horizontal wheel preserves a sub-ULP leading offset");
        rtl.Offset = .000001;
        Frame(rtl.Render);
        Frame(rtl.Render, horizontalThumb with { LeftMousePressed = true });
        Frame(rtl.Render, horizontalThumb with { MousePosition = new(94, horizontalThumb.MousePosition.Y) });
        check(rtl.Offset > 0, "dragging the RTL scrollbar thumb left advances the logical offset");
        Frame(rtl.Render, horizontalThumb);
        check(rtl.Offset == .000001, "returning the RTL thumb preserves the original leading offset");
        Frame(rtl.Render, idle with { LeftMouseReleased = true });
        rtl.Offset = 0;
        rtl.Options = rtl.Options with { FlowDirection = UiFlowDirection.Inherit };
        Frame(ui =>
        {
            using var scope = ui.BeginFluentProvider(null, direction : UiFlowDirection.RightToLeft);
            rtl.Render(ui);
        });
        check(rtlSource.Live[0].Bounds.X == 78, "a horizontal virtual list inherits RTL from its enclosing provider");
        rtl.Options = rtl.Options with { FlowDirection = UiFlowDirection.RightToLeft };
        source.RenderAction = () =>
        {
            source.RenderAction = null;
            list.Offset = 512.25;
        };
        Frame(list.Render);
        check(list.Offset == 512.25, "a row callback's explicit offset request is not overwritten by the viewport return value");
        Frame(list.Render);
        check(source.Live.Values.Any(row => row.Bounds.Y == 9.75f), "the next frame realizes a callback-requested fractional offset");
        source.RenderAction = () =>
        {
            source.RenderAction = null;
            list.Options = list.Options with { ItemExtent = 40 };
        };
        Frame(list.Render);
        check(source.Live[17].Bounds.Y == 41.75f && source.Live[17].Bounds.Height == 32,
            "a row callback's extent change does not alter the remaining slots in an already prepared frame");
        Frame(list.Render);
        check(source.Live[13].Bounds.Y == 17.75f && source.Live[13].Bounds.Height == 40,
            "the following frame realizes the new item extent without stale slot geometry");
        list.ItemCount = 0;
        Frame(list.Render);
        check(list.Offset == 0 && list.RealizedCount == 0 && source.Live.Count == 0 && source.Disposed == source.Created,
            "emptying a virtual source clamps the offset and disposes every previously realized row");
        check(context.State.ScrollStorage == (0, 0), "an empty virtual list releases its scrollbar auxiliary state");

        var nestedSource = new Source();
        var nested = Dux.VirtualList(nestedSource, 1_000_000, new(32, Height : 80));
        using var parent = Dux.ScrollView(nested, new(Width : 120, Height : 60));
        Frame(ui =>
        {
            parent.MeasureConstrained(ui, new());
            check(nestedSource.Keys == 0 && nestedSource.Created == 0,
                "parent layout resolves virtual infrastructure without building or realizing its item source");
            parent.Render(ui);
        });
        Frame(parent.Render, idle with { MousePosition = new(30, 30), MouseWheel = -1 });
        check(nested.Offset > 0 && parent.Offset.Y == 0, "a nested virtual viewport owns wheel movement before its outer ScrollView");
        nested.Offset = double.MaxValue;
        Frame(parent.Render);
        var nestedEnd = nested.Offset;
        Frame(parent.Render, idle with { MousePosition = new(30, 30), MouseWheel = -1 });
        check(nested.Offset == nestedEnd && parent.Offset.Y == 20,
            "a virtual viewport at its boundary passes wheel input to the common outer scroll kernel");

        var flexSource = new Source();
        using var flex = Dux.Row(new(MainAxisSize : UiMainAxisSize.Min), Dux.VirtualList(flexSource, 100, new(32, 100, 80)));
        Frame(ui =>
        {
            flex.MeasureConstrained(ui, UiBoxConstraints.Loose(new(300, 180)));
            check(flexSource.Keys == 0 && flexSource.Created == 0,
                "Row parent-data discovery treats a virtual list as infrastructure without running its realization Build");
            flex.Render(ui);
        });

        var pointerSource = new Source();
        using var pointerList = Dux.VirtualList(pointerSource, 1_000_000_000, new(32, 100, 100, Overscan : 0));
        Frame(pointerList.Render);
        Frame(pointerList.Render, idle with { MousePosition = new(104, 14), LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.ActiveId is not null, "the double-precision scrollbar captures its existing shared thumb owner");
        Frame(pointerList.Render, idle with { MousePosition = new(104, 94), LeftMouseDown = true });
        check(pointerList.Offset > maximum * 0.9 && pointerList.Offset <= maximum,
            "shared thumb dragging maps viewport coordinates to a double-precision virtual range");
        Frame(pointerList.Render, idle with { LeftMouseReleased = true });

        check(context.State.ActiveId is null, "double-precision scrollbar release clears pointer ownership");
        pointerList.Offset = maximum - .25;
        Frame(pointerList.Render);
        var thumb = idle with { MousePosition = new(104, 102), LeftMouseDown = true };
        Frame(pointerList.Render, thumb with { LeftMousePressed = true });
        check(context.State.ActiveId is not null && pointerList.Offset == maximum - .25,
            "pressing a thumb preserves the exact fractional offset near the billion-row tail");
        Frame(pointerList.Render, thumb);
        check(pointerList.Offset == maximum - .25, "holding a stationary thumb does not round the content offset");
        Frame(pointerList.Render, thumb with { MousePosition = new(104, 96) });
        check(pointerList.Offset < maximum - .25, "moving the thumb from a fractional tail position scrolls toward the start");
        Frame(pointerList.Render, thumb);
        check(pointerList.Offset == maximum - .25, "returning the thumb to its press position restores the exact offset");
        Frame(pointerList.Render, idle with { LeftMouseReleased = true });
        check(context.State.PreciseScrollbarDrag is null, "releasing the thumb discards its retained drag origin");
        pointerList.Offset = 0;
        Frame(pointerList.Render);
        Frame(pointerList.Render, idle with { MousePosition = new(104, 80), LeftMouseDown = true, LeftMousePressed = true });
        check(pointerList.Offset == 100, "virtual scrollbar track paging uses one viewport rather than a normalized full-range jump");
        Frame(pointerList.Render, idle with { LeftMouseReleased = true });

        pointerList.Offset = 0;
        Frame(pointerList.Render, idle with { MousePosition = new(104, 14), LeftMouseDown = true, LeftMousePressed = true });
        using var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(other);
        other.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        other.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            pointerList.Render(ui);
        }));
        using var otherData = other.GetDrawData();
        check(context.State.ActiveId is null, "moving a virtual viewport to a new UI context releases the old scrollbar capture");
        pointerList.Dispose();
        check(pointerSource.Live.Count == 0 && other.State.ScrollStorage == (0, 0),
            "virtual viewport disposal releases the current context's scrollbar state and every realized row");
        var reorder = new Source();
        using var keyed = Dux.VirtualList(reorder, 10, new(32, 100, 64, Overscan : 0));
        Frame(keyed.Render);
        var rowZero = reorder.Live[0];
        var rowOne = reorder.Live[1];
        reorder.KeyForIndex = index => index == 0 ? 1 : index == 1 ? 0 : index;
        keyed.Refresh();
        Frame(keyed.Render);
        check(ReferenceEquals(rowZero, reorder.Live[0]) && ReferenceEquals(rowOne, reorder.Live[1])
            && rowOne.Bounds.Y == 10 && rowZero.Bounds.Y == 42 && reorder.Created == 2,
            "source refresh reorders stable keys without replacing their owned widget instances");
        reorder.KeyForIndex = static _ => 0;
        keyed.Refresh();
        Frame(ui => check(Throws<InvalidOperationException>(() => keyed.Render(ui)), "duplicate realized keys fail explicitly before partial row rendering"));
        reorder.KeyForIndex = static index => index;
        reorder.ThrowCreateAt = 4;
        keyed.Offset = 96;
        Frame(ui => check(Throws<InvalidOperationException>(() => keyed.Render(ui)) && !reorder.Live.ContainsKey(3),
            "a later factory failure releases newly created rows through Build abort cleanup"));
        reorder.ThrowCreateAt = null;
        keyed.Refresh();
        Frame(keyed.Render);
        check(reorder.Live.Count == 2 && reorder.Live.ContainsKey(3) && reorder.Live.ContainsKey(4),
            "a virtual source recovers after a failed realization without leaked partial ownership");
        Frame(ui =>
        {
            using var clip = new UiControlContext(ui).Clip(new(0, 0, 1, 1));
            keyed.Render(ui);
        });
        check(keyed.RealizedCount == 0 && reorder.Live.Count == 0, "a fully clipped virtual viewport realizes no rows and releases its previous range");

        var boundedSource = new Source();
        using var budget = Dux.VirtualList(boundedSource, 1_000_000_000, new(0.001f, 100, 100, MaxRealizedItems : 8));
        Frame(ui => check(Throws<InvalidOperationException>(() => budget.Render(ui)) && boundedSource.Keys == 0 && boundedSource.Created == 0,
            "an exceeded explicit realization budget fails before enumerating or allocating source rows"));
        using var tiny = Dux.VirtualList(new Source(), 1_000_000_000, new(float.Epsilon, 100, 100, MaxRealizedItems : 8));
        Frame(ui => check(Throws<InvalidOperationException>(() => tiny.Render(ui)),
            "subnormal item extents clamp range arithmetic before integer conversion and cannot bypass the realization budget"));
        using var unbounded = Dux.VirtualList(new Source(), 10);
        Frame(ui => check(Throws<InvalidOperationException>(() => unbounded.MeasureConstrained(ui, new())),
            "virtual measurement rejects an unbounded viewport instead of inventing a height"));
        check(Throws<ArgumentOutOfRangeException>(() => list.Offset = double.NaN)
            && Throws<ArgumentOutOfRangeException>(() => list.ItemCount = -1)
            && Throws<ArgumentOutOfRangeException>(() => list.Options = new(ItemExtent : 0)),
            "invalid virtual offsets, counts and item extents fail explicitly");
        using var impossible = Dux.VirtualList(new Source(), int.MaxValue, new(float.MaxValue, 100, 100));
        Frame(ui => check(Throws<NotSupportedException>(() => impossible.Render(ui)),
            "an extent beyond double viewport precision fails before realizing rows"));

        var editors = new Source { Editors = true };
        using var editing = Dux.VirtualList(editors, 100, new(40, 180, 80, Overscan : 0));
        Frame(ui =>
        {
            editing.MeasureConstrained(ui, new());
            check(editors.Reads == 0 && editors.Created == 0, "virtual editor measurement does not construct controls or read their bindings");
            editing.Render(ui);
        });
        var editorRow = editors.Live[0];
        Frame(editing.Render, idle with { MousePosition = new(30, 20), LeftMouseDown = true, LeftMousePressed = true });
        var inputId = context.State.ActiveId;
        Frame(editing.Render, idle with { LeftMouseReleased = true });
        Frame(editing.Render, idle with { KeyEvents = [new(UiKey.End, true, default)], CharEvents = [new('x')] });
        check(inputId is not null && editors.Values[0] == "ax" && editors.Writes == 1,
            "a realized editor accepts one actual binding update");
        editing.Offset = 20;
        Frame(editing.Render, idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(ReferenceEquals(editorRow, editors.Live[0]) && editors.Values[0] == "a" && context.State.ActiveId == inputId,
            "overlapping virtual ranges retain editor identity, focus and undo history");
        editing.Offset = 80;
        Frame(editing.Render);
        check(editorRow.Disposals == 1 && context.State.ActiveId is null, "evicting a focused virtual editor releases its input owner");
        editing.ItemCount = 0;
        Frame(editing.Render);
        check(editors.Live.Count == 0 && context.State.TextInputStorage == (0, 0), "virtual editor eviction releases all edit buffers and undo histories");

        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        horizontal.Offset = maximum - .25;
        Frame(horizontal.Render);
        accessibility.TryGet(accessibility.Root.First!, out var horizontalPane);
        check(horizontalPane.Node.Scroll is { HorizontalContentWidth : 32_000_000_000d, HorizontallyScrollable : true,
                VerticallyScrollable : false } horizontalScroll
            && horizontalScroll.HorizontalOffset == maximum - .25 && horizontalScroll.HorizontalScrollPercent < 100,
            "the horizontal pane exposes its exact offset, range and axis");
        var horizontalStep = accessibility.RequestScroll(horizontalPane.Node.Id, horizontalPane.RuntimeId,
            UiAccessibilityScrollCommand.ByAmount(UiAccessibilityScrollAmount.SmallDecrement, UiAccessibilityScrollAmount.NoAmount), wait : false);
        Frame(horizontal.Render);
        check(horizontalStep == UiAccessibilityResult.Success && horizontal.Offset == maximum - .25 - step,
            "the horizontal accessibility amount preserves a fractional offset");
        var horizontalEnd = accessibility.RequestScroll(horizontalPane.Node.Id, horizontalPane.RuntimeId,
            UiAccessibilityScrollCommand.ByPercent(100, -1), wait : false);
        Frame(horizontal.Render);
        check(horizontalEnd == UiAccessibilityResult.Success && horizontal.Offset == maximum,
            "the horizontal accessibility percent reaches the final item");
        rtl.Offset = .000001;
        Frame(rtl.Render);
        accessibility.TryGet(accessibility.Root.First!, out var rtlPane);
        check(rtlPane.Node.Scroll is { RightToLeft : true, HorizontalOffset : var physicalOffset, HorizontalScrollPercent : > 0 }
            && physicalOffset == maximum, "RTL accessibility keeps logical progress when the physical summary rounds");
        rtl.Offset = 32;
        Frame(rtl.Render);
        check(accessibility.TryGet(rtlPane.FirstChild!, out var rtlOverscan) && rtlOverscan.Node.Offscreen,
            "the first RTL overscan provider stays alive when clipped to the right");
        var rtlReveal = accessibility.RequestAction(rtlOverscan.Node.Id, rtlOverscan.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        Frame(rtl.Render);
        Frame(rtl.Render);
        check(rtlReveal == UiAccessibilityResult.Success && rtl.Offset == 0
            && accessibility.TryGet(rtlOverscan.Node.Id, out var rtlShown) && rtlShown.RuntimeId == rtlOverscan.RuntimeId && !rtlShown.Node.Offscreen,
            "RTL ScrollItem reveals the existing provider using logical movement");
        var accessibleSource = new Source { Editors = true };
        using var accessibleList = Dux.VirtualList(accessibleSource, 1_000_000_000, new(32, 180, 100));
        accessibleList.AccessibleName = "Billion row list";
        Frame(accessibleList.Render);
        accessibility.TryGet(accessibility.Root.First!, out var pane);
        accessibility.TryGet(pane.FirstChild!, out var firstEditor);
        check(pane.Node is { Role : UiAccessibilityRole.Pane, Name : "Billion row list",
                Scroll : { VerticalContentHeight : 32_000_000_000d, HorizontallyScrollable : false, VerticallyScrollable : true } }
            && firstEditor.Node.ParentId == pane.Node.Id && accessibility.Count == 1 + accessibleList.RealizedCount,
            "the named virtual pane exposes exact vertical metrics and only the realized editors");

        accessibleList.Offset = 32;
        Frame(accessibleList.Render);
        check(accessibility.TryGet(firstEditor.Node.Id, out var overscan) && overscan.RuntimeId == firstEditor.RuntimeId
            && overscan.Node is { Offscreen : true, CanScrollIntoView : true },
            "an overscan editor retains its provider and supports ScrollItem while clipped out");
        var reveal = accessibility.RequestAction(firstEditor.Node.Id, firstEditor.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        Frame(accessibleList.Render);
        Frame(accessibleList.Render);
        check(reveal == UiAccessibilityResult.Success && accessibleList.Offset == 0
            && accessibility.TryGet(firstEditor.Node.Id, out var revealed) && !revealed.Node.Offscreen && revealed.RuntimeId == firstEditor.RuntimeId,
            "ScrollItem reveals the original overscan editor through the virtual pane");

        var structureEvents = new List<UiAccessibilityElement>();
        accessibility.PaneChildrenChanged += structureEvents.Add;
        var end = accessibility.RequestScroll(pane.Node.Id, pane.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait : false);
        Frame(accessibleList.Render);
        check(end == UiAccessibilityResult.Success && accessibleList.Offset == maximum && accessibleSource.Live.ContainsKey(999_999_999)
            && structureEvents.Count == 1 && structureEvents[0].RuntimeId == pane.RuntimeId && accessibleSource.Live.Count <= 6,
            "UIA reaches the billion-row end and publishes one bounded child-list invalidation");
        check(!accessibility.TryGet(firstEditor.Node.Id, out _) && accessibility.RequestAction(firstEditor.Node.Id, firstEditor.RuntimeId,
            UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Unavailable,
            "a provider for an evicted virtual editor is unavailable");

        accessibility.RequestScroll(pane.Node.Id, pane.RuntimeId, UiAccessibilityScrollCommand.ByAmount(
            UiAccessibilityScrollAmount.NoAmount, UiAccessibilityScrollAmount.SmallDecrement), wait : false);
        Frame(ui =>
        {
            step = ui.GetFrameHeight() * 3f;
            accessibleList.Render(ui);
        });
        check(accessibleList.Offset == maximum - step && accessibility.TryGet(pane.Node.Id, out var stepped)
            && stepped.Node.Scroll?.VerticalOffset == accessibleList.Offset,
            "UIA small steps preserve double precision near the billion-row end");
        accessibleList.Offset = maximum - 0.25;
        Frame(accessibleList.Render);
        check(accessibility.TryGet(pane.Node.Id, out var fractionalPane) && fractionalPane.Node.Scroll is { } fractionalScroll
            && fractionalScroll.VerticalOffset == maximum - 0.25 && fractionalScroll.VerticalScrollPercent < 100,
            "UIA publishes fractional offset and percent without rounding to the end");

        accessibility.TryGet(fractionalPane.FirstChild!, out var farOverscan);
        var farRowStart = (double)accessibleSource.Live.Keys.Min() * 32;
        accessibility.RequestAction(farOverscan.Node.Id, farOverscan.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        Frame(accessibleList.Render);
        Frame(accessibleList.Render);
        check(farOverscan.Node.Offscreen && accessibleList.Offset == farRowStart
            && accessibility.TryGet(farOverscan.Node.Id, out var farRevealed) && !farRevealed.Node.Offscreen
            && farRevealed.RuntimeId == farOverscan.RuntimeId,
            "ScrollItem uses the exact rebased row start beyond float's billion-coordinate precision");

        accessibleList.ItemCount = 0;
        Frame(accessibleList.Render);
        check(accessibility.TryGet(pane.Node.Id, out var emptyPane) && emptyPane.RuntimeId == pane.RuntimeId
            && emptyPane.FirstChild is null && emptyPane.Node.Scroll is null && accessibleSource.Live.Count == 0,
            "an empty virtual list retains its pane while withdrawing all row providers and Scroll");

        var indexedSource = new AutomationSource { Editors = true };
        using var indexed = Dux.VirtualList(indexedSource, 1_000_000_000, new(32, 180, 100));
        Frame(indexed.Render);
        accessibility.TryGet(accessibility.Root.First!, out var indexedPane);
        using var wake = new ManualResetEventSlim();
        accessibility.SetRequestFrameCallback(wake.Set);
        T Request<T>(Func<T> action)
        {
            wake.Reset();
            var task = Task.Run(action);
            if (!wake.Wait(TimeSpan.FromSeconds(1)))
                throw new TimeoutException("Virtual item request did not wake the render writer.");
            Frame(indexed.Render);
            return task.GetAwaiter().GetResult();
        }
        (UiAccessibilityResult Result, string? Id) Find(UiVirtualItemSearchProperty property, string? value, UiAccessibilityItemReference? after = null)
        {
            var result = accessibility.FindVirtualItem(indexedPane.Node.Id, indexedPane.RuntimeId, property, value, after, out var found);
            return (result, found);
        }

        const int farIndex = 987654321;
        const string farKey = "987654321";
        var initialCreated = indexedSource.Created;
        var initialKeys = indexedSource.Keys;
        var initialRealized = indexed.RealizedCount;
        structureEvents.Clear();
        var lookup = Request(() => Find(UiVirtualItemSearchProperty.AutomationId, farKey));
        accessibility.TryGet(lookup.Id!, out var placeholder);
        check(lookup.Result == UiAccessibilityResult.Success && indexedPane.Node.VirtualItemContainer
            && placeholder.Node is { Role : UiAccessibilityRole.ListItem, VirtualItem : { IsPlaceholder : true, Index : farIndex } }
            && placeholder.FirstChild is null && placeholder.Node.ParentId == indexedPane.Node.Id
            && indexed.Offset == 0 && indexedSource.Created == initialCreated && indexed.RealizedCount == initialRealized
            && indexedSource.Lookups == 1 && indexedSource.Keys == initialKeys + 1 && structureEvents.Count == 1,
            "indexed lookup exposes one peer placeholder without moving, creating rows or scanning the model");

        var reference = new UiAccessibilityItemReference(placeholder.Node.Id, placeholder.RuntimeId);
        structureEvents.Clear();
        var realize = Request(() => accessibility.RealizeVirtualItem(indexedPane.Node.Id, indexedPane.RuntimeId, reference));
        check(realize == UiAccessibilityResult.Success && indexed.Offset == (double)farIndex * 32
            && indexedSource.Live.ContainsKey(farIndex) && indexedSource.Live.Count <= 6
            && accessibility.TryGet(reference.Id, out var realized) && realized.RuntimeId == reference.RuntimeId
            && realized.Node is { Offscreen : false, VirtualItem.IsPlaceholder : false }
            && realized.FirstChild is not null && structureEvents.Count == 1,
            "realization rebases the real row and publishes its children on the original provider");

        var byName = Request(() => Find(UiVirtualItemSearchProperty.Name, $"Item : {farKey}"));
        var next = Request(() => Find(UiVirtualItemSearchProperty.Next, null, reference));
        check(byName == (UiAccessibilityResult.Success, reference.Id) && next.Result == UiAccessibilityResult.Success
            && accessibility.TryGet(next.Id!, out var nextItem) && nextItem.Node.VirtualItem is { Index : farIndex + 1, IsPlaceholder : false },
            "name lookup returns the live provider and unfiltered lookup continues after the supplied row");

        var firstLookup = Request(() => Find(UiVirtualItemSearchProperty.Next, null));
        var miss = Request(() => Find(UiVirtualItemSearchProperty.AutomationId, "missing"));
        check(firstLookup.Result == UiAccessibilityResult.Success && firstLookup.Id is not null
            && miss == (UiAccessibilityResult.Success, null) && !accessibility.TryGet(firstLookup.Id, out _)
            && indexed.Offset == (double)farIndex * 32,
            "a new lookup retires the prior placeholder and a miss leaves the current realization intact");

        const int duplicateIndex = 900000000;
        indexedSource.KeyForIndex = static index => index == duplicateIndex ? farIndex : index;
        var duplicate = Request(() => Find(UiVirtualItemSearchProperty.Name, $"Item : {duplicateIndex}"));
        check(duplicate == (UiAccessibilityResult.InvalidOperation, null) && indexed.Offset == (double)farIndex * 32,
            "lookup rejects a duplicate key instead of returning the wrong realized row");
        indexedSource.KeyForIndex = static index => index;

        using var timedOut = new ManualResetEventSlim();
        indexedSource.OnLookup = () =>
        {
            if (!timedOut.Wait(TimeSpan.FromSeconds(4)))
                throw new TimeoutException("The virtual item requester did not time out.");
        };
        wake.Reset();
        structureEvents.Clear();
        var canceled = Task.Run(() =>
        {
            var result = Find(UiVirtualItemSearchProperty.AutomationId, "0");
            timedOut.Set();
            return result;
        });
        if (!wake.Wait(TimeSpan.FromSeconds(1)))
            throw new TimeoutException("Canceled virtual item lookup did not wake the render writer.");
        Frame(indexed.Render);
        check(canceled.GetAwaiter().GetResult() == (UiAccessibilityResult.Timeout, null) && structureEvents.Count == 0,
            "a lookup canceled while the source resolves does not publish a late placeholder");

        var nestedAutomation = new AutomationSource { Editors = true };
        var innerList = Dux.VirtualList(nestedAutomation, 1000, new(32, 160, 160));
        using var outerList = Dux.ScrollView(new UiColumn([innerList, Dux.SizedBox(height : 200)],
            new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min)), new(Width : 190, Height : 90));
        outerList.Offset = new(0, 1000);
        Frame(outerList.Render);
        accessibility.TryGet(accessibility.Root.First!, out var outerPane);
        accessibility.TryGet(outerPane.FirstChild!, out var nestedPane);
        check(nestedPane.Node.Offscreen && innerList.RealizedCount == 0,
            "a fully clipped nested virtual pane retains its provider without creating rows");

        T NestedRequest<T>(Func<T> action)
        {
            wake.Reset();
            var task = Task.Run(action);
            if (!wake.Wait(TimeSpan.FromSeconds(1)))
                throw new TimeoutException("Nested virtual item request did not wake the render writer.");
            for (var pass = 0; pass < 5; pass++)
            {
                Frame(outerList.Render);
                if (task.Wait(TimeSpan.FromMilliseconds(30)))
                    return task.GetAwaiter().GetResult();
            }
            throw new InvalidOperationException("Nested virtual item request did not complete within the frame budget.");
        }

        (UiAccessibilityResult Result, string? Id) FindNested(UiVirtualItemSearchProperty property, string? value,
            UiAccessibilityItemReference? after = null) => NestedRequest(() =>
        {
            var result = accessibility.FindVirtualItem(nestedPane.Node.Id, nestedPane.RuntimeId, property, value, after, out var found);
            return (result, found);
        });
        var hiddenLookup = FindNested(UiVirtualItemSearchProperty.AutomationId, "998");
        accessibility.TryGet(hiddenLookup.Id!, out var nestedPlaceholder);
        check(hiddenLookup.Result == UiAccessibilityResult.Success && innerList.RealizedCount == 0
            && nestedPlaceholder.Node.VirtualItem is { Index : 998, IsPlaceholder : true },
            "looking up a row in the hidden pane creates only its placeholder");

        static int SwapLast(int index) => index == 998 ? 999 : index == 999 ? 998 : index;
        nestedAutomation.KeyForIndex = SwapLast;
        nestedAutomation.IndexForKey = SwapLast;
        innerList.Refresh();
        Frame(outerList.Render);
        check(accessibility.TryGet(nestedPlaceholder.Node.Id, out var relocated) && relocated.RuntimeId == nestedPlaceholder.RuntimeId
            && relocated.Node.VirtualItem is { Index : 999, Key : "998", IsPlaceholder : true } && innerList.RealizedCount == 0,
            "refresh moves the held placeholder by stable key without realizing the hidden range");

        var relocatedReference = new UiAccessibilityItemReference(nestedPlaceholder.Node.Id, nestedPlaceholder.RuntimeId);
        var nestedResult = NestedRequest(() => accessibility.RealizeVirtualItem(nestedPane.Node.Id, nestedPane.RuntimeId, relocatedReference));
        check(nestedResult == UiAccessibilityResult.Success && outerList.Offset.Y > 0 && outerList.Offset.Y < 270
            && innerList.Offset == 32_000d - 160 && nestedAutomation.Live.ContainsKey(998)
            && accessibility.TryGet(relocatedReference.Id, out var nestedItem) && nestedItem.RuntimeId == relocatedReference.RuntimeId
            && nestedItem.Node is { Offscreen : false, VirtualItem : { Index : 999, Key : "998", IsPlaceholder : false } },
            "realization reveals the required bottom slice through the parent and shows the relocated key");

        var afterLast = FindNested(UiVirtualItemSearchProperty.Next, null, relocatedReference);
        check(afterLast == (UiAccessibilityResult.Success, null), "start-after uses the relocated key's current position");

        outerList.Offset = default;
        innerList.Offset = 0;
        Frame(outerList.Render);
        accessibility.TryGet(nestedPane.Node.Id, out var partiallyVisible);
        var tailLookup = FindNested(UiVirtualItemSearchProperty.AutomationId, "998");
        accessibility.TryGet(tailLookup.Id!, out var tailPlaceholder);
        var tailReference = new UiAccessibilityItemReference(tailPlaceholder.Node.Id, tailPlaceholder.RuntimeId);
        var tailResult = NestedRequest(() => accessibility.RealizeVirtualItem(nestedPane.Node.Id, nestedPane.RuntimeId, tailReference));
        check(!partiallyVisible.Node.Offscreen && tailResult == UiAccessibilityResult.Success && outerList.Offset.Y > 0
            && accessibility.TryGet(tailReference.Id, out var tailItem) && !tailItem.Node.Offscreen,
            "a partly visible pane still moves its parent when clamping leaves the last row below the clip");

        var removedLookup = FindNested(UiVirtualItemSearchProperty.AutomationId, "100");
        accessibility.TryGet(removedLookup.Id!, out var removedPlaceholder);
        var removedReference = new UiAccessibilityItemReference(removedPlaceholder.Node.Id, removedPlaceholder.RuntimeId);
        innerList.ItemCount = 50;
        Frame(outerList.Render);
        var removedResult = Task.Run(() => accessibility.RealizeVirtualItem(nestedPane.Node.Id, nestedPane.RuntimeId, removedReference))
            .GetAwaiter().GetResult();
        check(removedResult == UiAccessibilityResult.Unavailable && !accessibility.TryGet(removedReference.Id, out _),
            "removing the held key retires the placeholder and rejects realization");
        accessibility.SetRequestFrameCallback(null);

        var variedSource = new Source();
        var variedExtents = new PairedExtents(35, 65);
        using var varied = Dux.VirtualList(variedSource, 1_000_000_000,
            new UiVirtualListOptions(Width : 100, Height : 105) { ExtentIndex = variedExtents });
        Frame(ui =>
        {
            varied.MeasureConstrained(ui, new());
            check(variedExtents.Reads == 0 && variedSource.Created == 0,
                "variable-height measurement does not query row boundaries or create widgets");
        });
        varied.Offset = 50_000_000_000d - 105.25;
        Frame(varied.Render);
        check(variedSource.Live[999_999_999].Bounds == new UiRect(10, 50.25f, 100 - thickness, 65)
            && varied.Offset == 50_000_000_000d - 105.25 && varied.RealizedCount <= 6 && variedExtents.Reads <= 96,
            "billion-row variable geometry uses bounded boundary reads and preserves the fractional tail");

        var anchoredSource = new Source();
        using var anchored = Dux.VirtualList(anchoredSource, 1000,
            new UiVirtualListOptions(Width : 100, Height : 100) { ExtentIndex = new PairedExtents(35, 65) });
        anchored.Offset = 305.25;
        Frame(anchored.Render);
        var anchorWidget = anchoredSource.Live[6];
        var anchorY = anchorWidget.Bounds.Y;
        var replacement = new PairedExtents(48, 72);
        anchored.Options = anchored.Options with { ExtentIndex = replacement };
        Frame(anchored.Render);
        check(anchored.Offset == 365.25 && ReferenceEquals(anchorWidget, anchoredSource.Live[6])
            && anchorWidget.Bounds.Y == anchorY && anchorWidget.Bounds.Height == 48,
            "changing row heights preserves the anchored key, intra-row fraction and widget ownership");

        static int SwapAnchor(int index) => index == 6 ? 8 : index == 8 ? 6 : index;
        anchoredSource.KeyForIndex = SwapAnchor;
        replacement.IndexForKey = SwapAnchor;
        anchored.Refresh();
        Frame(anchored.Render);
        check(anchored.Offset == 485.25 && ReferenceEquals(anchorWidget, anchoredSource.Live[6]) && anchorWidget.Bounds.Y == anchorY,
            "reordering variable rows uses the key index and keeps the visible anchor in place");
        anchored.Options = anchored.Options with { ExtentIndex = new PairedExtents(40, 80) { IndexForKey = SwapAnchor } };
        anchored.Offset = 12.25;
        Frame(anchored.Render);
        check(anchored.Offset == 12.25 && anchoredSource.Live[0].Bounds.Y == -2.25f,
            "an explicit offset overrides a pending height-anchor adjustment");

        var clippedSource = new Source();
        using var clippedList = Dux.VirtualList(clippedSource, 100,
            new UiVirtualListOptions(Width : 100, Height : 100, Overscan : 0) { ExtentIndex = new PairedExtents(35, 65) });
        void ClipList(UiImmediateContext ui, float top)
        {
            using var clip = new UiControlContext(ui).Clip(new(10, top, 100, 25));
            clippedList.Render(ui);
        }
        clippedList.Offset = 290.25;
        Frame(ui => ClipList(ui, 30));
        check(clippedSource.Live[6].Bounds.Y == 19.75f, "the clipped list starts 10.25 units into its first visible row");
        Frame(ui => ClipList(ui, 180));
        clippedList.Options = clippedList.Options with { ExtentIndex = new PairedExtents(48, 72) };
        Frame(ui => ClipList(ui, 180));
        check(clippedList.Offset == 290.25 && clippedList.RealizedCount == 0,
            "a fully clipped list defers height anchoring without realizing hidden rows");
        Frame(ui => ClipList(ui, 65));
        check(clippedList.Offset == 315.25 && clippedSource.Live.TryGetValue(6, out var revealedAnchor)
            && revealedAnchor.Bounds.Y == 54.75f,
            "height anchoring uses the new clip inset when the list becomes visible again");

        var focusedSource = new Source { Editors = true };
        var focusedExtents = new PairedExtents(35, 65);
        using var focusedList = Dux.VirtualList(focusedSource, 100,
            new UiVirtualListOptions(Width : 180, Height : 80, Overscan : 0) { ExtentIndex = focusedExtents });
        focusedList.Offset = 305.25;
        Frame(focusedList.Render);
        var focusedRow = focusedSource.Live[6];
        Frame(focusedList.Render, idle with { MousePosition = new(30, 20), LeftMouseDown = true, LeftMousePressed = true });
        var focusedId = context.State.ActiveId;
        Frame(focusedList.Render, idle with { LeftMouseReleased = true });
        Frame(focusedList.Render, idle with { KeyEvents = [new(UiKey.End, true, default)], CharEvents = [new('x')] });
        focusedSource.KeyForIndex = SwapAnchor;
        focusedList.Options = focusedList.Options with { ExtentIndex = new PairedExtents(48, 72) { IndexForKey = SwapAnchor } };
        focusedList.Refresh();
        Frame(focusedList.Render, idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(focusedId is not null && context.State.ActiveId == focusedId && ReferenceEquals(focusedRow, focusedSource.Live[6])
            && focusedList.Offset == 485.25 && focusedSource.Values.GetValueOrDefault(6) == "a" && focusedSource.Writes == 2,
            "the anchored editor keeps focus and undo when its variable row is resized and reordered");

        using var invalidGeometry = Dux.VirtualList(new Source(), 4,
            new UiVirtualListOptions(Width : 100, Height : 100) { ExtentIndex = new PairedExtents(0, 60) });
        Frame(ui => check(Throws<InvalidOperationException>(() => invalidGeometry.Render(ui)),
            "a realized variable row must have strictly increasing boundaries"));
        list.Dispose();
        check(Throws<ObjectDisposedException>(() => list.Offset = 0), "disposed virtual lists reject further mutation");
    }

    private sealed class PairedExtents(float even, float odd) : IUiVirtualListExtentIndex
    {
        internal int Reads;
        internal Func<int, int>? IndexForKey;

        public double GetBoundary(int index)
        {
            Reads++;
            return (double)(index / 2) * (even + odd) + (index % 2 == 0 ? 0 : even);
        }

        public int FindIndexByKey(string key, int itemCount)
        {
            if (!int.TryParse(key, out var value))
                return -1;
            var index = IndexForKey?.Invoke(value) ?? value;
            return index >= 0 && index < itemCount ? index : -1;
        }
    }

    internal class Source : IUiVirtualListSource
    {
        internal readonly Dictionary<int, Row> Live = [];
        internal readonly Dictionary<int, string> Values = [];
        internal Func<int, int> KeyForIndex = static index => index;
        internal int Keys, Created, Binds, Disposed, MaxLive, Reads, Writes;
        internal int? ThrowCreateAt;
        internal bool Editors;
        internal Action? RenderAction;
        public string GetKey(int index)
        {
            Keys++;
            return KeyForIndex(index).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public UiWidget Create(int index)
        {
            if (index == ThrowCreateAt)
                throw new InvalidOperationException("Deliberate row factory failure.");
            var key = KeyForIndex(index);
            var row = new Row(this, key);
            Created++;
            Live.Add(key, row);
            MaxLive = Math.Max(MaxLive, Live.Count);
            return row;
        }
        public void Bind(UiWidget widget, int index)
        {
            Binds++;
            if (((Row)widget).Key != KeyForIndex(index))
                throw new InvalidOperationException("Rebound key mismatch.");
        }
    }

    private sealed class AutomationSource : Source, IUiVirtualListAutomationSource
    {
        internal int Lookups;
        internal Action? OnLookup;
        internal Func<int, int>? IndexForKey;
        public string GetAccessibleItemName(int index) => $"Item : {index}";

        public int FindAccessibleItem(UiVirtualItemSearchProperty property, string value, int startIndex, int itemCount)
        {
            Lookups++;
            OnLookup?.Invoke();
            const string prefix = "Item : ";
            if (property == UiVirtualItemSearchProperty.Name && !value.StartsWith(prefix, StringComparison.Ordinal))
                return -1;
            var digits = property == UiVirtualItemSearchProperty.Name ? value.AsSpan(prefix.Length) : value.AsSpan();
            if (!int.TryParse(digits, out var key))
                return -1;
            var index = IndexForKey?.Invoke(key) ?? key;
            return index >= startIndex && index < itemCount ? index : -1;
        }
    }

    internal sealed class Row : UiControlWidget
    {
        private readonly Source _source;
        private readonly UiWidget? _editor;
        internal readonly int Key;
        internal UiRect Bounds;
        internal int Renders, Disposals;
        internal Row(Source source, int key)
        {
            _source = source;
            Key = key;
            if (source.Editors)
            {
                _editor = Fluent.Input("Row editor", new UiBinding<string>(
                    () =>
                    {
                        source.Reads++;
                        return source.Values.GetValueOrDefault(key, "a");
                    },
                    value =>
                    {
                        source.Writes++;
                        source.Values[key] = value;
                    }), 256);
                OwnChild("editor", _editor);
            }
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Fixed rows need no item measurement.");
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            var constraints = new UiControlContext(ui).LayoutConstraints;
            var position = ui.GetCursorScreenPos();
            Bounds = new(position.X, position.Y, constraints.MaxWidth, constraints.MaxHeight);
            _source.RenderAction?.Invoke();
            if (_editor is not null)
                _editor.Render(ui);
            else
                ui.Dummy(new(Bounds.Width, Bounds.Height));
        }
        protected override void OnDispose()
        {
            Disposals++;
            _source.Disposed++;
            _source.Live.Remove(Key);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
