using System;
using Duxel.App;
using Duxel.Core;

Action<string> log = message => Console.WriteLine("[DuxelTrace] " + message);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Widget Sample"
    },
    Font = new DuxelFontOptions
    {
        FontSize = 16
    },
    Debug = new DuxelDebugOptions
    {
        Log = log,
        LogEveryNFrames = 1000
    },
    Screen = new SampleScreen()
});


