using Duxel.Core;

internal static class DialogExitTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var reads = 0;
        var calls = 0;
        var backgroundCalls = 0;
        var closeAction = Fluent.Button("Closed action", () => calls++);
        using var dialog = Fluent.Dialog("Exit dialog", Fluent.Body1(() => "Frozen content " + ++reads), [closeAction]);
        using var background = Fluent.Button("Background action", () => backgroundCalls++);
        using var next = Fluent.Dialog("New dialog", Fluent.Button("New action"));
        var visible = true;
        var nextVisible = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        int Frame(float delta = 0, UiInputState? input = null)
        {
            context.NewFrame(new(delta, new(800, 600), new(1, 1)), input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                background.Render(ui);
                if (visible)
                    dialog.Render(ui);
                if (nextVisible)
                    next.Render(ui);
            }));
            using var data = context.GetDrawData();
            return data.TextureUpdates.Count(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup);
        }
        UiPaintGroup Snapshot() => dialog.Input.SurfacePaint ?? throw new InvalidOperationException("The open Dialog must own a captured surface.");
        bool HasDialog(string name) => tree.CaptureSnapshot().Elements.Any(x => x.Node.Name == name && x.Node.Role == UiAccessibilityRole.Dialog);
        void Open()
        {
            dialog.Show();
            Frame();
            Frame(.3f);
        }
        static bool Disposed(UiPaintGroup paint)
        {
            try
            {
                paint.ValidateOwner();
                return false;
            }
            catch (ObjectDisposedException) { return true; }
        }
        Frame();
        Open();
        var paint = Snapshot();
        check(dialog.Input.MotionNextAt is null,
            "Settled animated Dialog retains one reusable frozen-paint source without an animation deadline");
        var beforeReads = reads;
        dialog.Close();
        var recaptures = Frame();
        check(!dialog.IsOpen && !HasDialog("Exit dialog") && !Disposed(paint) && recaptures == 0 && reads == beforeReads,
            "Close ends Dialog input/accessibility immediately and replays frozen paint without invoking content");
        Frame(input: idle with
        {
            KeyEvents = [new(UiKey.Enter, true, default)]
        });
        check(calls == 0, "Exiting Dialog cannot invoke a retired action through the keyboard");
        var button = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Background action").Node.Bounds;
        var point = new UiVector2(button.X + button.Width / 2, button.Y + button.Height / 2);
        Frame(input: idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(input: idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(backgroundCalls == 1, "Frozen exit paint does not block background pointer input");
        recaptures += Frame(.125f);
        check(recaptures == 0 && reads == beforeReads && context.State.NextScheduledFrameTimeSeconds is not null,
            "Half-exited Dialog schedules finite replay without recapturing or reading live content");
        Frame(.125f);
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "Exit completion releases its paint owner and stops scheduling frames");

        Open();
        dialog.Close();
        Frame();
        dialog.Show();
        Frame();
        check(dialog.IsOpen && dialog.Input.MotionNextAt is null && context.State.NextScheduledFrameTimeSeconds is null,
            "Reopening a fully visible exit does not schedule a redundant no-change entry transition");

        Open();
        paint = Snapshot();
        dialog.Close();
        Frame();
        Frame(.125f);
        var expected = 600 * (1 - .15f * (float)UiDialogMotion.Exit.Evaluate(.5));
        dialog.Show();
        Frame();
        var actual = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Exit dialog" && x.Node.Role == UiAccessibilityRole.Dialog).Node.Bounds.Width;
        check(dialog.IsOpen && MathF.Abs(actual - expected) < .01f && Disposed(paint),
            "Reopening samples the displayed exit scale continuously and releases the old frozen owner");
        Frame(.3f);
        paint = Snapshot();
        dialog.Close();
        Frame();
        visible = false;
        Frame();
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "Hiding an exiting Dialog immediately cancels its snapshot and deadline");
        visible = true;
        Open();
        paint = Snapshot();
        dialog.Close();
        Frame();
        dialog.Options = dialog.Options with
        {
            MotionEnabled = false
        };
        Frame();
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "Disabling motion during exit cancels the frozen surface and its deadline");
        dialog.Options = dialog.Options with
        {
            MotionEnabled = true
        };
        Open();
        paint = Snapshot();
        dialog.Close();
        Frame();
        nextVisible = true;
        next.Show();
        Frame();
        check(next.IsOpen && next.Input.Tier > dialog.Input.Tier,
            "A newly opened overlay receives a layer above still-exiting paint");
        next.Close();
        nextVisible = false;
        visible = false;
        Frame();
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "Removing overlapping exit owners releases all finite presentation work");
        visible = true;
        dialog.Show();
        Frame();
        Frame(.05f);
        paint = Snapshot();
        dialog.Close();
        Frame();
        check(!dialog.IsOpen && !Disposed(paint), "Closing during entry transfers its partially visible frozen surface into exit ownership");
        Frame(.25f);
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "An interrupted entry still has a finite exit deadline");

        var controlled = true;
        dialog.Open = new UiBinding<bool>(() => controlled, value => { });
        Frame();
        Frame(.3f);
        paint = Snapshot();
        check(!dialog.Close() && dialog.IsOpen && ReferenceEquals(Snapshot(), paint),
            "A rejected controlled Close retains live paint ownership without starting an exit");
        controlled = false;
        Frame();
        check(!dialog.IsOpen && !HasDialog("Exit dialog") && !Disposed(paint),
            "External controlled false retires interaction and transfers the frozen surface");
        Frame(.25f);
        dialog.Open = null;

        Open();
        closeAction.Activated = () => { calls++; dialog.Close(); };
        var action = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Closed action").Node.Bounds;
        point = new(action.X + action.Width / 2, action.Y + action.Height / 2);
        Frame(input: idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        paint = Snapshot();
        Frame(input: idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(!dialog.IsOpen && !HasDialog("Exit dialog") && !Disposed(paint) && calls == 1,
            "An action closing during paint capture retires staged providers in the same frame and preserves the last visible snapshot");
        Frame(.25f);

        Open();
        paint = Snapshot();
        dialog.Close();
        Frame();
        dialog.Dispose();
        visible = false;
        Frame();
        check(Disposed(paint) && context.State.NextScheduledFrameTimeSeconds is null,
            "Disposing an exiting Dialog cancels frozen paint immediately");
        nextVisible = true;
        next.Show();
        Frame();
        Frame(.3f);
        var finalPaint = next.Input.SurfacePaint ?? throw new InvalidOperationException("The second Dialog must own its captured surface.");
        next.Close();
        Frame();
        context.Dispose();
        check(Disposed(finalPaint), "Disposing the host releases remaining frozen exit owners");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
