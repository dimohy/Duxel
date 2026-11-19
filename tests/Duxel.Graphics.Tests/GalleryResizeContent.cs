using Duxel.Core;
using Duxel.Gallery;

internal sealed class GalleryResizeContent(ObserverScreen gallery) : UiScreen
{
    private int _frames;
    private double? _started;
    private bool _scrolled;
    private int _ready;
    internal bool Ready => Volatile.Read(ref _ready) != 0;

    public override void Render(UiImmediateContext ui)
    {
        _started ??= ui.GetTime();
        gallery.Render(ui);
        if (!_scrolled && _frames > 0)
        {
            var tree = ui.AccessibilityTree;
            if (tree is not null)
            {
                var pane = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갤러리 페이지" && element.Node.Role == UiAccessibilityRole.Pane);
                if (tree.RequestScroll(pane.Node.Id, pane.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Gallery source resize benchmark could not scroll to the code.");
                _scrolled = true;
            }
        }
        if (_scrolled && _frames > 3 && ui.GetTime() - _started.Value >= .8)
            Volatile.Write(ref _ready, 1);
        _frames++;
        ui.RequestFrameAt(ui.GetTime());
    }
}
