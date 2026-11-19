using Duxel.Core;

internal static class DialogMotionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, Action<float?> setDelta)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var dialog = Fluent.Dialog("GPU moving dialog", Fluent.Body1("Body"),
            [Fluent.Button("Moving command")], new(Width: 240));
        var background = default(UiColor);
        var contentIdentity = 0L;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 320, 240), new(30, 70, 90), default, 0, 0);
            dialog.Render(ui);
            background = UiFluentDialogPresentation.Background(paint);
            contentIdentity = dialog.Input.SurfacePaint?.For(ui).Content?.Identity ?? 0;
        }
        UiRect Bounds() => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "GPU moving dialog"
            && x.Node.Role == UiAccessibilityRole.Dialog).Node.Bounds;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        setInput(idle);
        setDelta(0);
        try
        {
            var closed = capture("dialog-motion-closed", Draw);
            dialog.Show();
            var start = capture("dialog-motion-start", Draw);
            check(start.AsSpan().SequenceEqual(closed), "GPU Dialog entry starts with a transparent surface and backdrop");
            check(MathF.Abs(Bounds().Width - 204) < .01f, "GPU Dialog entry starts at85 percent of natural width");
            setDelta(.05f);
            var middle = capture("dialog-motion-middle", Draw);
            var bounds = Bounds();
            check(bounds.Width > 204 && bounds.Width < 240, "GPU Dialog entry presents an intermediate scaled surface");
            var motion = UiDialogMotion.SampleEntry(.05f);
            var index = ((int)(bounds.Y + 12) * 320 + (int)(bounds.X + 12)) * 4;
            int[] baseColor = [30, 70, 90];
            for (var channel = 0; channel < 3; channel++)
            {
                var fill = (byte)(background.Rgba >> (channel * 8));
                var expected = fill * motion.Opacity + baseColor[channel] * (1 - .4f * motion.BackdropOpacity) * (1 - motion.Opacity);
                check(Math.Abs(middle[index + channel] - expected) <= 3,
                    "GPU Dialog surface opacity and independent backdrop curve composite correctly, channel : " + channel);
            }
            var snapshot = contentIdentity;
            setDelta(0);
            var repeat = capture("dialog-motion-middle-repeat", Draw);
            check(snapshot != 0 && contentIdentity == snapshot, "GPU Dialog fixed-resolution motion reuses its captured paint content");
            check(middle.AsSpan().SequenceEqual(repeat), "GPU Dialog paused motion replays identical pixels");
            setDelta(.2f);
            var settled = capture("dialog-motion-settled", Draw);
            check(Bounds().Width == 240 && dialog.Input.SurfacePaint is not null && dialog.Input.MotionNextAt is null,
                "GPU Dialog entry settles at natural size and retains its reusable exit snapshot");
            bounds = Bounds();
            index = ((int)(bounds.Y + 12) * 320 + (int)(bounds.X + 12)) * 4;
            check(settled[index] == (byte)background.Rgba && settled[index + 1] == (byte)(background.Rgba >> 8)
                && settled[index + 2] == (byte)(background.Rgba >> 16),
                "GPU settled Dialog reuses an opaque cached surface");
            dialog.Close();
            setDelta(0);
            var exitStart = capture("dialog-motion-exit-start", Draw);
            check(exitStart.AsSpan().SequenceEqual(settled), "GPU Dialog exit begins from the exact retained visible surface");
            setDelta(.125f);
            var exitMiddle = capture("dialog-motion-exit-middle", Draw);
            var surfaceOpacity = 1 - (float)UiDialogMotion.Exit.Evaluate(.5);
            var backdropOpacity = 1 - (float)UiDialogMotion.Backdrop.Evaluate(.5);
            for (var channel = 0; channel < 3; channel++)
            {
                var fill = (byte)(background.Rgba >> (channel * 8));
                var expected = fill * surfaceOpacity + baseColor[channel] * (1 - .4f * backdropOpacity) * (1 - surfaceOpacity);
                check(Math.Abs(exitMiddle[index + channel] - expected) <= 3,
                    "GPU Dialog exit uses independent surface/backdrop curves, channel : " + channel);
            }
            setDelta(0);
            var exitRepeat = capture("dialog-motion-exit-repeat", Draw);
            check(exitMiddle.AsSpan().SequenceEqual(exitRepeat), "GPU frozen exit replays identical pixels without live controls");
            setDelta(.125f);
            var dismissed = capture("dialog-motion-dismissed", Draw);
            check(dismissed.AsSpan().SequenceEqual(closed) && context.State.NextScheduledFrameTimeSeconds is null,
                "GPU exit completion restores the exact base scene and releases its deadline");
            dialog.Show();
            setDelta(0);
            capture("dialog-motion-interrupted-start", Draw);
            setDelta(.05f);
            var interruptedEntry = capture("dialog-motion-interrupted-entry", Draw);
            dialog.Close();
            setDelta(0);
            var interruptedExit = capture("dialog-motion-interrupted-exit", Draw);
            check(interruptedEntry.AsSpan().SequenceEqual(interruptedExit),
                "GPU interrupted entry begins exit with identical scale, opacity and frozen pixels");
            setDelta(.25f);
            var interruptedDone = capture("dialog-motion-interrupted-done", Draw);
            check(interruptedDone.AsSpan().SequenceEqual(closed) && context.State.NextScheduledFrameTimeSeconds is null,
                "GPU interrupted entry completes its finite exit without residual paint");
        }
        finally
        {
            setDelta(null);
            setInput(idle);
            context.SetAccessibilityTree(null);
        }
    }
}
