using Duxel.Core;

namespace Duxel.App;

internal sealed class DuxelRootContentScreen(UiScreen inner):UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout();
        inner.Render(ui);
    }
}
