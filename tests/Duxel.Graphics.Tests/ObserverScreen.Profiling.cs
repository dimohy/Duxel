using System.Diagnostics;

namespace Duxel.Gallery;

internal sealed partial class ObserverScreen
{
    internal bool ProfileRenderSections { get; set; }
    internal (long Navigation, long Options, long Page) RenderSectionTicks { get; private set; }

    private bool _profilingRender;
    private long _navigationStart;
    private long _toolbarStart;
    private long _pageStart;

    partial void BeginNavigationProfile()
    {
        _profilingRender = ProfileRenderSections;
        if (_profilingRender)
            _navigationStart = Stopwatch.GetTimestamp();
    }

    partial void BeginToolbarProfile()
    {
        if (_profilingRender)
            _toolbarStart = Stopwatch.GetTimestamp();
    }

    partial void BeginPageProfile()
    {
        if (_profilingRender)
            _pageStart = Stopwatch.GetTimestamp();
    }

    partial void EndRenderProfile()
    {
        if (_profilingRender)
            RenderSectionTicks = (_toolbarStart - _navigationStart, _pageStart - _toolbarStart,
                Stopwatch.GetTimestamp() - _pageStart);
    }
}
