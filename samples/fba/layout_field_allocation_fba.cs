// FBA: Field parent allocation, actual editors, labels and descriptions.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new FieldAllocationPage();
var options = DuxelApp.Options<FieldAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Field",
    width : 940,
    height : 720);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 820, MinHeight = 620 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct FieldAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

[UiWidgetStyle(typeof(UiField<,>), typeof(UiLabelFieldBehavior),
    typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize, UiHorizontalFieldLayout>))]
public partial class AllocationHorizontalField;

sealed class FieldAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var email = new UiState<string>("hello@example.com");
        var name = new UiState<string>("Duxel");
        var vertical = Fluent.Field(Fluent.Input("Email editor", email.Binding, 128));
        var horizontal = new AllocationHorizontalField(Fluent.Input("Name editor", name.Binding, 128));
        vertical.Options = new() { Label = "Email", Required = true, Hint = "Changes keep the same editor.", ValidationMessage = "Check this value" };
        horizontal.Options = new() { Label = "Name", Required = true, Hint = "Changes keep the same editor.", ValidationMessage = "Check this value" };
        var verticalBox = Dux.SizedBox(vertical, 180, 180);
        var horizontalBox = Dux.SizedBox(horizontal, 180, 180);
        UiSizedBox[] boxes = [verticalBox, horizontalBox];

        var width = Fluent.Button("Width : 180");
        width.Activated = () =>
        {
            var next = verticalBox.Options.Width == 180 ? 300 : 180;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 180");
        height.Activated = () =>
        {
            var next = verticalBox.Options.Height == 180 ? 80 : 180;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };
        var enabled = true;
        var validation = Fluent.Button("Validation : On");
        validation.Activated = () =>
        {
            enabled = !enabled;
            vertical.Options = vertical.Options with { ValidationMessage = enabled ? "Check this value" : null };
            horizontal.Options = horizontal.Options with { ValidationMessage = enabled ? "Check this value" : null };
            validation.Label = enabled ? "Validation : On" : "Validation : Off";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated Field roots"),
                Fluent.Body1("Change the parent size, type in either field, and toggle validation."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Vertical Field"), verticalBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Horizontal Field"), horizontalBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), width, height, validation)
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
