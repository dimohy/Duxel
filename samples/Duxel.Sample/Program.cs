using System;
using Duxel.App;
using Duxel.Core;
using Duxel.Core.Dsl;

var vsync = true;
var fullscreen = false;
Action<string> log = message => Console.WriteLine("[DuxelTrace] " + message);

var bindings = new UiDslBindings()
    .BindButton("play", () => log("Play"))
    .BindButton("options", () => log("Options"))
    .BindButton("exit", () => Environment.Exit(0))
    .BindButton("refresh", () => log("Refresh"))
    .BindBool("vsync", () => vsync, value => vsync = value)
    .BindBool("fullscreen", () => fullscreen, value => fullscreen = value);

var render = UiDslAuto.Render("Main.ui");

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Widget Sample"
    },
    Font = new DuxelFontOptions
    {
        FontSize = 16,
        InitialGlyphs = SampleScreen.GlyphStrings
    },
    Debug = new DuxelDebugOptions
    {
        Log = log,
        LogEveryNFrames = 1000
    },
    Dsl = new DuxelDslOptions
    {
        Render = render,
        Bindings = bindings
    }
});


