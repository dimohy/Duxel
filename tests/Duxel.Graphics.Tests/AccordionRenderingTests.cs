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
    }
}
