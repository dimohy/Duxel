#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property PublishAot=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:property DuxelViewFiles="Views/NotificationSettings.duxel.yaml;Views/WorkNotes.duxel.yaml;Views/TransferProgress.duxel.yaml;Views/ReleaseChecklist.duxel.yaml;Views/QuickTools.duxel.yaml;Views/About.duxel.yaml"
#:package Duxel.$(DuxelHost).App@0.4.0
#:package Duxel.FluentDsl@0.4.1-fluentdsl.3
#:include Actions/NotificationActions.cs
#:include Actions/SampleActions.cs

using Duxel.App;
using Duxel.Core;
using Duxel.Generated.Views;
using Duxel.Sample.FluentDsl;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

var session = DuxelApp.PrimarySession;
using var settings = NotificationSettingsView.Create(onSave : NotificationActions.Save, requestFrame : session.RequestFrame);
using var notes = WorkNotesView.Create(onReview : SampleActions.Review, onClearNote : SampleActions.ClearNote, requestFrame : session.RequestFrame);
using var progress = TransferProgressView.Create(onApply : SampleActions.Apply, onResetProgress : SampleActions.ResetProgress, requestFrame : session.RequestFrame);
using var checklist = ReleaseChecklistView.Create(onEvaluate : SampleActions.Evaluate, requestFrame : session.RequestFrame);
using var tools = QuickToolsView.Create(onAddOne : SampleActions.AddOne, onSubtractOne : SampleActions.SubtractOne,
    onDouble : SampleActions.Double, onResetCounter : SampleActions.ResetCounter, requestFrame : session.RequestFrame);
using var about = AboutView.Create(requestFrame : session.RequestFrame);
var activeView = settings.View;
using var navigation = Dux.Wrap(new UiWrapOptions(Spacing : 8, RunSpacing : 6),
    Fluent.Button("Settings", () => activeView = settings.View),
    Fluent.Button("Notes", () => activeView = notes.View),
    Fluent.Button("Progress", () => activeView = progress.View),
    Fluent.Button("Checklist", () => activeView = checklist.View),
    Fluent.Button("Tools", () => activeView = tools.View),
    Fluent.Button("About", () => activeView = about.View));
var screen = Dux.App(new UiView(ui =>
{
    navigation.Render(ui);
    activeView.Render(ui);
}));
var options = DuxelApp.Options<SampleDesign>(screen, title : "Duxel Fluent DSL", width : 720, height : 500)
    with { Debug = new() { LogStartupTimings = true } };

#if DUXEL_Windows
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("This build requires Windows.");
DuxelWindowsApp.Run(options with { Window = options.Window with { MinWidth = 460, MinHeight = 420 } }, session);
#elif DUXEL_Linux
if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("This build requires Linux.");
if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Provide an installed font file as the first argument.");
var font = args[0];
DuxelLinuxApp.Run(options with
{
    Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) },
    Frame = new() { UseEventDrivenPlatformLoop = true }
}, session);
#else
#error This example requires the Windows or Linux Duxel host.
#endif

readonly struct SampleDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11;
}
