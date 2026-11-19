using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunInputRetirement(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        void Send(uint message, nuint character = 0, nint flags = 0) => SendMessageW(platform.WindowHandle, message, character, flags);
        foreach (var message in new uint[] { 0x0102, 0x0286 })
        {
            Send(0x010E);
            ime.SetCompositionOwner("retired");
            Send(message, 0xD83D);
            ime.ReleaseInput("retired");
            ime.ReleaseInput("retired");
            ime.SetCompositionOwner("replacement");
            Send(message, 0xDE00);
            check(ime.ConsumeCommittedText("retired") is null && ime.ConsumeCommittedText("replacement") is null,
                $"IME retirement : lane={message:X} / a retired first UTF16 unit never attaches to replacement focus");
            ime.SetCompositionOwner("retired");
            Send(message, 'X');
            check(ime.ConsumeCommittedText("retired") == "X",
                $"IME retirement : lane={message:X} / the same input id is reusable without a tombstone");
            ime.ConsumeRecentCommittedText();
            Send(message, 'A');
            ime.SetCompositionOwner("other");
            Send(message, 'B');
            ime.ReleaseInput("retired");
            check(ime.ConsumeCommittedText("retired") is null && ime.ConsumeCommittedText("other") == "B"
                && ime.ConsumeRecentCommittedText() == "AB",
                $"IME retirement : lane={message:X} / release discards only the removed input's queue and preserves recent metadata");
        }
        foreach (var changeFocus in new[] { false, true })
        {
            platform.PollEvents();
            ime.ValidationCompletions.Clear();
            ime.SetCompositionOwner("retired");
            Send(0x010D);
            ime.ValidationSetCompositionStrings("한", "정");
            Send(0x010F, flags: 0x8);
            if (changeFocus)
                ime.SetCompositionOwner("replacement");
            ime.ValidationCompositionNotification = _ =>
            {
                Send(0x010F, flags: 0x800);
                Send(0x010E);
                return true;
            };
            ime.ReleaseInput("retired");
            platform.PollEvents();
            check(ime.ValidationCompletions is [{ Action: 4, Owner: null, LockHeld: false }]
                && ime.ConsumeCommittedText("retired") is null && ime.ConsumeCommittedText("replacement") is null,
                $"IME retirement : changedFocus={changeFocus} / removal cancels on the native owner thread and drops late results");
            ime.SetCompositionOwner("retired");
            Send(0x0102, 'R');
            check(ime.ConsumeCommittedText("retired") == "R",
                $"IME retirement : changedFocus={changeFocus} / canceled ownership does not poison a later input with the same id");
            ime.ConsumeRecentCommittedText();
        }
        platform.PollEvents();
        ime.SetCompositionOwner("repeat-a");
        Send(0x0102, 'Z', 3);
        check(ime.ConsumeCommittedText("repeat-a") == "ZZZ" && platform.Input.Snapshot.CharEvents.Count == 3,
            "Legacy native repetition : BMP multiplicity matches in raw input and the immediate IME handler");
        platform.PollEvents();
        Send(0x0102, 0xD83D, 3);
        ime.SetCompositionOwner("repeat-b");
        Send(0x0102, 0xDE00, 1);
        check(ime.ConsumeCommittedText("repeat-a") == "😀😀😀" && ime.ConsumeCommittedText("repeat-b") is null && platform.Input.Snapshot.CharEvents.Count == 3,
            "Legacy native repetition : the first UTF16 unit retains both owner and multiplicity");
        platform.PollEvents();
        ime.SetCompositionOwner("repeat-a");
        Send(0x010D);
        ime.ValidationSetCompositionStrings("한", "정정정");
        Send(0x010F, flags: 0x8);
        Send(0x010F, flags: 0x800);
        Send(0x0102, '정', 3);
        Send(0x010E);
        check(ime.ConsumeCommittedText("repeat-a") == "정정정",
            "Legacy native repetition : an expanded character result does not duplicate an identical IMM result string");
        ime.SetCompositionOwner(null);
        ime.ConsumeRecentCommittedText();
        ime.ValidationCompositionNotification = _ => true;
    }
}
