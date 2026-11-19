using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    internal static void Run(Action<bool, string> check)
    {
        var wakes = 0;
        using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions
        {
            Width = 320,
            Height = 240,
            Title = "Duxel native IME routing validation",
            TrayOptions = new(),
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            FrameInvalidated = () => wakes++
        });
        var ime = platform.ImeHandler as WindowsImeHandler ?? throw new InvalidOperationException("The native IME handler is required.");
        void Send(uint message, nuint character = 0, nint flags = 0) => SendMessageW(platform.WindowHandle, message, character, flags);
        const uint Start = 0x010D, End = 0x010E, Composition = 0x010F, ImeChar = 0x0286, Char = 0x0102;
        void StartSession()
        {
            Send(Start);
            ime.ValidationSetCompositionStrings("한", "정");
            Send(Composition, flags: 0x8);
        }
        void Commit(bool resultString)
        {
            if (resultString)
                Send(Composition, flags: 0x800);
            else
                Send(ImeChar, '정');
        }
        check(!platform.ValidationWindowShown, "Native IME validation owns an unshown window without changing user focus");
        foreach (var resultString in new[] { false, true })
        {
            foreach (var worker in new[] { false, true })
                foreach (var next in new string?[] { "B", null })
                {
                    void Verify(bool condition, string name) => check(condition, $"Native IME routing : resultString={resultString}, worker={worker}, next={next ?? "none"} / {name}");
                    ime.ConsumeRecentCommittedText();
                    ime.SetCompositionOwner("A");
                    var initialWakes = wakes;
                    StartSession();
                    Verify(ime.GetCompositionText() == "한", "native composition update reads the original input's preedit");
                    void ChangeOwner(string? value)
                    {
                        if (worker)
                            Task.Run(() => ime.SetCompositionOwner(value)).GetAwaiter().GetResult();
                        else
                            ime.SetCompositionOwner(value);
                    }
                    ChangeOwner(next);
                    Verify(ime.GetCompositionText() is null, "preedit stays hidden from a different active input");
                    ChangeOwner("A");
                    Verify(ime.GetCompositionText() == "한", "returning to the composition input preserves its preedit");
                    ChangeOwner(next);
                    Commit(resultString);
                    Commit(resultString);
                    Send(End);
                    Verify(ime.ConsumeCommittedText("A") == "정", "result is delivered to the original composition input");
                    Verify(ime.ConsumeCommittedText("B") is null, "result cannot leak to the newly focused input");
                    Verify(ime.ConsumeCommittedText("A") is null, "original input consumes the result once");
                    Verify(ime.ConsumeRecentCommittedText() == "정", "legacy recent-result observation remains exactly once");
                    Send(Char, 'X');
                    Verify(ime.ConsumeCommittedText("B") == (next is null ? null : "X"), "ordinary text after composition follows current focus");
                    Verify(ime.GetCompositionText() is null, "native end clears visible composition");
                    Verify(wakes > initialWakes, "native composition messages request a frame");
                }
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner(null);
            StartSession();
            check(ime.GetCompositionText() is null, "An ownerless native composition has no visible input target");
            ime.SetCompositionOwner("B");
            check(ime.GetCompositionText() is null, "An ownerless native composition does not attach its preedit to later focus");
            Commit(resultString);
            Send(End);
            check(ime.ConsumeCommittedText("B") is null, "An ownerless native composition does not write into a later input");
            check(ime.ConsumeRecentCommittedText() is null, "An ownerless native result is not published as accepted input");
            Send(Char, 'X');
            check(ime.ConsumeCommittedText("B") == "X", "Current input accepts ordinary characters after an ownerless session ends");
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner("A");
            StartSession();
            ime.SetCompositionOwner("B");
            Send(End);
            check(ime.ConsumeCommittedText("A") is null && ime.ConsumeCommittedText("B") is null, "Ending without a result never invents committed text");
            check(ime.GetCompositionText() is null, "Ending without a result retires preedit");
            Send(Char, 'X');
            check(ime.ConsumeCommittedText("B") == "X", "Current input remains usable after canceled composition");
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner("A");
            Send(Char, 'Z');
            check(ime.ConsumeCommittedText("A") == "Z", "Ordinary native WM_CHAR follows current input");
            ime.SetCompositionOwner("B");
            Send(ImeChar, 'R');
            check(ime.ConsumeCommittedText("B") == "R", "Native WM_IME_CHAR outside composition follows current input");
            check(ime.ConsumeRecentCommittedText() == "ZR", "Recent ordinary input preserves native message order");
        }
        RunCompositionFlags(platform, ime, check);
        RunScalarMessages(platform, ime, check);
        RunScalarConsumers(platform, ime, check);
        RunConsumerFlow(platform, ime, check);
        RunFocusConsumers(platform, ime, check);
        RunInputRetirement(platform, ime, check);
        RunFocusHandoff(platform, ime, check);
        check(!platform.ValidationWindowShown, "Native IME validation leaves its window unshown");
    }
    private static void RunScalarMessages(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        const uint Character = 0x0102, ImeCharacter = 0x0286, Start = 0x010D, End = 0x010E;
        void Send(uint message, nuint value = 0) => SendMessageW(platform.WindowHandle, message, value, 0);
        void Reset()
        {
            Send(End);
            ime.ConsumeCommittedText("A");
            ime.ConsumeCommittedText("B");
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner("A");
            platform.PollEvents();
        }
        foreach (var message in new[] { Character, ImeCharacter })
            foreach (var composing in new[] { false, true })
                foreach (var nextOwner in new string?[] { "A", "B", null })
                {
                    Reset();
                    if (composing)
                        Send(Start);
                    void Verify(bool condition, string name) => check(condition,
                        $"Native scalar : message={message}, composing={composing}, next={nextOwner ?? "none"} / {name}");
                    Send(message, 0xD83D);
                    Verify(ime.ConsumeCommittedText("A") is null && ime.ConsumeRecentCommittedText() is null,
                        "a high surrogate is never exposed to a render consumer");
                    if (message == Character)
                        Verify(platform.Input.Snapshot.CharEvents.Count == 0, "the raw input lane also withholds a high surrogate");
                    platform.PollEvents();
                    Task.Run(() => ime.SetCompositionOwner(nextOwner)).GetAwaiter().GetResult();
                    Send(message, 0xDE00);
                    Verify(ime.ConsumeCommittedText("A") == "😀" && ime.ConsumeCommittedText("B") is null,
                        "the complete scalar reaches only the input captured by its first code unit");
                    Verify(ime.ConsumeRecentCommittedText() == "😀", "recent input exposes the complete scalar exactly once");
                    if (message == Character)
                        Verify(platform.Input.Snapshot.CharEvents.SequenceEqual(new[] { new UiCharEvent(0x1F600) }),
                            "the raw input lane preserves a pair across frame consumption and emits one scalar");
                }
        foreach (var message in new[] { Character, ImeCharacter })
        {
            Reset();
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "An orphan low surrogate is rejected : " + message);
            Send(message, 0xD83D);
            Send(message, 'X');
            check(ime.ConsumeCommittedText("A") == "X", "A BMP character supersedes an incomplete pair without replacement text : " + message);
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "The superseded pair cannot resume : " + message);
            Reset();
            ime.SetCompositionOwner(null);
            Send(message, 0xD83D);
            ime.SetCompositionOwner("B");
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("B") is null && ime.ConsumeRecentCommittedText() is null,
                "An ownerless first code unit never attaches to later focus : " + message);
            Reset();
            Send(message, 0xD83D);
            Send(Start);
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "A pair cannot cross native composition start : " + message);
            if (message == Character)
                check(platform.Input.Snapshot.CharEvents.Count == 0, "The raw lane shares native composition-start retirement");
            Send(message, 0xD83D);
            Send(End);
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "A pair cannot cross native composition end : " + message);
            if (message == Character)
                check(platform.Input.Snapshot.CharEvents.Count == 0, "The raw lane shares native composition-end retirement");
            Reset();
            Send(message, 0xD83D);
            Send(0x0008);
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "A pair cannot cross native window focus loss : " + message);
            Reset();
            Send(Start);
            Send(message, 0xD83D);
            Send(0x010F);
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("A") is null, "A pair cannot cross native composition cancellation : " + message);
            if (message == Character)
                check(platform.Input.Snapshot.CharEvents.Count == 0, "The raw lane shares native cancellation retirement even when the IME hook consumes the message");
        }
        Reset();
        Send(Character, 0xD83D);
        Send(ImeCharacter, 0xDE00);
        check(ime.ConsumeCommittedText("A") is null, "Different native message lanes cannot complete each other's surrogate pair");
        Send(Character, 0xDE00);
        check(ime.ConsumeCommittedText("A") == "😀", "An unrelated message lane does not discard the original pending pair");
        Reset();
    }

    private static void RunScalarConsumers(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        foreach (var message in new uint[] { 0x0102, 0x0286 })
            foreach (var reverse in new[] { false, true })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.ConfigureEmoji(context);
                context.SetImeHandler(ime);
                var first = "a";
                var second = "b";
                using var inputA = new UiInput<UiStandardTextInputBehavior, Capture<A>>("Scalar A", new(() => first, text => first = text), 256, 200);
                using var inputB = new UiInput<UiStandardTextInputBehavior, Capture<B>>("Scalar B", new(() => second, text => second = text), 256, 200);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame(UiInputState? state = null)
                {
                    context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), state ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
                    context.Render(new ConsumerScreen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        void First()
                        {
                            ui.SetCursorScreenPos(new(0, 0));
                            inputA.Render(ui);
                        }
                        void Second()
                        {
                            ui.SetCursorScreenPos(new(0, 40));
                            inputB.Render(ui);
                        }
                        if (reverse)
                        {
                            Second();
                            First();
                        }
                        else
                        {
                            First();
                            Second();
                        }
                    }));
                    using var data = context.GetDrawData();
                }
                void Verify(bool condition, string name) => check(condition, $"Native scalar consumers : message={message}, reverse={reverse} / {name}");
                Frame();
                var idA = Capture<A>.Last.Id;
                context.State.ActiveId = context.State.FocusedId = idA;
                context.State.SetCursor(idA, 1);
                context.State.SetSelection(idA, new(1, 1));
                Frame();
                SendMessageW(platform.WindowHandle, message, 0xD83D, 0);
                Frame();
                Verify(first == "a" && second == "b", "rendering between code units never publishes an incomplete source");
                Frame(idle with
                {
                    MousePosition = new(10, 50),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                });
                Verify(context.State.ActiveId == Capture<B>.Last.Id && first == "a" && second == "b",
                    "pointer focus can move while the original scalar is incomplete");
                SendMessageW(platform.WindowHandle, message, 0xDE00, 0);
                Frame(idle with
                {
                    LeftMouseReleased = true
                });
                Verify(first == "a😀" && second == "b" && context.State.GetCursor(idA, 3) == 3,
                    "the completed scalar reaches the inactive original binding with a valid final cursor");
                context.State.ActiveId = context.State.FocusedId = idA;
                Frame();
                Verify(Capture<A>.Last.CaretIndex == 3, "returning focus restores the caret after the complete supplementary character");
                inputA.Dispose();
                inputB.Dispose();
                Verify(context.State.TextInputStorage == (0, 0), "scalar consumers release both edit buffers");
                ime.SetCompositionOwner(null);
                ime.ConsumeRecentCommittedText();
                Capture<A>.Last = Capture<B>.Last = default;
            }
    }

    private static void RunCompositionFlags(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        foreach (var changeFocus in new[] { false, true })
        {
            void Send(nint flags) => SendMessageW(platform.WindowHandle, 0x010F, 0, flags);
            void Verify(bool condition, string name) => check(condition, $"Native IME flags : focusChanged={changeFocus} / {name}");
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner("A");
            SendMessageW(platform.WindowHandle, 0x010D, 0, 0);
            ime.ValidationSetCompositionStrings("한", "정");
            Send(0x8);
            Verify(ime.GetCompositionText() == "한", "COMPSTR publishes the indicated preedit");
            ime.ValidationSetCompositionStrings("not indicated", "not indicated");
            Send(0x10);
            Verify(ime.GetCompositionText() == "한", "attribute-only update preserves the existing string");
            Send(0);
            Verify(string.IsNullOrEmpty(ime.GetCompositionText()), "no GCS flags cancel the visible composition");
            ime.ValidationSetCompositionStrings("한", "정");
            Send(0x8);
            if (changeFocus)
                ime.SetCompositionOwner("B");
            ime.ValidationSetCompositionStrings("다", "정");
            Send(0x808);
            Verify(ime.ConsumeCommittedText("A") == "정", "combined message commits to the original input");
            Verify(ime.ConsumeCommittedText("B") is null, "combined message never commits to another input");
            Verify(ime.GetCompositionText() == (changeFocus ? null : "다"), "combined message retains new preedit only for its owner");
            ime.SetCompositionOwner("A");
            Verify(ime.GetCompositionText() == "다", "returning focus exposes the new preedit");
            ime.ValidationSetCompositionStrings("라", "정");
            Send(0x808);
            Verify(ime.ConsumeCommittedText("A") == "정", "the next segment can commit identical text within the same native session");
            Verify(ime.GetCompositionText() == "라", "the next combined update replaces preedit");
            ime.ValidationSetCompositionStrings("unflagged stale preedit", "끝");
            Send(0x800);
            Verify(ime.ConsumeCommittedText("A") == "끝" && string.IsNullOrEmpty(ime.GetCompositionText()), "result-only update clears prior preedit without reading stale COMPSTR");
            Send(0x800);
            Verify(ime.ConsumeCommittedText("A") is null, "duplicate result-only notification does not repeat a segment");
            Verify(ime.ConsumeRecentCommittedText() == "정정끝", "accepted segments preserve native message order exactly");
            SendMessageW(platform.WindowHandle, 0x010E, 0, 0);
        }
    }

    private static void RunConsumerFlow(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        foreach (var resultString in new[] { false, true })
            foreach (var reverse in new[] { false, true })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                context.SetImeHandler(ime);
                var first = "a";
                var second = "b";
                using var inputA = new UiInput<UiStandardTextInputBehavior, Capture<A>>("Native A", new(() => first, text => first = text), 256, 200);
                using var inputB = new UiInput<UiStandardTextInputBehavior, Capture<B>>("Native B", new(() => second, text => second = text), 256, 200);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame(UiInputState? state = null)
                {
                    context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), state ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
                    context.Render(new ConsumerScreen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        void First()
                        {
                            ui.SetCursorScreenPos(new(0, 0));
                            inputA.Render(ui);
                        }
                        void Second()
                        {
                            ui.SetCursorScreenPos(new(0, 40));
                            inputB.Render(ui);
                        }
                        if (reverse)
                        {
                            Second();
                            First();
                        }
                        else
                        {
                            First();
                            Second();
                        }
                    }));
                    using var data = context.GetDrawData();
                }
                void Verify(bool condition, string name) => check(condition, $"Native IME actual controls : resultString={resultString}, reverse={reverse} / {name}");
                Frame();
                var idA = Capture<A>.Last.Id;
                context.State.ActiveId = context.State.FocusedId = idA;
                context.State.SetCursor(idA, 1);
                context.State.SetSelection(idA, new(1, 1));
                Frame();
                SendMessageW(platform.WindowHandle, 0x010D, 0, 0);
                ime.ValidationSetCompositionStrings("한", "정");
                SendMessageW(platform.WindowHandle, 0x010F, 0, 0x8);
                Frame();
                Verify(Capture<A>.Last.CompositionText == "한" && Capture<B>.Last.CompositionText is null,
                    "preedit belongs to the original live input");
                Frame(idle with
                {
                    MousePosition = new(10, 50),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                });
                Verify(context.State.ActiveId == Capture<B>.Last.Id && string.IsNullOrEmpty(Capture<B>.Last.CompositionText)
                    && first == "a" && second == "b", "clicking the other input never displays or writes the old preedit");
                if (resultString)
                    SendMessageW(platform.WindowHandle, 0x010F, 0, 0x800);
                else
                    SendMessageW(platform.WindowHandle, 0x0286, '정', 0);
                SendMessageW(platform.WindowHandle, 0x010E, 0, 0);
                Frame(idle with
                {
                    LeftMouseReleased = true
                });
                Verify(first == "a정" && second == "b" && context.State.ActiveId == Capture<B>.Last.Id,
                    "native result updates only the original binding while new focus remains active");
                Verify(context.State.GetCursor(idA, first.Length) == 2,
                    "late native commit persists the inactive input's final cursor");
                context.State.ActiveId = context.State.FocusedId = idA;
                Frame();
                Verify(Capture<A>.Last.CaretIndex == 2,
                    "returning focus restores the cursor after the late committed text");
                if (resultString)
                {
                    SendMessageW(platform.WindowHandle, 0x010D, 0, 0);
                    ime.ValidationSetCompositionStrings("다", "같");
                    SendMessageW(platform.WindowHandle, 0x010F, 0, 0x808);
                    Frame();
                    Verify(first == "a정같" && Capture<A>.Last.CompositionText == "다" && Capture<A>.Last.CaretIndex == 3,
                        "one native message commits source and displays the next preedit at its new insertion edge");
                    ime.ValidationSetCompositionStrings("라", "같");
                    SendMessageW(platform.WindowHandle, 0x010F, 0, 0x808);
                    Frame();
                    Verify(first == "a정같같" && Capture<A>.Last.CompositionText == "라" && Capture<A>.Last.CaretIndex == 4,
                        "a repeated-text segment updates the actual binding and advances the next preedit");
                    ime.ValidationSetCompositionStrings("stale", "끝");
                    SendMessageW(platform.WindowHandle, 0x010F, 0, 0x800);
                    Frame();
                    Verify(first == "a정같같끝" && string.IsNullOrEmpty(Capture<A>.Last.CompositionText),
                        "result-only message completes the live editor without stale preedit");
                    SendMessageW(platform.WindowHandle, 0x010E, 0, 0);
                    Frame();
                    Verify(first == "a정같같끝" && Capture<A>.Last.CaretIndex == 5 && second == "b",
                        "native end preserves the combined-message source and final caret without replay");
                }
                inputA.Dispose();
                inputB.Dispose();
                Verify(context.State.TextInputStorage == (0, 0), "live control disposal releases both edit buffers");
                Capture<A>.Last = Capture<B>.Last = default;
                ime.SetCompositionOwner(null);
                ime.ConsumeRecentCommittedText();
            }
    }
    private readonly struct A
    {
    }
    private readonly struct B
    {
    }
    private sealed class ConsumerScreen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private readonly struct Capture<T> : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiInputLayout Measure(UiControlContext context, string label, float width) => new(new(width, 32), new(0, 0, width, 32), new(0, 0, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => Last = edit;
    }
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint window, uint message, nuint wParam, nint lParam);
}
