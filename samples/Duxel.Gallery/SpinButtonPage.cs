using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal sealed class SpinButtonPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "숫자 입력";
    public string Description => "숫자를 직접 입력하거나 버튼과 키보드로 값을 조절해 보세요. 허용 범위를 바꾸고 증감 버튼을 길게 눌러 볼 수 있습니다.";
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            var quantity = new UiState<double?>(3);
            var weight = new UiState<double?>(1.5);
            var count = 0;
            var last = "변경 없음";
            var quantityOptions = new UiSpinButtonOptions(Minimum : 0, Maximum : 100, PageStep : 10,
                IncrementLabel : "수량 늘리기", DecrementLabel : "수량 줄이기");
            var quantitySample = GallerySample.Create(() =>
            {
                var input = Fluent.SpinButton("수량", quantity.Binding, quantityOptions);
                input.Changed = change => { count++; last = change.Accepted ? "반영됨" : "입력 확인 필요"; };
                return input;
            }, layout : input => Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Label("수량", input), input));
            var amount = quantitySample.Preview;
            var quantityFactory = quantitySample.Source;
            quantitySample.BindSource(() => (Value : quantity.Value, Options : amount.Options, Count : count, Last : last),
                state => "var quantity = new UiState<double?>(" + Number(state.Value) + ");\n"
                    + $"var count = {state.Count};\nvar last = {GallerySample.String(state.Last)};\n"
                    + "var quantityOptions = " + OptionsSource(state.Options) + ";\n\n" + quantityFactory);

            var weightOptions = new UiSpinButtonOptions(Minimum : 0, Maximum : 20, Step : .1, PageStep : 1, Precision : 1,
                Appearance : UiInputAppearance.FilledDarker, IncrementLabel : "무게 늘리기", DecrementLabel : "무게 줄이기");
            var weightSample = GallerySample.Create(() =>
            {
                var input = Fluent.SpinButton("무게", weight.Binding, weightOptions);
                input.Formatter = value => value is { } n ? n.ToString("0.0", CultureInfo.InvariantCulture) + " kg" : "";
                input.Parser = text => string.IsNullOrWhiteSpace(text) ? new(true, null)
                    : double.TryParse(text.Trim().Replace("kg", "", StringComparison.Ordinal).Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                        ? new(true, value) : new(false, null);
                return input;
            }, layout : input => Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Label("무게 (kg)", input), input));
            var mass = weightSample.Preview;
            var weightFactory = weightSample.Source;
            weightSample.BindSource(() => (Value : weight.Value, Options : mass.Options),
                state => "using System.Globalization;\n\nvar weight = new UiState<double?>(" + Number(state.Value) + ");\n"
                    + "var weightOptions = " + OptionsSource(state.Options) + ";\n\n" + weightFactory);
            Samples = [quantitySample, weightSample];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("수량 비우기", () => quantity.Value = null),
                    Fluent.Button("읽기 전용 전환", () => amount.Options = amount.Options with { ReadOnly = !amount.Options.ReadOnly }),
                    Fluent.Button("크기 전환", () => amount.Options = amount.Options with
                    {
                        Size = amount.Options.Size == UiSpinButtonSize.Medium ? UiSpinButtonSize.Small : UiSpinButtonSize.Medium
                    }),
                    Fluent.Button("모양 전환", () => amount.Options = amount.Options with
                    {
                        Appearance = (UiInputAppearance)(((int)amount.Options.Appearance + 1) % 4)
                    })),
                quantitySample, weightSample,
                Fluent.Body1(() => $"수량 : {quantity.Value?.ToString(CultureInfo.InvariantCulture) ?? "비어 있음"} / 무게 : {weight.Value?.ToString(CultureInfo.InvariantCulture) ?? "비어 있음"}"),
                Fluent.Body1(() => $"변경 알림 : {count} / {last}"),
                Fluent.Caption1("↑·↓ : 증감 / Page Up·Down : 큰 단위 / Home·End : 최솟값·최댓값"),
                Fluent.Caption1("Enter를 누르거나 포커스를 옮기면 입력한 값을 적용하고, Esc를 누르면 취소합니다. 증감 버튼을 길게 누르면 값이 계속 바뀝니다."));
        }
        private static string Number(double? value) => value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "d" : "null";
        private static string OptionsSource(UiSpinButtonOptions options) =>
            $"new UiSpinButtonOptions(Minimum : {Number(options.Minimum)}, Maximum : {Number(options.Maximum)},\n"
            + $"    Step : {Number(options.Step)}, PageStep : {Number(options.PageStep)}, Precision : {options.Precision?.ToString(CultureInfo.InvariantCulture) ?? "null"},\n"
            + FormattableString.Invariant($"    Width : {options.Width:R}f, Size : UiSpinButtonSize.{options.Size}, Appearance : UiInputAppearance.{options.Appearance},\n")
            + $"    ReadOnly : {GallerySample.Boolean(options.ReadOnly)}, Invalid : {GallerySample.Boolean(options.Invalid)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)},\n"
            + $"    IncrementLabel : {GallerySample.String(options.IncrementLabel)}, DecrementLabel : {GallerySample.String(options.DecrementLabel)})";
    }
}
