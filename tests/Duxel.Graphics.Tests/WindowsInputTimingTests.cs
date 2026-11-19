using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunNativeInputTime(WindowsPlatformBackend platform, Action<bool, string> check)
    {
        foreach (var sample in new (uint Posted, ulong Now, ulong Expected)[] { (900, 1000, 900), (0xFFFFFFF0, 0x100000010, 0xFFFFFFF0), (0, 0x100000010, 0x100000000) })
            check(WindowsPlatformBackend.ValidationInputMessageTime(sample.Posted, sample.Now) == sample.Expected / 1000d,
                $"Windows input time : posted={sample.Posted:X}, now={sample.Now:X} / unwrap queued DWORD time on the native uptime clock");
        platform.PollEvents();
        var before = WindowsPlatformBackend.ValidationInputClockSeconds();
        if (!PresentationTestPlatform.PostMessage(platform.WindowHandle, 0x0102, 'Q', 0))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        platform.PollEvents();
        var after = WindowsPlatformBackend.ValidationInputClockSeconds();
        var queued = platform.Input.Snapshot.OrderedInputs!.Where(input => input.CharEvents.Any(text => text.CodePoint == 'Q')).ToArray();
        check(queued is [{ EventTimeSeconds: { } time }] && time >= before && time <= after,
            "Windows input time : an actual posted WM_CHAR retains its queue timestamp through the native IME hook");
        foreach (var gap in new[] { .1, 1.0 })
        {
            platform.ValidationCaptureOrderedPointer(new(-100, -100), false);
            platform.PollEvents();
            platform.ValidationCaptureOrderedPointer(new(20, 12), true, 10);
            platform.ValidationCaptureOrderedPointer(new(20, 12), false, 10.01);
            platform.ValidationCaptureOrderedPointer(new(20, 12), true, 10 + gap);
            var capture = AppInputSnapshots.Copy(platform.Input.Snapshot);
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var counts = new List<int>();
            context.NewInputBatch(new(.016f, new(400, 300), new(1, 1)), AppInputSnapshots.ToLogical(capture, 1)!, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { if (ui.IsMouseClicked(0)) counts.Add(context.State.LastClickCount); }));
            using var data = context.GetDrawData();
            check(counts.SequenceEqual(new[] { 1, gap < .3 ? 2 : 1 }),
                $"Windows input time : gap={gap} / native capture, App copy/scale and Core preserve click timing within one host frame");
        }
        platform.ValidationCaptureOrderedPointer(new(-100, -100), false);
    }
}
