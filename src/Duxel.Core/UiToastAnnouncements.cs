namespace Duxel.Core;

internal struct UiToastAnnouncementState
{
    internal ulong? Revision;
    internal string? Message;
    internal UiToastIntent? Intent;
}
public sealed partial class UiImmediateContext
{
    internal void AnnounceToast(ref UiToastAnnouncementState state, UiToastOptions options, string id, ulong revision)
    {
        if (options.DisableAnnouncement) { state = default; return; }
        if (_accessibility is null || state.Revision == revision) return;
        var message = options.Title + (string.IsNullOrEmpty(options.Body) ? "" : ", " + options.Body);
        // Range-value changes already have their own semantics; do not repeat an unchanged spoken message.
        if (state.Message == message && state.Intent == options.Intent)
        {
            state.Revision = revision;
            return;
        }
        if (!Announce(message, id + "/" + revision, options.AnnouncementPriority)) return;
        state.Revision = revision; state.Message = message; state.Intent = options.Intent;
    }
}
