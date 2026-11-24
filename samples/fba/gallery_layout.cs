#:property TargetFramework=net11.0-windows
#:property PublishAot=false
#:property SelfContained=false
#:package Duxel.Windows.App@0.4.0

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

(string Name, UiAlignment Position)[] alignments =
[
    ("Top left", new(-1, -1)), ("Top center", new(0, -1)), ("Top right", new(1, -1)),
    ("Center left", new(-1, 0)), ("Center", new(0, 0)), ("Center right", new(1, 0)),
    ("Bottom left", new(-1, 1)), ("Bottom center", new(0, 1)), ("Bottom right", new(1, 1))
];

UiWidget[] cells = [.. alignments.Select(item =>
    Dux.Stack(new UiStackOptions(Fit: UiStackFit.Expand),
        new UiViewHost(new UiView(ui =>
        {
            var origin = ui.GetCursorScreenPos();
            var size = ui.GetContentRegionAvail();
            var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
            ui.GetWindowDrawList().AddRectFilled(bounds, ui.GetColorU32(UiStyleColor.FrameBg), ui.WhiteTextureId, bounds);
        })),
        Dux.Align(Fluent.Badge(item.Name), item.Position)))];

var root = Dux.Column(new UiFlexOptions(Spacing: 12),
    Fluent.Text(Text.Title2, "Align and SizedBox"),
    Dux.SizedBox(Dux.UniformGrid(new(3, CrossAxisSpacing: 8, MainAxisSpacing: 8, MainAxisExtent: 56), cells), width: 420),
    Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
        Dux.SizedBox(Fluent.Button("Fixed size"), width: 128, height: 40),
        Dux.SizedBox(Fluent.Button("Fixed width"), width: 128),
        Dux.SizedBox(Fluent.Button("Fixed height"), height: 40),
        Fluent.Button("Natural size")));

var options = DuxelApp.Options<GalleryDesign>(Dux.App(root), title: "Duxel · Layout", width: 620, height: 420);
DuxelWindowsApp.Run(options with { Debug = new() { LogStartupTimings = true } });

readonly struct GalleryDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11;
}
