#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:include Host/$(DuxelHost).cs
#:project ../../src/Duxel.Core/Duxel.Core.csproj
#:project ../../src/Duxel.App/Duxel.App.csproj
#:project ../../src/Duxel.$(DuxelHost).App/Duxel.$(DuxelHost).App.csproj

using Duxel.App;
using Duxel.Core;
using Duxel.Samples;

var content = Dux.Padding(Fluent.Text(Text.Title2, "Hello, Duxel"), new UiEdgeInsets(24));
var options = DuxelApp.Options(Dux.App(content), title: "Duxel Hello", width: 360, height: 180)
    with { Debug = new() { LogStartupTimings = true } };
DesktopSampleHost.Run(options, args);
