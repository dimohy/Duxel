using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class LayoutPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;

    private GallerySample<T> Record<T>(GallerySample<T> sample) where T : UiWidget
    {
        _samples.Add(sample);
        return sample;
    }

    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        _samples.Clear();
        var count = 0;
        var row = GallerySample.Create(() =>
        {
            var counter = Fluent.Button(string.Format(GalleryText.Get("Layout counter: {0}"), count));
            counter.Activated = () => counter.Label = string.Format(GalleryText.Get("Layout counter: {0}"), ++count);
            return Dux.Row(new(Spacing : 12), Dux.Expanded(counter), Dux.FlexSpacer(),
                Dux.Flexible(Fluent.Button("Flexible : 1")));
        });
        var rowFactory = row.Source;
        row.BindSource(() => count, value => $"var count = {value};\n\n" + rowFactory);

        var column = GallerySample.Create(() => Dux.Column(
            new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button(GalleryText.Get("Column first item")), Fluent.Button(GalleryText.Get("Column second item"))));

        var wrap = GallerySample.Create(() => Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
            Enumerable.Range(1, 6).Select(index =>
            {
                var button = Fluent.Button(string.Format(GalleryText.Get("Wrap item {0}"), index));
                button.Size = new(150, 32);
                return (UiWidget)button;
            }).ToArray()));

        var gridOptions = new UiUniformGridOptions(3, CrossAxisSpacing : 12, MainAxisSpacing : 12, MainAxisExtent : 40);
        var grid = GallerySample.Create(() => Dux.UniformGrid(gridOptions,
            Enumerable.Range(1, 6).Select(index => (UiWidget)Fluent.Button(string.Format(GalleryText.Get("Cell {0}"), index))).ToArray()));
        var gridFactory = grid.Source;
        grid.BindSource(() => grid.Preview.Options, options =>
            $"var gridOptions = new UiUniformGridOptions({options.CrossAxisCount}, CrossAxisSpacing : 12, MainAxisSpacing : 12, MainAxisExtent : 40);\n\n" + gridFactory);
        var columns = Fluent.Button(GalleryText.Format("Columns: {0}", grid.Preview.Options.CrossAxisCount));
        columns.Activated = () =>
        {
            grid.Preview.Options = grid.Preview.Options with { CrossAxisCount = grid.Preview.Options.CrossAxisCount == 3 ? 2 : 3 };
            columns.Label = GalleryText.Format("Columns: {0}", grid.Preview.Options.CrossAxisCount);
        };

        var right = 0f;
        var stack = GallerySample.Create(() =>
        {
            var button = Fluent.Button(GalleryText.Get("Move overlay"));
            var positioned = Dux.Positioned(button, new(Right : right, Top : 8, Width : 160, Height : 32));
            button.Activated = () => positioned.Options = positioned.Options with { Right = positioned.Options.Right == 0 ? 180 : 0 };
            return Dux.SizedBox(Dux.Stack(new(Fit : UiStackFit.Expand),
                Fluent.Body1(GalleryText.Get("Select the button to move it within Stack and Positioned.")), positioned), width : 1000, height : 64);
        });
        var positionedPreview = (UiPositionedWidget)((UiStackWidget)stack.Preview.Child!).Children[1];
        var stackFactory = stack.Source;
        stack.BindSource(() => positionedPreview.Options.Right,
            value => "var right = " + value!.Value.ToString("R", CultureInfo.InvariantCulture) + "f;\n\n" + stackFactory);

        var alignment = UiAlignment.TopLeft;
        var align = GallerySample.Create(() => Dux.SizedBox(
            Dux.Align(Fluent.Button(GalleryText.Get("Aligned button")), alignment), width : 1000, height : 64));
        var aligned = (UiAlign)align.Preview.Child!;
        var alignFactory = align.Source;
        align.BindSource(() => aligned.Options.Alignment, value =>
            "var alignment = new UiAlignment(" + value.X.ToString("R", CultureInfo.InvariantCulture) + "f, "
            + value.Y.ToString("R", CultureInfo.InvariantCulture) + "f);\n\n" + alignFactory);
        var alignButton = Fluent.Button(GalleryText.Get("Toggle alignment"), () =>
            aligned.Options = aligned.Options with { Alignment = aligned.Options.Alignment == UiAlignment.TopLeft ? UiAlignment.BottomRight : UiAlignment.TopLeft });

        var center = GallerySample.Create(() => Dux.SizedBox(
            Dux.Center(Fluent.Button(GalleryText.Get("Centered button"))), width : 1000, height : 56));
        var padding = GallerySample.Create(() => Dux.Padding(
            Dux.ConstrainedBox(Fluent.Button("Padding + ConstrainedBox"), new(220, 300, 40, 60)), new(12)));
        var container = GallerySample.Create(() => Dux.Container(
            Fluent.Button(GalleryText.Get("Container spacing")), new(Padding : new(16), Margin : new(4))));

        var scrollOptions = new UiScrollOptions(UiScrollAxis.Vertical, Width : 320, Height : 140);
        var scroll = GallerySample.Create(() => Dux.ScrollView(Dux.SizedBox(
            Dux.UniformGrid(new(3, CrossAxisSpacing : 8, MainAxisSpacing : 8, MainAxisExtent : 36),
                Enumerable.Range(1, 24).Select(index => (UiWidget)Dux.Container(
                    Fluent.Body1(string.Format(GalleryText.Get("Scroll item {0}"), index)), new(Padding : new(6)))).ToArray()),
            width : 480), scrollOptions));
        var scrollFactory = scroll.Source;
        scroll.BindSource(() => scroll.Preview.Options, options =>
            $"var scrollOptions = new UiScrollOptions(UiScrollAxis.{options.Axis}, Width : "
            + GallerySample.Number(options.Width) + ", Height : " + GallerySample.Number(options.Height)
            + ");\n\n" + scrollFactory);
        var scrollAxes = Fluent.Button(GalleryText.Format("Scroll direction: {0}", GalleryText.Get("Vertical")));
        scrollAxes.Activated = () =>
        {
            var axis = scroll.Preview.Options.Axis == UiScrollAxis.Vertical ? UiScrollAxis.Both : UiScrollAxis.Vertical;
            scroll.Preview.Options = scroll.Preview.Options with { Axis = axis };
            scrollAxes.Label = GalleryText.Format("Scroll direction: {0}", GalleryText.Get(axis == UiScrollAxis.Both ? "Horizontal and vertical" : "Vertical"));
        };

        return Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Try the controls, then check the C# below each example for the same settings.")),
            Fluent.Subtitle2("Row · Expanded · Flexible · Spacer"), Record(row),
            Fluent.Subtitle2("Column"), Record(column),
            Fluent.Subtitle2("Wrap"), Record(wrap),
            Fluent.Subtitle2("UniformGrid"), columns, Record(grid),
            Fluent.Subtitle2("Stack · Positioned"), Record(stack),
            Fluent.Subtitle2("SizedBox · Align"), alignButton, Record(align),
            Fluent.Subtitle2("Center"), Record(center),
            Fluent.Subtitle2("Padding · ConstrainedBox"), Record(padding),
            Fluent.Subtitle2("Container · Margin"), Record(container),
            Fluent.Subtitle2("ScrollView"), scrollAxes, Record(scroll));
    });
}
