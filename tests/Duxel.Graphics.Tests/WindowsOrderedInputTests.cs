using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    internal static void SendOrderedNativeCharacter(nint window, uint character) => SendMessageW(window, 0x0102, character, 0);
    internal static void RunOrderedInput(Action<bool, string> check)
    {
        using var writer = new OrderedUiWriter();
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 400,
            Height = 300,
            Title = "Duxel owned native ordered input",
            EnableOrderedInput = true,
            TrayOptions = new(),
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider()
        });
        var ime = (WindowsImeHandler)platform.ImeHandler!;
        ime.ValidationCompositionNotification = _ => true;
        void Send(uint message, uint character = 0, nint flags = 0) => SendMessageW(platform.WindowHandle, message, character, flags);
        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
            foreach (var reverse in new[] { false, true })
                foreach (var scenario in new[] { "char-first", "click-first", "surrogate-frame", "surrogate-ownerless", "surrogate-reset", "start-first", "click-start", "combined", "completion", "dispose-composition", "dispose-surrogate", "dispose-pending", "dispose-cancel", "hidden-composition", "hidden-surrogate", "repeat-bmp", "repeat-held-bmp", "repeat-surrogate", "repeat-surrogate-first-count", "repeat-ownerless", "repeat-reset", "repeat-key", "repeat-duplicate", "repeat-duplicate-bulk" })
                {
                    using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                    using var tree = new UiAccessibilityTree();
                    if (scenario.Contains("surrogate", StringComparison.Ordinal))
                        ControlFontFixture.ConfigureEmoji(context);
                    else
                        ControlFontFixture.Configure(context);
                    context.SetImeHandler(ime);
                    context.SetAccessibilityTree(tree);
                    var first = scenario == "repeat-key" ? "abcd" : "a";
                    var second = "b";
                    var binding = new UiBinding<string>(() => first, value => first = value);
                    using UiWidget a = kind switch
                    {
                        "Input" => Fluent.Input("Ordered A", binding, 256, 240),
                        "Textarea" => Fluent.Textarea("Ordered A", binding, 256, 240, 90),
                        "Markdown" => new UiMarkdownEditor("Ordered A", binding, new() { Width = 240, Height = 90 }),
                        _ => throw new InvalidOperationException("Unknown native ordered editor.")
                    };
                    using var b = Fluent.Input("Ordered B", new UiBinding<string>(() => second, value => second = value), 256, 240);
                    var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                    var showFirst = true;
                    var screen = new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        void First()
                        {
                            ui.SetCursorScreenPos(default);
                            if (showFirst)
                                a.Render(ui);
                        }
                        void Second()
                        {
                            ui.SetCursorScreenPos(new(0, 150));
                            b.Render(ui);
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
                    });
                    string FindA()
                    {
                        string? found = null;
                        void Visit(string? id)
                        {
                            while (id is not null && tree.TryGet(id, out var element))
                            {
                                if (element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "Ordered A")
                                    found = element.Node.Id;
                                Visit(element.FirstChild);
                                id = element.Next;
                            }
                        }
                        Visit(tree.Root.First);
                        return found ?? throw new InvalidOperationException("Missing ordered editor.");
                    }
                    void Render(IReadOnlyList<UiInputState>? states = null)
                    {
                        writer.Invoke(() =>
                        {
                            var frame = new UiFrameInfo(1f / 60, new(400, 300), new(1, 1));
                            if (states is { Count: > 0 })
                                context.NewInputBatch(frame, states, new(0, 0, 400, 300), UiTextSettings.Default);
                            else
                                context.NewFrame(frame, idle, new(0, 0, 400, 300), UiTextSettings.Default);
                            context.Render(screen);
                            using var data = context.GetDrawData();
                        });
                    }
                    void Flush()
                    {
                        var captured = AppInputSnapshots.Copy(platform.Input.Snapshot);
                        Render(AppInputSnapshots.ToLogical(captured, 1));
                    }
                    void ClickB() => platform.ValidationCaptureOrderedPointer(new(200 * platform.ContentScale, 160 * platform.ContentScale), true);
                    void BeginComposition()
                    {
                        Send(0x010D);
                        ime.ValidationSetCompositionStrings("한", "정");
                        Send(0x010F, flags: 0x8);
                    }
                    platform.ValidationCaptureOrderedPointer(new(-100, -100), false);
                    platform.PollEvents();
                    ime.SetCompositionOwner(null);
                    ime.ConsumeRecentCommittedText();
                    Send(0x010D);
                    Send(0x010E);
                    Flush();
                    var idA = FindA();
                    if (scenario is not ("surrogate-ownerless" or "repeat-ownerless"))
                    {
                        context.State.ActiveId = context.State.FocusedId = idA;
                        context.State.SetCursor(idA, first.Length);
                        context.State.SetSelection(idA, new(first.Length, first.Length));
                        Render();
                    }
                    platform.PollEvents();
                    ime.ValidationCompletions.Clear();
                    ime.ValidationCompositionNotification = _ => true;
                    var expectedA = first;
                    var expectedB = "b";
                    var expectedCompletions = 0;
                    switch (scenario)
                    {
                        case "repeat-bmp":
                        case "repeat-held-bmp":
                            Send(0x0102, 'X', scenario == "repeat-bmp" ? 3 : 0x40000004);
                            expectedA = scenario == "repeat-bmp" ? "aXXX" : "aXXXX";
                            break;
                        case "repeat-surrogate":
                        case "repeat-surrogate-first-count":
                        case "repeat-ownerless":
                        case "repeat-reset":
                            Send(0x0102, 0xD83D, 3);
                            Flush();
                            platform.PollEvents();
                            if (scenario == "repeat-reset")
                            {
                                Send(0x010D);
                                Send(0x010E);
                            }
                            ClickB();
                            Send(0x0102, 0xDE00, scenario == "repeat-surrogate-first-count" ? 1 : 3);
                            if (scenario is "repeat-surrogate" or "repeat-surrogate-first-count")
                                expectedA = "a😀😀😀";
                            break;
                        case "repeat-key":
                            Send(0x0100, 0x25, 3);
                            Send(0x0101, 0x25, unchecked((nint)0xC0000001u));
                            break;
                        case "repeat-duplicate":
                        case "repeat-duplicate-bulk":
                            BeginComposition();
                            if (scenario == "repeat-duplicate-bulk")
                                ime.ValidationSetCompositionStrings("한", "정정정");
                            Send(0x010F, flags: 0x800);
                            Send(0x0102, '정', 3);
                            Send(0x010E);
                            expectedA = scenario == "repeat-duplicate-bulk" ? "a정정정" : "a정";
                            break;
                        case "char-first":
                            Send(0x0102, 'X');
                            ClickB();
                            expectedA = "aX";
                            break;
                        case "click-first":
                            ClickB();
                            Send(0x0102, 'X');
                            expectedB = "bX";
                            break;
                        case "surrogate-frame":
                            Send(0x0102, 0xD83D);
                            Flush();
                            platform.PollEvents();
                            ClickB();
                            Send(0x0102, 0xDE00);
                            expectedA = "a😀";
                            break;
                        case "surrogate-ownerless":
                            Send(0x0102, 0xD83D);
                            ClickB();
                            Send(0x0102, 0xDE00);
                            break;
                        case "surrogate-reset":
                            Send(0x0102, 0xD83D);
                            Send(0x010D);
                            Send(0x010E);
                            Send(0x0102, 0xDE00);
                            break;
                        case "start-first":
                            BeginComposition();
                            ClickB();
                            Send(0x010F, flags: 0x800);
                            Send(0x010E);
                            expectedA = "a정";
                            break;
                        case "click-start":
                            ClickB();
                            BeginComposition();
                            Send(0x010F, flags: 0x800);
                            ime.ValidationSetCompositionStrings("wrong", "wrong");
                            Send(0x010E);
                            expectedB = "b정";
                            break;
                        case "combined":
                            ClickB();
                            BeginComposition();
                            Send(0x010F, flags: 0x808);
                            ime.ValidationSetCompositionStrings("wrong", "wrong");
                            Send(0x010E);
                            expectedB = "b정";
                            break;
                        case "completion":
                            ime.ValidationCompositionNotification = _ => { Send(0x010F, flags: 0x800); Send(0x010E); return true; };
                            BeginComposition();
                            ClickB();
                            Flush();
                            platform.PollEvents();
                            expectedA = "a정";
                            expectedCompletions = 1;
                            break;
                        case "dispose-composition":
                        case "dispose-cancel":
                        case "hidden-composition":
                        case "dispose-pending":
                            BeginComposition();
                            Flush();
                            platform.PollEvents();
                            showFirst = false;
                            if (scenario is "dispose-composition" or "dispose-cancel")
                                writer.Invoke(a.Dispose);
                            else
                                Render();
                            if (scenario == "dispose-cancel")
                            {
                                ime.ValidationCompositionNotification = _ => { Send(0x010F, flags: 0x800); Send(0x010E); return true; };
                                platform.PollEvents();
                                expectedCompletions = 1;
                            }
                            else
                            {
                                Send(0x010F, flags: 0x800);
                                Send(0x010E);
                            }
                            if (scenario == "dispose-pending")
                            {
                                Flush();
                                platform.PollEvents();
                                writer.Invoke(a.Dispose);
                            }
                            if (scenario == "hidden-composition")
                                expectedA = "a정";
                            break;
                        case "dispose-surrogate":
                        case "hidden-surrogate":
                            Send(0x0102, 0xD83D);
                            Flush();
                            platform.PollEvents();
                            showFirst = false;
                            if (scenario == "dispose-surrogate")
                                writer.Invoke(a.Dispose);
                            else
                                Render();
                            ClickB();
                            Send(0x0102, 0xDE00);
                            if (scenario == "hidden-surrogate")
                                expectedA = "a😀";
                            break;
                    }
                    Flush();
                    if (scenario.StartsWith("hidden", StringComparison.Ordinal))
                    {
                        showFirst = true;
                        Render();
                    }
                    var prefix = $"Native ordered : {kind}, reverse={reverse}, scenario={scenario}";
                    check(first == expectedA && second == expectedB, prefix + $" / expected={expectedA}|{expectedB}; actual={first}|{second}");
                    if (scenario == "repeat-key")
                        check(context.State.GetCursor(idA, 99) == 1, prefix + " / all coalesced arrow presses move the real editor caret");
                    platform.PollEvents();
                    check(ime.ValidationCompletions.Count == expectedCompletions,
                        prefix + " / active sessions complete once and native-ended sessions never receive a stale request");
                    if (scenario.StartsWith("dispose", StringComparison.Ordinal) || scenario.StartsWith("hidden", StringComparison.Ordinal))
                        check(ime.ConsumeCommittedText(idA) is null && (scenario != "dispose-cancel" || ime.ValidationCompletions is [{ Action: 4 }]),
                            prefix + " / removed inputs retain no pending commit, live hidden inputs consume theirs, and removal cancels the native session");
                    ime.SetCompositionOwner(null);
                }
        RunNativeInputTime(platform, check);
        RunNativeInputRepetition(platform, check);
        RunNativeFocusKeyLifetime(platform, check);
        platform.PollEvents();
        ime.SetCompositionOwner(null);
        ime.ConsumeRecentCommittedText();
        Send(0x0102, 'X');
        var wrongOwnerInput = AppInputSnapshots.Copy(platform.Input.Snapshot).OrderedInputs!;
        using (var wrongOwner = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2)))
        {
            wrongOwner.NewInputBatch(new(1f / 60, new(400, 300), new(1, 1)), wrongOwnerInput, new(0, 0, 400, 300), UiTextSettings.Default);
            var rejected = false;
            try
            {
                wrongOwner.Render(new Screen(_ => { }));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("owning IME handler", StringComparison.Ordinal)) { rejected = true; }
            check(rejected && ime.ConsumeRecentCommittedText() is null,
                "captured native text rejects a foreign or missing IME consumer before changing committed state");
        }
        check(!platform.ValidationWindowShown, "ordered native capture uses an unshown owned HWND without global pointer or keyboard input");
        platform.ValidationCaptureOrderedPointer(new(-100, -100), false);
    }

    private sealed class OrderedUiWriter : IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(Action Work, TaskCompletionSource<bool> Result)> _work = new();
        private readonly Thread _thread;
        internal OrderedUiWriter()
        {
            _thread = new Thread(() =>
            {
                foreach (var item in _work.GetConsumingEnumerable())
                {
                    try
                    {
                        item.Work();
                        item.Result.SetResult(true);
                    }
                    catch (Exception ex) { item.Result.SetException(ex); }
                }
            })
            {
                IsBackground = true,
                Name = "Duxel owned ordered UI writer"
            };
            _thread.Start();
        }
        internal void Invoke(Action work)
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add((work, result));
            result.Task.GetAwaiter().GetResult();
        }
        public void Dispose()
        {
            _work.CompleteAdding();
            _thread.Join();
            _work.Dispose();
        }
    }
}
