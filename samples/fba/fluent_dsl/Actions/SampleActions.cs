using System.Globalization;
using Duxel.Generated.Views;

namespace Duxel.Sample.FluentDsl;

internal static class SampleActions
{
    internal static void Review(WorkNotesState state)
    {
        var draft = state.Draft.Value.Trim();
        var kind = state.FollowUp.Value ? "Follow-up" : "Note";
        state.Summary.Value = draft.Length == 0 ? "Write a note first." : $"{kind} reviewed ({draft.Length} characters).";
    }

    internal static void ClearNote(WorkNotesState state)
    {
        state.Draft.Value = "";
        state.FollowUp.Value = false;
        state.Summary.Value = "Draft cleared.";
    }

    internal static void Apply(TransferProgressState state)
        => state.Status.Value = FormattableString.Invariant($"Recorded {state.Completion.Value:0.##}%.");

    internal static void ResetProgress(TransferProgressState state)
    {
        state.Completion.Value = 0;
        state.Status.Value = "Progress reset.";
    }

    internal static void Evaluate(ReleaseChecklistState state)
    {
        var complete = (state.BuildChecked.Value ? 1 : 0) + (state.NotesChecked.Value ? 1 : 0)
            + (state.RollbackChecked.Value ? 1 : 0);
        state.BadgeText.Value = complete == 3 ? "Ready" : "In review";
        state.Status.Value = complete == 3 ? "All release requirements met." : $"{complete} of 3 requirements met.";
    }

    internal static void AddOne(QuickToolsState state) => SetCount(state, (long)state.Count.Value + 1);
    internal static void SubtractOne(QuickToolsState state) => SetCount(state, (long)state.Count.Value - 1);
    internal static void Double(QuickToolsState state) => SetCount(state, (long)state.Count.Value * 2);
    internal static void ResetCounter(QuickToolsState state) => SetCount(state, 0);

    private static void SetCount(QuickToolsState state, long value)
    {
        state.Count.Value = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        state.Result.Value = "Counter: " + state.Count.Value.ToString(CultureInfo.InvariantCulture);
    }
}
