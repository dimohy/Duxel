using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal sealed class SpinButtonPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "숫자 입력";
    public string Description => "Type a number or adjust it using the buttons or keyboard. Change the range and try holding the increase or decrease button.";
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
            var last = GalleryText.Get("No changes");
            var quantityOptions = new UiSpinButtonOptions(Minimum : 0, Maximum : 100, PageStep : 10,
                IncrementLabel : GalleryText.Get("Increase quantity"), DecrementLabel : GalleryText.Get("Decrease quantity"));
            var quantitySample = GallerySample.Create(() =>
            {
                var input = Fluent.SpinButton(GalleryText.Get("Quantity"), quantity.Binding, quantityOptions);
                input.Changed = change => { count++; last = change.Accepted ? GalleryText.Get("Applied") : GalleryText.Get("Check the input"); };
                return input;
            }, layout : input => Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Label(GalleryText.Get("Quantity"), input), input));
            var amount = quantitySample.Preview;
            var quantityFactory = quantitySample.Source;
            quantitySample.BindSource(() => (Value : quantity.Value, Options : amount.Options, Count : count, Last : last),
                state => "var quantity = new UiState<double?>(" + Number(state.Value) + ");\n"
                    + $"var count = {state.Count};\nvar last = {GallerySample.String(state.Last)};\n"
                    + "var quantityOptions = " + OptionsSource(state.Options) + ";\n\n" + quantityFactory);

            var weightOptions = new UiSpinButtonOptions(Minimum : 0, Maximum : 20, Step : .1, PageStep : 1, Precision : 1,
                Appearance : UiInputAppearance.FilledDarker, IncrementLabel : GalleryText.Get("Increase weight"), DecrementLabel : GalleryText.Get("Decrease weight"));
            var weightSample = GallerySample.Create(() =>
            {
                var input = Fluent.SpinButton(GalleryText.Get("Weight"), weight.Binding, weightOptions);
                input.Formatter = value => value is { } n ? n.ToString("0.0", CultureInfo.InvariantCulture) + " kg" : "";
                input.Parser = text => string.IsNullOrWhiteSpace(text) ? new(true, null)
                    : double.TryParse(text.Trim().Replace("kg", "", StringComparison.Ordinal).Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                        ? new(true, value) : new(false, null);
                return input;
            }, layout : input => Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Label(GalleryText.Get("Weight (kg)"), input), input));
            var mass = weightSample.Preview;
            var weightFactory = weightSample.Source;
            weightSample.BindSource(() => (Value : weight.Value, Options : mass.Options),
                state => "using System.Globalization;\n\nvar weight = new UiState<double?>(" + Number(state.Value) + ");\n"
                    + "var weightOptions = " + OptionsSource(state.Options) + ";\n\n" + weightFactory);
            Samples = [quantitySample, weightSample];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Clear quantity"), () => quantity.Value = null),
                    Fluent.Button(GalleryText.Get("Toggle read-only"), () => amount.Options = amount.Options with { ReadOnly = !amount.Options.ReadOnly }),
                    Fluent.Button(GalleryText.Get("Change size"), () => amount.Options = amount.Options with
                    {
                        Size = amount.Options.Size == UiSpinButtonSize.Medium ? UiSpinButtonSize.Small : UiSpinButtonSize.Medium
                    }),
                    Fluent.Button(GalleryText.Get("Change appearance"), () => amount.Options = amount.Options with
                    {
                        Appearance = (UiInputAppearance)(((int)amount.Options.Appearance + 1) % 4)
                    })),
                quantitySample, weightSample,
                Fluent.Body1(() => GalleryText.Format("Quantity: {0} / Weight: {1}",
                    quantity.Value?.ToString(CultureInfo.InvariantCulture) ?? GalleryText.Get("Empty"),
                    weight.Value?.ToString(CultureInfo.InvariantCulture) ?? GalleryText.Get("Empty"))),
                Fluent.Body1(() => GalleryText.Format("Change notifications: {0} / {1}", count, last)),
                Fluent.Caption1(GalleryText.Get("Up/Down: adjust · Page Up/Down: larger steps · Home/End: minimum/maximum")),
                Fluent.Caption1(GalleryText.Get("Press Enter or move focus to apply the value. Press Esc to cancel. Hold an increment or decrement button to keep changing the value.")));
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
