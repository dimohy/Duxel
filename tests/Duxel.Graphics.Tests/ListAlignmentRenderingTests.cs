using Duxel.Core;

internal static class ListAlignmentRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiFluentColorScheme scheme)
    {
        foreach (var kind in new[] { "single", "composite", "disabled" })
        {
            var disabled = kind == "disabled";
            var composite = kind == "composite";
            var text = new UiState<string>("회의 준비");
            var selected = new UiState<string?>(disabled ? null : "person");
            var actualName = Fluent.Body1("정세일");
            var referenceName = Fluent.Body1("정세일");
            using var referenceInput = Fluent.Input("메모", text.Binding, 100, 80);
            using var referenceButton = Fluent.Button("실행");
            UiWidget content = composite ? Dux.Row(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min,
                CrossAxisAlignment: UiCrossAxisAlignment.Center), actualName,
                Fluent.Input("메모", text.Binding, 100, 80), Fluent.Button("실행")) : actualName;
            using var list = new UiList<UiFluentListPresentation>("정렬 확인", selected.Binding,
                [new("person", "정세일", content, disabledSelection: disabled)],
                new(Navigation: composite ? UiListNavigation.Composite : UiListNavigation.Items, Width: 280));
            var row = new UiRect(20, 80, 280, 32);
            void Draw(UiImmediateContext ui, int mode)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var context = new UiControlContext(ui);
                context.Frame(new(0, 0, 320, 240), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
                if (mode == 0)
                {
                    ui.RenderLayoutChild(list, row, new(0, 0, 320, 240));
                    return;
                }
                ui.BeginDisabled(disabled);
                try
                {
                    UiFluentListPresentation.PaintCheckmark(context, new(row.X, row.Y, 32, 32), default, !disabled);
                    var measured = referenceName.Measure(ui, new(248, 32));
                    var x = row.X + 32;
                    var y = mode == 2 ? row.Y : row.Y + (row.Height - measured.Y) / 2;
                    ui.RenderLayoutChild(referenceName, new(x, y, measured.X, measured.Y), row);
                    if (composite)
                    {
                        x += measured.X + 8;
                        var inputSize = referenceInput.Measure(ui, new(80, 32));
                        ui.RenderLayoutChild(referenceInput, new(x, row.Y + (row.Height - inputSize.Y) / 2, inputSize.X, inputSize.Y), row);
                        x += inputSize.X + 8;
                        var buttonSize = referenceButton.Measure(ui, new(248, 32));
                        ui.RenderLayoutChild(referenceButton, new(x, row.Y + (row.Height - buttonSize.Y) / 2, buttonSize.X, buttonSize.Y), row);
                    }
                }
                finally { ui.EndDisabled(); }
            }
            var actual = capture(kind + "-actual", ui => Draw(ui, 0));
            var expected = capture(kind + "-center-reference", ui => Draw(ui, 1));
            check(actual.AsSpan().SequenceEqual(expected),
                "GPU List checkbox and text match independent centered placement: " + kind);
            check(!actual.AsSpan().SequenceEqual(capture(kind + "-old-top-reference", ui => Draw(ui, 2))),
                "GPU List alignment distinguishes the retired top-aligned text: " + kind);
            referenceName.Dispose();
        }
    }
}
