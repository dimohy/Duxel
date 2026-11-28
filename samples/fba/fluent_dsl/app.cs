#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property PublishAot=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DuxelViewFiles=Views/**/*.duxel.yaml
#:package Duxel.$(DuxelHost).App@0.4.0
#:package Duxel.FluentDsl@0.4.1-fluentdsl.4
#:include Actions/*.cs

using Duxel.App;
using Duxel.Core;
using Duxel.Generated.Views;
using Duxel.Sample.FluentDsl;
var redraw = DuxelApp.RequestFrame;
using var settings = NotificationSettingsView.Create(onSave : NotificationActions.Save, requestFrame : redraw);
using var notes = WorkNotesView.Create(onReview : SampleActions.Review, onClearNote : SampleActions.ClearNote, requestFrame : redraw);
using var progress = TransferProgressView.Create(onApply : SampleActions.Apply, onResetProgress : SampleActions.ResetProgress, requestFrame : redraw);
using var checklist = ReleaseChecklistView.Create(onEvaluate : SampleActions.Evaluate, requestFrame : redraw);
using var tools = QuickToolsView.Create(onAddOne : SampleActions.AddOne, onSubtractOne : SampleActions.SubtractOne,
    onDouble : SampleActions.Double, onResetCounter : SampleActions.ResetCounter, requestFrame : redraw);
using var about = AboutView.Create(requestFrame : redraw);

(string Label, IUiView View)[] pages =
[
    ("Settings", settings.View), ("Notes", notes.View), ("Progress", progress.View),
    ("Checklist", checklist.View), ("Tools", tools.View), ("About", about.View)
];
var activeView = pages[0].View;
using var navigation = Dux.Wrap(new UiWrapOptions(Spacing : 8, RunSpacing : 6),
    pages.Select(page => Fluent.Button(page.Label, () => activeView = page.View)).ToArray());
var root = new UiView(ui =>
{
    navigation.Render(ui);
    activeView.Render(ui);
});
var options = DuxelApp.Options(root, title : "Duxel Fluent DSL", width : 720, height : 500);
FluentDslHost.Run(options with
{
    Window = options.Window with { MinWidth = 460, MinHeight = 420 },
    Design = UiCompiledDesign.Windows11,
    Debug = new() { LogStartupTimings = true }
}, args);
