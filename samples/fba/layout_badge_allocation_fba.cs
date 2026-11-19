// FBA: Badge parent roots, counter visibility, dot mode and clipping.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new BadgeAllocationPage();
var options = DuxelApp.Options<BadgeAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Badge",
    width : 860,
    height : 600);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct BadgeAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class BadgeAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var count = new UiState<int>(0);
        var dot = new UiState<bool>(false);
        var badge = Fluent.Badge("Status");
        var counter = Fluent.CounterBadge(() => new UiCounterBadgeValue(count.Value, Dot : dot.Value));
        var badgeBox = Dux.SizedBox(badge, 100, 64);
        var counterBox = Dux.SizedBox(counter, 100, 64);
        UiSizedBox[] boxes = [badgeBox, counterBox];

        var width = Fluent.Button("Width : 100");
        width.Activated = () =>
        {
            var next = badgeBox.Options.Width == 100 ? 240 : 100;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 64");
        height.Activated = () =>
        {
            var next = badgeBox.Options.Height == 64 ? 8 : 64;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };
        var dotMode = Fluent.Button("Dot : Off");
        dotMode.Activated = () =>
        {
            dot.Value = !dot.Value;
            dotMode.Label = dot.Value ? "Dot : On" : "Dot : Off";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated Badge roots"),
                Fluent.Body1("Change the root size and counter. A hidden zero counter keeps the assigned area."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Badge"), badgeBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("CounterBadge"), counterBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), width, height, dotMode),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Button("Increment", () => count.Value++),
                    Fluent.Button("Zero", () => count.Value = 0),
                    Fluent.Button("Overflow", () => count.Value = 108)),
                Fluent.Text(() => $"Count : {count.Value}")
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
