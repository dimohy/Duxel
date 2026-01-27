using Dux.App;

DuxApp.Run(new DuxAppOptions
{
    Window = new DuxWindowOptions
    {
        Title = "Dux Widget Sample"
    },
    Font = new DuxFontOptions
    {
        FontSize = 16,
        //InitialGlyphs = SampleScreen.GlyphStrings
    },
    Debug = new DuxDebugOptions
    {
        Log = message =>
        {
            Console.WriteLine("[DuxTrace] " + message);
        },
        LogEveryNFrames = 1000
    },
    Screen = new SampleScreen()
});

