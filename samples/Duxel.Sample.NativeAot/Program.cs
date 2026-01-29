using System;
using Duxel.App;
using Duxel.Core;
using Duxel.Core.Dsl;

var vsync = true;

var bindings = new UiDslBindings()
    .BindBool("vsync", () => vsync, value => vsync = value)
    .BindButton("exit", () => Environment.Exit(0));

var render = UiDslAuto.Render("Main.ui");

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel NativeAOT DSL Sample",
        VSync = true
    },
    Font = new DuxelFontOptions
    {
        FontSize = 16
    },
    Dsl = new DuxelDslOptions
    {
        Render = render,
        Bindings = bindings
    }
});

