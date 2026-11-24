using Duxel.Core;
using Duxel.Gallery;

internal static class CardNudgeTests
{
    internal static void Run(Action<bool, string> check)
    {
        CardNudgePolicyTests.Run(check);
        RunMessageSlots(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var frameRequests = 0;
        context.SetRequestFrameCallback(() => frameRequests++);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var open = true;
        var accept = true;
        var writes = 0;
        var actions = 0;
        var binding = new UiBinding<bool>(() => open, next => { writes++; if (accept) open = next; });
        using var root = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min),
            Fluent.CardNudge("Suggestion", "One helpful tip", binding,
                action: Fluent.Button("Use tip", () => actions++), options: new(Width: 280)),
            Fluent.Button("Next", () => { }));
        var screen = new Screen(root);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => accessibility.CaptureSnapshot().Elements
            .Single(e => e.Node.Role == role && e.Node.Name == name);
        UiRect Next() => Find("Next", UiAccessibilityRole.Button).Node.Bounds;
        bool HasCard() => accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Card && e.Node.Name == "Suggestion");
        void Invoke(string name, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var element = Find(name, UiAccessibilityRole.Button);
            accessibility.RequestAction(element.Node.Id, element.RuntimeId, action, wait: false);
            Frame();
        }
        void Drag(UiVector2 from, UiVector2 to)
        {
            Frame(idle with { MousePosition = from, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = to, LeftMouseDown = true });
            Frame(idle with { MousePosition = to, LeftMouseReleased = true });
            Frame();
        }

        Frame();
        var card = Find("Suggestion", UiAccessibilityRole.Card).Node;
        var start = new UiVector2(card.Bounds.X + card.Bounds.Width / 2, card.Bounds.Y + card.Bounds.Height / 2);
        var nextY = Next().Y;
        check(Find("Dismiss", UiAccessibilityRole.Button).Node.ParentId == card.Id
            && Find("Use tip", UiAccessibilityRole.Button).Node.ParentId == card.Id && nextY > card.Bounds.Y,
            "CardNudge publishes named real actions inside the inline card and allocates following content");
        Invoke("Use tip");
        check(actions == 1 && writes == 0 && open, "the card action remains independent from dismissal");
        Drag(start, start with { Y = start.Y + 70 });
        check(writes == 0 && open, "vertical drag does not dismiss the card");
        var action = Find("Use tip", UiAccessibilityRole.Button).Node.Bounds;
        var actionPoint = new UiVector2(action.X + action.Width / 2, action.Y + action.Height / 2);
        Drag(actionPoint, actionPoint with { X = actionPoint.X + 90 });
        check(writes == 0 && actions == 1, "a drag beginning on the action cannot dismiss or fire the action");

        accept = false;
        Invoke("Dismiss");
        Frame();
        check(writes == 1 && HasCard(), "rejected controlled dismissal leaves the live card visible");
        accept = true;
        Invoke("Dismiss", UiAccessibilityAction.Focus);
        var beforeDismiss = frameRequests;
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        check(frameRequests > beforeDismiss, "closing between layout and paint requests a follow-up frame in event-wait mode");
        Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        Frame();
        check(!open && writes == 2 && !HasCard() && Find("Next", UiAccessibilityRole.Button).Node.Focused,
            "keyboard dismissal removes the live card and retires focus to the next button");
        Frame();
        check(Next().Y < nextY && Next().Y > 0 && context.State.NextScheduledFrameTimeSeconds.HasValue,
            "following content moves while frozen exit paint fades");
        for (var i = 0; i < 18; i++) Frame();
        check(Next().Y == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "CardNudge collapse settles at zero height and releases event scheduling");

        foreach (var direction in new[] { -1, 1 })
        {
            open = true;
            Frame();
            check(HasCard() && Next().Y == nextY, "external binding can show the card again");
            Drag(start, start with { X = start.X + direction * 70 });
            check(!open && !HasCard(), "horizontal swipe dismisses from either direction");
        }

        var reducedOpen = new UiState<bool>(false);
        using var reduced = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min),
            Fluent.CardNudge("Suggestion", "One helpful tip", reducedOpen.Binding,
                options: new(Width: 160, ReducedMotion: true, CloseLabel: null)),
            Fluent.Button("Next", () => { }));
        screen = new Screen(reduced);
        Frame();
        var closedY = Next().Y;
        check(!HasCard() && context.State.NextScheduledFrameTimeSeconds is null, "initially closed nudge has no entry paint or deadline");
        reducedOpen.Value = true;
        Frame();
        check(HasCard() && Next().Y > closedY
            && !accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Dismiss"),
            "the optional close slot can be omitted when a caller supplies its own dismissal");
        reducedOpen.Value = false;
        Frame();
        check(!HasCard() && Next().Y == closedY && context.State.NextScheduledFrameTimeSeconds is null,
            "reduced motion removes layout space immediately");
    }

    private static void RunMessageSlots(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var open = new UiState<bool>(true);
        var invoked = 0;
        var actionInvoked = 0;
        var iconBounds = new List<UiRect>();
        var icon = new UiMenuIcon((_, bounds, _, _, _) => iconBounds.Add(bounds));
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min),
            Fluent.CardNudge("Suggestion", "Details", open.Binding,
                action : Fluent.Button("Separate action", () => actionInvoked++),
                options : new(Width : 300, ReducedMotion : true)
                {
                    LeadingIcon = icon,
                    AccentIcon = icon,
                    AccentText = "Accent",
                    MessageAction = () => invoked++,
                }), Fluent.Button("Next", () => { }));
        var screen = new Screen(root);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState input)
        {
            iconBounds.Clear();
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Button(string name) => accessibility.CaptureSnapshot().Elements.Single(
            element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == name);

        Frame(idle);
        var message = Button("Suggestion");
        check(iconBounds.Count == 2 && iconBounds[0].Width == 24 && iconBounds[1].Width == 12
            && iconBounds[0].X < iconBounds[1].X,
            "CardNudge paints both decorative icon slots at their logical sizes");
        check(accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Accent")
            && Button("Separate action").Node.ParentId == message.Node.ParentId,
            "CardNudge publishes accent text and separate sibling actions");
        var point = new UiVector2(message.Node.Bounds.X + 12, message.Node.Bounds.Y + 10);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        check(invoked == 0 && open.Value, "CardNudge message tap waits for release");
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(invoked == 1 && actionInvoked == 0 && open.Value, "CardNudge message tap invokes only its action");

        var action = Button("Separate action");
        accessibility.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame(idle);
        check(invoked == 1 && actionInvoked == 1 && open.Value, "CardNudge action widget stays independent");
        accessibility.RequestAction(message.Node.Id, message.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        Frame(idle);
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        check(invoked == 2 && open.Value, "CardNudge message supports keyboard invocation");

        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        var end = point with { X = point.X + 72 };
        Frame(idle with { MousePosition = end, LeftMouseDown = true });
        Frame(idle with { MousePosition = end, LeftMouseReleased = true });
        Frame(idle);
        check(!open.Value && invoked == 2, "CardNudge message swipe dismisses without invoking the message");
        check(!accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Id == message.Node.Id),
            "CardNudge retires the message action with the dismissed card");
    }

    internal static void RunGallery(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 900));
        using var accessibility = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(accessibility);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "CardNudge" };
        var pixels = fixture.Capture(screen.Render);
        check(screen.SourceCountForPage("CardNudge") == 1 && !Fluent.MissingComponentNames.Contains("CardNudge")
            && accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Link
                && element.Node.Name == "CardNudge" && element.Node.ItemStatus is null),
            "CardNudge keeps its Gallery route and adjacent source after the next addition");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-cardnudge.png"), pixels);
        using var page = new CardNudgePage();
        var sample = page.Samples.Single();
        sample.SynchronizeSource();
        check(sample.Source.Contains("Fluent.CardNudge(", StringComparison.Ordinal), "the preview exports the public CardNudge factory");
        check(sample.Source.Contains("LeadingIcon", StringComparison.Ordinal)
            && sample.Source.Contains("AccentIcon", StringComparison.Ordinal)
            && sample.Source.Contains("MessageAction", StringComparison.Ordinal),
            "the Gallery source exports the optional CardNudge slots");
        File.WriteAllText(Path.Combine(output, "cardnudge-example.cs"), "using Duxel.Core;\nUiImmediateContext ui = null!;\n" + sample.Source);

        var message = accessibility.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "빠르게 찾아보세요");
        accessibility.RequestAction(message.Node.Id, message.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        fixture.Capture(screen.Render);
        fixture.Capture(screen.Render);
        check(accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Name == "즐겨찾기 팁을 열었습니다."),
            "the Gallery message updates the caption without dismissing the suggestion");

        var action = accessibility.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "즐겨찾기에 추가");
        accessibility.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        fixture.Capture(screen.Render);
        fixture.Capture(screen.Render);
        check(accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Name == "즐겨찾기에 추가했습니다.")
            && !accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Id == action.Node.Id),
            "the Gallery action updates the following content and retires the dismissed card");
    }

    private sealed class Screen(UiWidget root) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            root.Render(ui);
        }
    }
}
