using Duxel.Core;

internal static class AccessibilityPublicationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        var initial = tree.CaptureSnapshot();
        check(initial.Revision == 0 && initial.Elements.Count == 0 && initial.First is null, "An unpublished accessibility snapshot is explicitly empty");
        var publications = 0;
        var reentryRejected = false;
        var disposeRejected = false;
        UiAccessibilitySnapshot? observed = null;
        tree.Published += () =>
        {
            publications++;
            observed = tree.CaptureSnapshot();
            try
            {
                tree.BeginFrame();
            }
            catch (InvalidOperationException) { reentryRejected = true; }
            try
            {
                tree.Dispose();
            }
            catch (InvalidOperationException) { disposeRejected = true; }
        };
        void Frame(string first, string? second, string focus)
        {
            tree.BeginFrame();
            tree.Add(new("pane", "", UiAccessibilityRole.Pane, "Pane", new(0, 0, 200, 100), true, false, false, Focusable: false));
            tree.Add(new(first, "pane", UiAccessibilityRole.Button, first, new(0, 0, 50, 30), true, false, false));
            if (second is not null)
                tree.Add(new(second, "pane", UiAccessibilityRole.Button, second, new(60, 0, 50, 30), true, false, false));
            tree.EndFrame(focus, authoritativeFocus: true);
        }
        Frame("a", "b", "a");
        var first = tree.CaptureSnapshot();
        check(publications == 1 && observed?.Revision == first.Revision && first.Focus == "a" && first.Elements.Count == 3,
            "Publication observers capture one complete revision outside reader locks");
        check(reentryRejected && disposeRejected, "Publication observers cannot reenter frame publication or disposal");
        Frame("a", "b", "a");
        check(publications == 1 && tree.CaptureSnapshot().Revision == first.Revision, "Unchanged frames do not announce a new accessibility publication");
        var bRuntime = first.Elements.Single(e => e.Node.Id == "b").RuntimeId;
        Frame("b", "a", "b");
        var moved = tree.CaptureSnapshot();
        check(moved.Elements.Single(e => e.Node.Id == "pane").FirstChild == "b"
            && moved.Elements.Single(e => e.Node.Id == "b").Next == "a" && moved.Focus == "b",
            "An atomic snapshot preserves reordered siblings and authoritative focus");
        Frame("a", null, "a");
        Frame("a", "b", "a");
        check(tree.CaptureSnapshot().Elements.Single(e => e.Node.Id == "b").RuntimeId != bRuntime,
            "Removing and recreating a logical identifier never aliases an old accessibility runtime object");
        check(first.Elements.Single(e => e.Node.Id == "pane").FirstChild == "a" && first.Focus == "a" && first.Elements.Count == 3,
            "A retained snapshot is unchanged by later publications");
        var readOnly = false;
        try
        {
            ((IList<UiAccessibilityElement>)first.Elements)[0] = default;
        }
        catch (NotSupportedException) { readOnly = true; }
        check(readOnly, "Snapshot consumers cannot mutate the published element array");
        using var stop = new CancellationTokenSource();
        using var ready = new ManualResetEventSlim();
        var reader = Task.Run(() =>
        {
            var copies = 0;
            do
            {
                var copy = tree.CaptureSnapshot();
                var ids = copy.Elements.Select(e => e.Node.Id).ToHashSet();
                if (!ids.Contains(copy.Focus!) || copy.Elements.Any(e => e.Node.ParentId.Length != 0 && !ids.Contains(e.Node.ParentId)))
                    throw new InvalidOperationException("A concurrent snapshot exposed mixed revisions.");
                copies++;
                ready.Set();
            } while (!stop.IsCancellationRequested);
            return copies;
        });
        if (!ready.Wait(TimeSpan.FromSeconds(2)))
            throw new TimeoutException("The snapshot reader did not start.");
        try
        {
            for (var i = 0; i < 200; i++)
                Frame(i % 2 == 0 ? "a" : "b", null, i % 2 == 0 ? "a" : "b");
        }
        finally { stop.Cancel(); }
        check(reader.GetAwaiter().GetResult() > 0, "Concurrent snapshots keep ancestry and focus from the same revision");
        tree.Dispose();
        var disposed = false;
        try
        {
            tree.CaptureSnapshot();
        }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "Disposed trees reject new snapshots while earlier copies remain readable");
        InputFocus(check);
    }

    private static void InputFocus(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "unchanged";
        var disabled = false;
        var label = "Counter : 1";
        UiImmediateContext? lastUi = null;
        void Render(UiImmediateContext ui)
        {
            lastUi = ui;
            ui.SetCursorScreenPos(new(10, 10));
            ui.BeginDisabled(disabled);
            ui.InputText("Legacy input", ref value, 100, UiInputTextFlags.ReadOnly);
            ui.EndDisabled();
            ui.SetCursorScreenPos(new(10, 70));
            ui.Button("stable-counter", label, new(160, 32));
        }
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(320, 200), new(1, 1)), idle, new(0, 0, 320, 200), UiTextSettings.Default);
            context.Render(new Screen(Render));
            context.GetDrawData().ReleasePooled();
        }
        Frame();
        var input = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Edit);
        var button = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button);
        check(input.Node.Focusable && input.Node.ReadOnly && !input.Node.Focused, "Legacy read-only inputs advertise focus without claiming initial focus");
        check(tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "Legacy input focus requests enter the common action queue");
        Frame();
        Frame();
        check(tree.TryGet(input.Node.Id, out var focused) && focused.Node.Focused && value == "unchanged",
            "Legacy input accessibility focus survives later frames without editing read-only text");
        label = "Counter : 2";
        Frame();
        check(tree.TryGet(button.Node.Id, out var renamed) && renamed.RuntimeId == button.RuntimeId && renamed.Node.Name == label,
            "The explicit button identifier keeps one native identity when its displayed name changes");
        disabled = true;
        Frame();
        check(tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Disabled,
            "Disabled legacy inputs reject external focus requests");
        var invalid = false;
        try
        {
            lastUi!.Button("", "label", new(100, 32));
        }
        catch (ArgumentException) { invalid = true; }
        check(invalid, "Explicit button identifiers reject empty values before rendering");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
