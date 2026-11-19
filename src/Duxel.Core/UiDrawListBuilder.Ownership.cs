namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    private void ReleaseUnpublishedDrawLists()
    {
        // Commit metadata before any channel clears its command storage.
        if (_hasPendingPrimitiveCommand)
            CommitPrimitiveCommand();
        if (_channels is null)
            Release(_drawLists);
        else
            foreach (var channel in _channels)
                Release(channel.DrawLists);

        static void Release(List<UiDrawList> lists)
        {
            foreach (var list in lists)
                list.ReleasePooled();
            lists.Clear();
        }
    }
}
