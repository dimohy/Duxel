using System;
using Duxel.App;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Performance Test"
    },
    Font = new DuxelFontOptions
    {
        FontSize = 16,
        InitialGlyphs = PerfTestScreen.GlyphStrings
    },
    Screen = new PerfTestScreen()
});

