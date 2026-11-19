using Duxel.Core;

internal static class DialogRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var clicks = 0;
        using var dialog = Fluent.Dialog("GPU dialog", Fluent.Body1("Dialog content"),
            [Fluent.Button("Confirm", () => clicks++)], new(Width: 240, MotionEnabled: false));
        var bounds = default(UiRect);
        var background = default(UiColor);
        UiRasterTextureStatistics shadows = default;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 320, 240), new(30, 70, 90), default, 0, 0);
            dialog.Render(ui);
            if (dialog.IsOpen)
                paint.Frame(bounds, new(255, 0, 0), default, 0, 0);
            background = UiFluentDialogPresentation.Background(paint);
            shadows = ui.ShadowTextureStatistics;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) =>
            tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name && x.Node.Role == role);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        setInput(idle);
        capture("dialog-closed", Draw);
        var corners = new Dictionary<UiDialogType, byte[]>();
        foreach (var type in new[] { UiDialogType.Modal, UiDialogType.NonModal, UiDialogType.Alert })
        {
            dialog.Options = dialog.Options with
            {
                Type = type
            };
            dialog.Show();
            capture("dialog-" + type + "-layout", Draw);
            bounds = Find("GPU dialog", type == UiDialogType.Alert ? UiAccessibilityRole.AlertDialog : UiAccessibilityRole.Dialog).Node.Bounds;
            var pixels = capture("dialog-" + type, Draw);
            var uploads = shadows.Uploads;
            var repeat = capture("dialog-" + type + "-repeat", Draw);
            check(pixels.AsSpan().SequenceEqual(repeat), "GPU Dialog stable paint replays exactly : " + type);
            check(shadows.Uploads == uploads, "GPU Dialog stable shadow does not regenerate or upload : " + type);
            check(bounds.Width == 240 && bounds.X == 40 && bounds.Y >= 0 && bounds.Y + bounds.Height <= 240,
                "GPU Dialog centered natural surface stays inside the viewport : " + type);
            var offset = ((int)(bounds.Y + bounds.Height - 12) * 320 + (int)(bounds.X + bounds.Width - 12)) * 4;
            check(pixels[offset] == (byte)background.Rgba && pixels[offset + 1] == (byte)(background.Rgba >> 8)
                && pixels[offset + 2] == (byte)(background.Rgba >> 16),
                "GPU Dialog fill paints above later red base content : " + type);
            corners[type] = pixels[..4];
            if (type == UiDialogType.NonModal)
            {
                check(Find("Close", UiAccessibilityRole.Button).Node.Bounds.Width == 24,
                    "GPU non-modal title renders the compiled dismiss icon in a24px action");
            }
            var command = Find("Confirm", UiAccessibilityRole.Button).Node.Bounds;
            var point = new UiVector2(command.X + command.Width / 2, command.Y + command.Height / 2);
            setInput(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            capture("dialog-" + type + "-press", Draw);
            setInput(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            capture("dialog-" + type + "-release", Draw);
            check(clicks == (int)type + 1, "GPU Dialog action processes real pointer activation once : " + type);
            setInput(idle);
            dialog.Close();
            capture("dialog-" + type + "-closed", Draw);
        }
        for (var channel = 0; channel < 3; channel++)
        {
            check(Math.Abs(corners[UiDialogType.Modal][channel] - corners[UiDialogType.NonModal][channel] * .6f) <= 2,
                "GPU modal backdrop uses40 percent dimming independently from shadow, channel : " + channel);
            check(Math.Abs(corners[UiDialogType.Alert][channel] - corners[UiDialogType.Modal][channel]) <= 1,
                "GPU alert and modal share the same backdrop palette, channel : " + channel);
        }
        context.SetAccessibilityTree(null);
    }
}
