#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:include Host/$(DuxelHost).cs
#:package Duxel.$(DuxelHost).App@0.4.0

using Duxel.App;
using Duxel.Core;
using Duxel.Samples;

var enabled = new UiState<bool>(false);
var clicks = new UiState<int>(0);
var root = Dux.Column(new UiFlexOptions(Spacing: 12),
    Fluent.Text(Text.Title2, "Controls and state"),
    Fluent.Switch("Enable the counter", enabled.Binding),
    Fluent.Button("Count", () =>
    {
        if (enabled.Value)
            clicks.Value++;
    }),
    Fluent.Text(Text.Body1, () => enabled.Value ? $"Count: {clicks.Value}" : "The counter is off."));

var options = DuxelApp.Options<GalleryDesign>(Dux.App(root), title: "Duxel · Controls", width: 480, height: 320)
    with { Debug = new() { LogStartupTimings = true } };
DesktopSampleHost.Run(options, args, useEventDrivenLoop : true);

readonly struct GalleryDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11;
}
