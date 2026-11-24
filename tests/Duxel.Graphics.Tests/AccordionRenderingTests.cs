using System.Collections.Immutable;
using Duxel.Core;

internal static class AccordionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width,
        Action<UiInputState> setInput, UiContext ownerContext)
    {
        var state = ownerContext.State;
        using var accessibility = new UiAccessibilityTree();
        ownerContext.SetAccessibilityTree(accessibility);
        var value = new UiState<ImmutableArray<string>>(ImmutableArray<string>.Empty);
        using var accordion = Fluent.Accordion("GPU Accordion", value.Binding, new UiAccordionItem[]
        {
            new("a", "First wrapped Accordion header", Fluent.Button("First panel body")),
            new("b", "Second section", Fluent.Button("Second panel body")),
            new("c", "Disabled section", Fluent.Body1("Disabled body"), false)
        }, 440, new(Collapsible: true));
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, new(0, 0, 0, 0), 0, 0);
            ui.SetCursorScreenPos(new(24, 24));
            accordion.Render(ui);
        }
        byte[] CaptureSettled(string name)
        {
            for (var frame = 0; frame < 12 && state.NextScheduledFrameTimeSeconds.HasValue; frame++)
                capture(name + "-transition", Render);
            check(state.NextScheduledFrameTimeSeconds is null, "GPU Accordion settles its disclosure before comparing pixels : " + name);
            return capture(name, Render);
        }
        var closed = capture("closed", Render);
        var visible = false;
        for (var index = 4; index < closed.Length; index += 4)
            visible |= closed[index] != closed[0] || closed[index + 1] != closed[1] || closed[index + 2] != closed[2];
        check(visible, "GPU Accordion fixture establishes a nonempty root-layout rendering before state comparisons");
        value.Value = ImmutableArray.Create("a");
        var expanded = capture("expanded", Render);
        check(!closed.AsSpan().SequenceEqual(expanded), "GPU Accordion expansion adds actual body content and reflows following headers");
        var settledExpanded = CaptureSettled("expanded-settled");
        check(settledExpanded.AsSpan().SequenceEqual(capture("replay", Render)), "GPU Accordion unchanged retained frame reproduces identical pixels");
        value.Value = ImmutableArray.Create("b");
        check(!settledExpanded.AsSpan().SequenceEqual(capture("second", Render)), "GPU single Accordion selection replaces its real panel content");
        value.Value = ImmutableArray<string>.Empty;
        capture("collapse-start", Render);
        check(closed.AsSpan().SequenceEqual(CaptureSettled("collapsed")), "GPU collapse exactly restores the closed frame");
        accordion.Options = accordion.Options with
        {
            ChevronPosition = UiAccordionChevronPosition.End
        };
        check(!closed.AsSpan().SequenceEqual(capture("end-chevron", Render)), "GPU end-chevron layout changes header placement without changing panel state");
        accordion.Options = accordion.Options with
        {
            Size = UiAccordionSize.Small
        };
        check(!closed.AsSpan().SequenceEqual(capture("small", Render)), "GPU small Accordion headers use their distinct metrics and typography");
        accordion.Width = 120;
        var narrow = capture("narrow", Render);
        var outside = true;
        for (var y = 0; y < narrow.Length / (width * 4); y++)
            for (var x = 145; x < width; x++)
                for (var channel = 0; channel < 3; channel++)
                    outside &= narrow[(y * width + x) * 4 + channel] == narrow[channel];
        check(outside, "GPU narrow wrapped Accordion cannot paint beyond its allocated right edge");
        accordion.Width = 260;
        accordion.Options = new(Collapsible: true);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        setInput(idle);
        var rest = capture("interaction-rest", Render);
        var pointer = idle with
        {
            MousePosition = new(100, 46)
        };
        setInput(pointer);
        var hover = capture("interaction-hover", Render);
        check(state.MouseCursor == UiMouseCursor.Hand && hover.AsSpan().SequenceEqual(rest),
            "GPU Accordion matches official transparent hover pixels and pointer cursor");
        setInput(pointer with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("interaction-press", Render);
        check(value.Value.IsEmpty, "GPU Accordion pointer press does not toggle before release");
        setInput(pointer with
        {
            LeftMouseReleased = true
        });
        capture("interaction-release", Render);
        check(value.Value.SequenceEqual(new[] { "a" }), "GPU Accordion real release opens the intended section");
        value.Value = ImmutableArray<string>.Empty;
        var disabledPointer = idle with
        {
            MousePosition = new(100, 134)
        };
        setInput(disabledPointer);
        capture("interaction-disabled-hover", Render);
        check(state.MouseCursor == UiMouseCursor.NotAllowed, "GPU disabled Accordion matches the official not-allowed cursor");
        setInput(disabledPointer with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("interaction-disabled-press", Render);
        setInput(disabledPointer with
        {
            LeftMouseReleased = true
        });
        capture("interaction-disabled-release", Render);
        check(value.Value.IsEmpty, "GPU disabled Accordion ignores actual pointer activation");
        check(accessibility.TryGet(accordion.HeaderIds[0], out var firstHeader)
            && accessibility.RequestAction(firstHeader.Node.Id, firstHeader.RuntimeId, UiAccessibilityAction.Focus, wait: false)
                == UiAccessibilityResult.Success, "GPU Accordion establishes the first header as the keyboard starting point");
        setInput(idle);
        capture("interaction-keyboard-start", Render);
        setInput(idle with
        {
            KeyEvents = [new(UiKey.Tab, true, default)]
        });
        var focused = capture("interaction-keyboard-focus", Render);
        check(!focused.AsSpan().SequenceEqual(rest) && state.MouseCursor == UiMouseCursor.Arrow,
            "GPU Accordion keyboard navigation paints the official focus affordance");
        setInput(idle with
        {
            KeyEvents = [new(UiKey.Enter, true, default)]
        });
        capture("interaction-keyboard-toggle", Render);
        check(value.Value.SequenceEqual(new[] { "b" }), "GPU Accordion keyboard Enter activates the next focused header");
        ownerContext.SetAccessibilityTree(null);

        VerifyDisclosureDirection(capture, check, width, setInput, state);
        VerifyForcedColors(capture, check, width);
        VerifyPanelMotion(capture, check, width, setInput, ownerContext);
    }

    private static void VerifyPanelMotion(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, Action<UiInputState> setInput, UiContext owner)
    {
        using var tree = new UiAccessibilityTree();
        owner.SetAccessibilityTree(tree);
        var selection = new UiState<ImmutableArray<string>>(ImmutableArray<string>.Empty);
        var swatch = new MotionSwatch();
        using var widget = Fluent.Accordion("Motion", selection.Binding, new UiAccordionItem[]
        {
            new("first", "Motion header", Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), swatch, Fluent.Button("Motion action"))),
            new("second", "Following header", Fluent.Body1("Second panel"))
        }, 220, new UiAccordionOptions(Collapsible : true) { AnimatePanels = true });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        setInput(idle);

        var closed = capture("motion-closed", Render);
        var collapsedY = widget.HeaderBounds[1].Y;
        selection.Value = ImmutableArray.Create("first");
        capture("motion-open-start", Render);
        check(widget.HeaderBounds[1].Y == collapsedY && widget.NextPanelFrame.HasValue,
            "GPU Accordion begins opening without jumping to the full height");
        owner.State.AdvanceTime(.09f);
        var midway = capture("motion-open-midway", Render);
        var openingY = widget.HeaderBounds[1].Y;
        owner.State.AdvanceTime(.2f);
        var open = capture("motion-open", Render);
        var expandedY = widget.HeaderBounds[1].Y;
        check(collapsedY < openingY && openingY < expandedY && !widget.NextPanelFrame.HasValue,
            "GPU Accordion opening reserves an intermediate height and settles");
        var pixel = (70 * width + 50) * 4;
        check(!midway.AsSpan(pixel, 3).SequenceEqual(closed.AsSpan(pixel, 3)) &&
            !midway.AsSpan(pixel, 3).SequenceEqual(open.AsSpan(pixel, 3)) &&
            open.AsSpan(pixel, 3).SequenceEqual(new byte[] { 209, 65, 33 }),
            "GPU Accordion opening paints the body at intermediate opacity");
        check(tree.TryGet(widget.PanelIds[0], out _) &&
            tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Motion action"),
            "GPU Accordion exposes the open panel and its action");

        selection.Value = ImmutableArray<string>.Empty;
        var drawsBeforeExit = swatch.Draws;
        capture("motion-close-start", Render);
        check(widget.HeaderBounds[1].Y == expandedY && !tree.TryGet(widget.PanelIds[0], out _) &&
            !tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Motion action"),
            "GPU Accordion closes accessibility immediately while preserving exit geometry");
        owner.State.AdvanceTime(.09f);
        var exiting = capture("motion-close-midway", Render);
        check(widget.HeaderBounds[1].Y > collapsedY && widget.HeaderBounds[1].Y < expandedY &&
            swatch.Draws == drawsBeforeExit && !exiting.AsSpan(pixel, 3).SequenceEqual(closed.AsSpan(pixel, 3)),
            "GPU Accordion shrinks and paints only the retained snapshot after closing");
        selection.Value = ImmutableArray.Create("first");
        var beforeReopen = widget.HeaderBounds[1].Y;
        capture("motion-reopen", Render);
        check(widget.HeaderBounds[1].Y > collapsedY && widget.HeaderBounds[1].Y <= beforeReopen &&
            tree.TryGet(widget.PanelIds[0], out _), "GPU Accordion reverses an interrupted exit and restores live content");
        widget.Options = widget.Options with { ReducedMotion = true };
        capture("motion-reduced", Render);
        check(widget.HeaderBounds[1].Y == expandedY && !widget.NextPanelFrame.HasValue,
            "GPU Accordion reduced motion settles without a panel deadline");
        selection.Value = ImmutableArray<string>.Empty;
        capture("motion-reduced-closed", Render);
        check(widget.HeaderBounds[1].Y == collapsedY && !widget.NextPanelFrame.HasValue &&
            !tree.TryGet(widget.PanelIds[0], out _), "GPU Accordion reduced motion closes the body immediately");
        owner.SetAccessibilityTree(null);

        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, default, 0, 0);
            ui.SetCursorScreenPos(new(24, 24));
            widget.Render(ui);
        }
    }

    private sealed class MotionSwatch : UiControlWidget
    {
        internal int Draws { get; private set; }

        protected override UiVector2 MeasureContent(UiControlContext context) => new(160, 32);

        protected override void RenderControl(UiImmediateContext ui)
        {
            Draws++;
            var bounds = ui.AllocateLayoutBox(new(160, 32));
            var context = new UiControlContext(ui);
            context.Frame(new(bounds.X, bounds.Y, 160, 32), new UiColor(209, 65, 33), default, 0, 0);
            ui.FinishLayoutBox(bounds, new(160, 32));
        }
    }

    private static void VerifyForcedColors(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width)
    {
        var palette = new UiForcedColorPalette(new(5, 10, 15), new(240, 230, 220), new(140, 150, 155),
            new(20, 90, 180), new(250, 240, 230), new(30, 200, 220), new(35, 45, 55), new(210, 220, 230));
        var header = new UiRect(24, 24, 220, 44);
        var icon = new UiMenuIcon((context, bounds, color, _, _) => context.Frame(bounds, color, default, 0, 0));
        using var body = Fluent.Body1("Body");
        foreach (var (itemEnabled, parentEnabled, focused) in new[]
        {
            (true, true, false), (true, true, true), (false, true, false), (true, false, false)
        })
        {
            var name = $"forced-{itemEnabled}-{parentEnabled}-{focused}";
            var item = new UiAccordionItem("header", "Accordion header", body, itemEnabled, icon);
            var foreground = itemEnabled && parentEnabled ? palette.CanvasText : palette.GrayText;
            var pixels = capture(name, ui =>
            {
                var context = Background(ui);
                using var scope = ui.BeginFluentProvider(new(palette.Apply(context.Theme)));
                ui.BeginDisabled(!parentEnabled);
                try
                {
                    var options = new UiAccordionOptions();
                    var layout = UiFluentAccordionPresentation.MeasureHeader(context, item, header.Width, options);
                    UiFluentAccordionPresentation.PaintHeader(context, item, layout, header, false,
                        new() { FocusVisible = focused }, options);
                }
                finally { ui.EndDisabled(); }
            });
            check(Matches(pixels, 70, 46, foreground), "GPU Accordion forced colors paint the optional icon " + name);
            check(Enumerable.Range(36, 20).Any(y => Enumerable.Range(90, 145).Any(x => Matches(pixels, x, y, foreground))),
                "GPU Accordion forced colors paint the header text " + name);
            check(Matches(pixels, 180, 26, focused ? palette.Highlight : palette.Canvas),
                "GPU Accordion forced colors match the focus outline " + name);
            var reference = capture(name + "-reference", ui =>
            {
                var context = Background(ui);
                UiDisclosureMotion.Paint(context, new(44, 46), foreground, 1.5f, -90);
            });
            var chevronMatches = true;
            for (var y = 40; y < 53; y++)
            {
                var offset = (y * width + 38) * 4;
                chevronMatches &= pixels.AsSpan(offset, 13 * 4).SequenceEqual(reference.AsSpan(offset, 13 * 4));
            }
            check(chevronMatches, "GPU Accordion forced colors match the disclosure color " + name);
        }

        UiControlContext Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, palette.Canvas, default, 0, 0);
            return context;
        }

        bool Matches(byte[] pixels, int x, int y, UiColor color)
        {
            var offset = (y * width + x) * 4;
            return pixels[offset] == (byte)color.Rgba && pixels[offset + 1] == (byte)(color.Rgba >> 8) &&
                pixels[offset + 2] == (byte)(color.Rgba >> 16);
        }
    }

    private static void VerifyDisclosureDirection(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, Action<UiInputState> setInput, UiState state)
    {
        var selection = new UiState<ImmutableArray<string>>(ImmutableArray<string>.Empty);
        var iconBounds = default(UiRect);
        var icon = new UiMenuIcon((_, bounds, _, _, _) => iconBounds = bounds);
        using var widget = Fluent.Accordion("Direction", selection.Binding,
            new UiAccordionItem[] { new("one", "Header", Fluent.Body1("Body"), Icon : icon) }, 220, new(Collapsible : true));
        setInput(new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default));

        foreach (var (direction, position, chevronX, iconX, closedAngle, openAngle) in new[]
        {
            (UiFlowDirection.LeftToRight, UiAccordionChevronPosition.Start, 44, 62, -90, 0),
            (UiFlowDirection.LeftToRight, UiAccordionChevronPosition.End, 226, 36, 0, 180),
            (UiFlowDirection.RightToLeft, UiAccordionChevronPosition.Start, 224, 186, 90, 0),
            (UiFlowDirection.RightToLeft, UiAccordionChevronPosition.End, 42, 212, 0, 180)
        })
        {
            widget.Options = widget.Options with { ChevronPosition = position };
            foreach (var expanded in new[] { false, true })
            {
                selection.Value = expanded ? ImmutableArray.Create("one") : ImmutableArray<string>.Empty;
                var name = $"{direction}-{position}-{(expanded ? "open" : "closed")}";
                var actual = capture(name, Render);
                for (var frame = 0; frame < 12 && state.NextScheduledFrameTimeSeconds.HasValue; frame++)
                    actual = capture(name + "-settled", Render);
                check(iconBounds == new UiRect(iconX, 36, 20, 20),
                    "GPU Accordion positions the borrowed icon at logical start " + name);
                var expected = capture(name + "-reference", ui =>
                {
                    var context = Background(ui);
                    var foreground = context.FluentColorScheme == UiFluentColorScheme.Dark
                        ? new UiColor(255, 255, 255) : new(36, 36, 36);
                    UiDisclosureMotion.Paint(context, new(chevronX, 46), foreground, 1.5f, expanded ? openAngle : closedAngle);
                });
                var matches = true;
                for (var y = 40; y < 53; y++)
                {
                    var offset = (y * width + chevronX - 6) * 4;
                    matches &= actual.AsSpan(offset, 13 * 4).SequenceEqual(expected.AsSpan(offset, 13 * 4));
                }
                check(matches, "GPU Accordion matches the reference disclosure position and direction " + name);
            }

            void Render(UiImmediateContext ui)
            {
                Background(ui);
                using var provider = ui.BeginFluentProvider(null, direction : direction);
                ui.SetCursorScreenPos(new(24, 24));
                widget.Render(ui);
            }
        }

        static UiControlContext Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, new(0, 0, 0, 0), 0, 0);
            return context;
        }
    }
}
