using System;
using Dux.App;

DuxApp.Run(new DuxAppOptions
{
    Window = new DuxWindowOptions
    {
        Title = "Dux Performance Test"
    },
    Font = new DuxFontOptions
    {
        FontSize = 16,
        InitialGlyphs = PerfTestScreen.GlyphStrings
    },
    Screen = new PerfTestScreen()
});
