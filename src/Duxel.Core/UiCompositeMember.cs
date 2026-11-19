namespace Duxel.Core;

internal readonly record struct UiCompositeMember(string Id, bool ConsumesArrows)
{
    internal static int Horizontal(UiKey key, bool rightToLeft)
    {
        var step = key == UiKey.RightArrow ? 1 : key == UiKey.LeftArrow ? -1 : 0;
        return rightToLeft ? -step : step;
    }
}
