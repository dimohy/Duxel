using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunNativeFocusKeyLifetime(WindowsPlatformBackend platform, Action<bool, string> check)
    {
        void Send(uint message, uint key = 0, nint flags = 0) => SendMessageW(platform.WindowHandle, message, key, flags);
        // Retire keys left by earlier scenarios before starting this independent lifetime fixture.
        Send(0x0008);
        platform.PollEvents();
        foreach (var source in new[] { UiKeyRepeatSource.Engine, UiKeyRepeatSource.Platform })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            context.SetImeHandler(platform.ImeHandler!);
            var held = new List<bool>();
            var settings = new UiKeyRepeatSettings(.2, .1) { Source = source };
            var prefix = $"Native focus keys : {source}";
            void Flush()
            {
                var input = AppInputSnapshots.Copy(platform.Input.Snapshot);
                var steps = AppInputSnapshots.ToLogical(input, 1)!;
                var frame = new UiFrameInfo(.016f, new(400, 300), new(1, 1));
                if (steps.Count > 0)
                    context.NewInputBatch(frame, steps.Select(step => step with { KeyRepeatSettings = settings }).ToArray(), new(0, 0, 400, 300), UiTextSettings.Default);
                else
                    context.NewFrame(frame, new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], settings, default), new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    held.Add(context.State.IsKeyDown(UiKey.A));
                    _ = ui.IsKeyPressed(UiKey.LeftArrow, true);
                }));
                using var data = context.GetDrawData();
            }
            platform.PollEvents();
            Send(0x0100, 0x25, 3);
            Send(0x0100, 0x41, 1);
            Flush();
            check(context.State.IsKeyDown(UiKey.A) && context.State.IsKeyDown(UiKey.LeftArrow), prefix + " / native keys enter Core held state");
            check(context.State.HasRepeatTime(UiKey.LeftArrow) == (source == UiKeyRepeatSource.Engine), prefix + " / only engine source arms a deadline");
            platform.PollEvents();
            Send(0x0008);
            var loss = AppInputSnapshots.Copy(platform.Input.Snapshot);
            check(loss.KeyEvents.Count == 2 && loss.KeyEvents.All(key => !key.IsDown && !key.IsRepeat), prefix + " / focus loss releases each unique held key after the poll boundary");
            check(loss.OrderedInputs!.Count(step => step.KeyEvents.Count == 2) == 1, prefix + " / releases occupy one ordered logical step alongside IME dispatch");
            Flush();
            check(!context.State.IsKeyDown(UiKey.A) && !context.State.IsKeyDown(UiKey.LeftArrow) && !context.State.HasRepeatTime(UiKey.LeftArrow), prefix + " / Core held state and deadline retire together");
            platform.PollEvents();
            Send(0x0008);
            check(platform.Input.Snapshot.KeyEvents.Count == 0 && platform.Input.Snapshot.OrderedInputs!.All(step => step.KeyEvents.Count == 0), prefix + " / repeated focus loss emits no phantom releases");
            platform.PollEvents();
            Send(0x0100, 0x41, 3);
            Send(0x0008);
            Send(0x0100, 0x41, 1);
            held.Clear();
            Flush();
            check(held.Where((value, index) => index == 0 || value != held[index - 1]).SequenceEqual(new[] { true, false, true }), prefix + " / down-loss-down keeps exact Core state transitions within one packet");
            var events = platform.Input.Snapshot.KeyEvents;
            check(events.Count == 5 && events.Count(key => !key.IsDown) == 1, prefix + " / repeated downs retain multiplicity but need one release");
            platform.PollEvents();
            Send(0x0101, 0x41, unchecked((nint)0xC0000001u));
            Send(0x0008);
            check(platform.Input.Snapshot.KeyEvents is [{ IsDown: false }], prefix + " / an ordinary keyup removes native ownership before loss");
            Flush();
            check(!context.State.IsKeyDown(UiKey.A), prefix + " / subsequent keyup leaves Core released");
        }
    }
}
