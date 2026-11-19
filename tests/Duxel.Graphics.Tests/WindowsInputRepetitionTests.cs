using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunNativeInputRepetition(WindowsPlatformBackend platform, Action<bool, string> check)
    {
        var ime = (WindowsImeHandler)platform.ImeHandler!;
        foreach (var count in new[] { 0, 1, 3, 65535 })
            foreach (var held in new[] { false, true })
            {
                platform.PollEvents();
                SendMessageW(platform.WindowHandle, 0x0100, 0x41, (nint)(count | (held ? 0x40000000 : 0)));
                SendMessageW(platform.WindowHandle, 0x0101, 0x41, unchecked((nint)0xC0000001u));
                var input = AppInputSnapshots.Copy(platform.Input.Snapshot);
                var downs = input.KeyEvents.Where(key => key.IsDown).ToArray();
                var expected = Math.Max(1, count);
                check(downs.Length == expected && downs.Select((key, i) => key.IsRepeat == (held || i > 0)).All(value => value),
                    $"Native repetition : count={count}, held={held} / preserve multiplicity and previous-key-state metadata");
                using var context = Context();
                var amounts = new List<int>();
                var presses = new List<bool>();
                var repeated = new List<bool>();
                var renders = 0;
                context.NewInputBatch(new(.016f, new(400, 300), new(1, 1)), AppInputSnapshots.ToLogical(input, 1)!, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    renders++;
                    amounts.Add(ui.GetKeyPressedAmount(UiKey.A, .2f, .1f));
                    presses.Add(ui.IsKeyPressed(UiKey.A, false));
                    repeated.Add(ui.IsKeyPressed(UiKey.A, true));
                }));
                using var data = context.GetDrawData();
                check(amounts.SequenceEqual(new[] { expected, 0 }) && presses.SequenceEqual(new[] { !held, false }) && repeated.SequenceEqual(new[] { true, false }),
                    $"Native repetition : count={count}, held={held} / Core queries consume repeat count and honor repeat=false");
                check(renders == 2, $"Native repetition : count={count}, held={held} / coalesced repeats do not multiply UI compositions");
            }
        platform.PollEvents();
        ime.SetCompositionOwner("repeat-large");
        ime.ConsumeRecentCommittedText();
        SendMessageW(platform.WindowHandle, 0x0102, 'Z', 65535);
        var chars = AppInputSnapshots.Copy(platform.Input.Snapshot);
        using (var context = Context())
        {
            context.SetImeHandler(ime);
            context.NewInputBatch(new(.016f, new(400, 300), new(1, 1)), AppInputSnapshots.ToLogical(chars, 1)!, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(_ => { }));
            using var data = context.GetDrawData();
            check(chars.CharEvents.Count == 65535 && ime.ConsumeCommittedText("repeat-large") == new string('Z', 65535),
                "Native repetition : maximum WORD count emits matching raw scalars and one complete repeated commit");
        }
        ime.SetCompositionOwner(null);
        ime.ConsumeRecentCommittedText();

        static UiContext Context()
        {
            var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            return context;
        }
    }
}
