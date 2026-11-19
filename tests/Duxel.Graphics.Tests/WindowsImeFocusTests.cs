using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunFocusHandoff(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        var thread = Environment.CurrentManagedThreadId;
        void Send(uint message, nint flags = 0) => SendMessageW(platform.WindowHandle, message, 0, flags);
        void Pump() => platform.PollEvents();
        void Reset()
        {
            Send(0x010E);
            ime.ValidationCompositionNotification = null;
            ime.ValidationCaretAcceptance = null;
            ime.ConsumeCommittedText("A");
            ime.ConsumeCommittedText("B");
            ime.ConsumeRecentCommittedText();
            ime.SetCompositionOwner("A");
            Pump();
            ime.ValidationCompletions.Clear();
            ime.ValidationCaretPlacements.Clear();
        }
        void Begin(string? owner)
        {
            ime.SetCompositionOwner(owner);
            Send(0x010D);
            ime.ValidationSetCompositionStrings("한", "정");
            Send(0x010F, 0x8);
        }
        void Place(float x) => ime.SetCaretRect(new(x, 10, 1, 20), new(x, 10, 100, 30), 16, 0);
        foreach (var worker in new[] { false, true })
        {
            void Act(Action action)
            {
                if (worker)
                    Task.Run(action).GetAwaiter().GetResult();
                else
                    action();
            }
            Reset();
            Act(() => Place(10));
            Pump();
            check(ime.ValidationCaretPlacements.Count == 1 && ime.ValidationCaretPlacements[0].Thread == thread,
                "Caret placement runs only on the HWND owner thread : " + worker);
            Begin("A");
            Act(() => { ime.SetCompositionOwner("B"); ime.SetCompositionOwner("B"); Place(200); });
            Pump();
            check(ime.ValidationCompletions.Count == 1 && ime.ValidationCompletions[0] is { Action: 1, Owner: "A" },
                "Leaving a composing input requests CPS_COMPLETE exactly once : " + worker);
            check(ime.ValidationCompletions.All(call => call.Thread == thread && !call.LockHeld),
                "Native completion dispatch holds no state lock on the owner thread : " + worker);
            check(ime.ValidationCaretPlacements.Count == 1 && ime.ValidationCaretPlacements[0].Caret.X == 10,
                "The new input cannot move the previous composition's candidate anchor : " + worker);
            Send(0x010F, 0x800);
            Send(0x010E);
            Pump();
            check(ime.ConsumeCommittedText("A") == "정" && ime.ConsumeCommittedText("B") is null,
                "Focus completion preserves the old input's result ownership : " + worker);
            check(ime.ValidationCaretPlacements.Count == 2 && ime.ValidationCaretPlacements[^1] is { Input: "B", Caret.X: 200 },
                "Native end releases the latest deferred position for the new input : " + worker);
            Act(() => Place(200));
            Pump();
            check(ime.ValidationCaretPlacements.Count == 2,
                "An unchanged placement does not reapply IMM state : " + worker);
        }
        Reset();
        Task.Run(() => Place(10)).GetAwaiter().GetResult();
        ime.SetCompositionOwner("B");
        Pump();
        check(ime.ValidationCaretPlacements.Count == 0, "A queued placement cannot cross logical focus versions");
        Reset();
        Begin("A");
        ime.SetCompositionOwner("B");
        ime.SetCompositionOwner("A");
        Pump();
        check(ime.ValidationCompletions.Count == 0 && ime.GetCompositionText() == "한",
            "Returning to the original input before dispatch cancels the pending completion");
        Reset();
        Begin("A");
        ime.SetCompositionOwner("B");
        Send(0x010E);
        Begin("B");
        Pump();
        check(ime.ValidationCompletions.Count == 0 && ime.GetCompositionText() == "한",
            "A queued request from an ended session cannot complete a later composition");
        Reset();
        Task.Run(() => Place(10)).GetAwaiter().GetResult();
        Begin("A");
        Pump();
        check(ime.ValidationCaretPlacements.Count == 0,
            "A queued placement from before START cannot cross composition sessions");
        Reset();
        Begin(null);
        ime.SetCompositionOwner("B");
        Pump();
        check(ime.ValidationCompletions.Count == 1 && ime.ValidationCompletions[0].Action == 4,
            "An ownerless composition is canceled instead of committed into a later input");
        Reset();
        Begin("A");
        ime.ValidationCompositionNotification = _ =>
        {
            Send(0x010F, 0x800);
            Send(0x010E);
            return true;
        };
        Task.Run(() => { ime.SetCompositionOwner("B"); Place(200); }).GetAwaiter().GetResult();
        Pump();
        check(ime.ValidationCompletions.Count == 1 && ime.ConsumeCommittedText("A") == "정"
            && ime.ValidationCaretPlacements.Count == 1 && ime.ValidationCaretPlacements[0].Input == "B",
            "Synchronous native result/end callbacks safely release the new input's placement");
        Reset();
        Task.Run(() => { Place(10); Place(20); Place(30); }).GetAwaiter().GetResult();
        Pump();
        check(ime.ValidationCaretPlacements.Count == 1 && ime.ValidationCaretPlacements[0].Caret.X == 30,
            "Queued placements coalesce to the latest geometry");
        Reset();
        ime.ValidationCaretAcceptance = () => false;
        Place(10);
        Pump();
        ime.ValidationCaretAcceptance = () => true;
        Place(10);
        Pump();
        check(ime.ValidationCaretPlacements.Count == 2, "An unapplied position is never recorded as successfully cached");
        Place(10);
        Pump();
        check(ime.ValidationCaretPlacements.Count == 2, "A later successful position restores unchanged-state reuse");
        Reset();
        Begin("A");
        ime.ValidationCompositionNotification = _ =>
        {
            if (!User32.PostMessage(platform.WindowHandle, 0x010E, 0, 0))
                throw new InvalidOperationException("Posting the native test END failed.");
            return false;
        };
        ime.SetCompositionOwner("B");
        Pump();
        check(ime.GetCompositionText() is null,
            "An END already queued by the service retires a failed old completion before error publication");
        Reset();
        Begin("A");
        ime.ValidationCompositionNotification = _ =>
        {
            Send(0x010E);
            Begin("B");
            return false;
        };
        ime.SetCompositionOwner("B");
        Pump();
        check(ime.GetCompositionText() == "한",
            "A failed old request cannot fault a new reentrant composition session");
        Reset();
        Begin("A");
        ime.ValidationCompositionNotification = _ => false;
        ime.SetCompositionOwner("B");
        Pump();
        var rejected = false;
        try
        {
            ime.GetCompositionText();
        }
        catch (InvalidOperationException error) { rejected = error.ToString().Contains("rejected", StringComparison.OrdinalIgnoreCase); }
        check(rejected, "A rejected native completion is surfaced on a managed boundary without inventing a result");
        check(Task.Run(() =>
        {
            try
            {
                ime.GetCompositionText();
                return false;
            }
            catch (InvalidOperationException error) { return error.ToString().Contains("rejected", StringComparison.OrdinalIgnoreCase); }
        }).GetAwaiter().GetResult(), "The render worker observes the published native operation failure");
        ime.SetCompositionOwner(null);
        Pump();
        check(ime.ValidationCompletions.Count == 1,
            "A failed completion still permits focus cleanup without issuing native retries");
    }
}
