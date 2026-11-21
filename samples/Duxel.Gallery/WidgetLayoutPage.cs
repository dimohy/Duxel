using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class WidgetLayoutPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "위젯 배치";
    public string Description => "Add spacing, rotate, or scale a widget. Transformed widgets still accept input, and their pop-ups open aligned with the screen.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiEdgeInsets AppliedMargin => _content?.AppliedMargin ?? default;
    internal int Activations => _content?.Activations ?? 0;
    internal UiWidgetTransform AppliedTransform => _content?.AppliedTransform ?? UiWidgetTransform.Identity;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    void IObserverNaturalPage.PrepareContent(UiImmediateContext ui) => (_content ??= new()).Prepare(ui);
    public void Render(UiImmediateContext ui)
    {
        var content = _content ??= new();
        content.Prepare(ui);
        content.Root.Render(ui);
    }
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private readonly UiState<float> _left = new(8), _top = new(8), _right = new(8), _bottom = new(8);
        private readonly UiState<bool?> _checked = new(false);
        private readonly UiState<float> _value = new(.5f);
        private readonly UiState<float> _rotation = new(0), _scale = new(1);
        private readonly UiState<bool?> _visual = new(false);
        private readonly UiState<string> _text = new(GalleryText.Get("Sample input"));
        private readonly UiState<int> _activations = new(0);
        private int _originIndex = 1;
        private UiWidgetTransform _transform = UiWidgetTransform.Identity;
        private UiVector2 _buttonSize;
        private readonly UiWidget[] _targets;
        private readonly List<IGallerySample> _samples = [];
        internal IReadOnlyList<IGallerySample> Samples => _samples;
        internal UiWidget Root { get; }
        internal int Activations => _activations.Value;
        internal UiEdgeInsets AppliedMargin => _targets[0].Margin;
        internal UiWidgetTransform AppliedTransform => _transform;

        internal Content()
        {
            var button = Record(GallerySample.Create(() =>
            {
                var button = Fluent.Button(GalleryText.Get("Run button"), () => _activations.Value++);
                button.Size = new(140, 32);
                return button;
            }), () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var checkbox = Record(GallerySample.Create(() => Fluent.Checkbox(GalleryText.Get("Checkbox with margin"), _checked.Binding)),
                () => _checked.Value, value => $"var _checked = new UiState<bool?>({(value is null ? "null" : GallerySample.Boolean(value.Value))});");
            var slider = Record(GallerySample.Create(() =>
            {
                var slider = Fluent.Slider(GalleryText.Get("Slider with margin"), _value.Binding, 0, 1, .01f);
                slider.Size = new(180, 32);
                slider.Options = slider.Options with { ShowStepMarkers = false };
                return slider;
            }), () => _value.Value, value => $"var _value = new UiState<float>({Number(value)});");
            var input = Record(GallerySample.Create(() => Fluent.Input(GalleryText.Get("Transformed input"), _text.Binding, 200, 200)),
                () => _text.Value, value => $"var _text = new UiState<string>({GallerySample.String(value)});");
            var popover = Record(GallerySample.Create(() => Fluent.Popover(GalleryText.Get("Transformed popover"),
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1(GalleryText.Get("The popover opens in screen coordinates.")), Fluent.Button(GalleryText.Get("Run from the popover"), () => _activations.Value++)),
                options : new(Width : 240))), () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var menu = Record(GallerySample.Create(() => new UiMenu<UiFluentMenuPresentation>(GalleryText.Get("Transformed menu"),
                [new("run", GalleryText.Get("Run from the menu"))], _ => _activations.Value++)),
                () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var tooltip = Record(GallerySample.Create(() => Fluent.Tooltip(Fluent.Button(GalleryText.Get("Transformed tooltip")),
                GalleryText.Get("The tooltip appears next to the rotated target, aligned with the screen."))), () => 0, _ => string.Empty);
            var legacy = Record(GallerySample.Create(() => new LegacyPortalPreview(() => _activations.Value++)),
                () => _activations.Value, value => $"var _activations = new UiState<int>({value});",
                GallerySample.LocalizeSource(GalleryPageSource.Declaration(nameof(LegacyPortalPreview))));
            _targets = [button.Preview, checkbox.Preview, slider.Preview, input.Preview,
                popover.Preview, menu.Preview, tooltip.Preview, legacy.Preview];
            var controls = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Set a margin on each side of the widget. Clicking the margin does not activate the button.")),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
                    Edge(GalleryText.Get("Shared left margin"), _left), Edge(GalleryText.Get("Shared top margin"), _top),
                    Edge(GalleryText.Get("Shared right margin"), _right), Edge(GalleryText.Get("Shared bottom margin"), _bottom)),
                Fluent.Button(GalleryText.Get("Reset shared margins"), () => _left.Value = _top.Value = _right.Value = _bottom.Value = 0),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
                    Numeric(GalleryText.Get("Shared rotation"), _rotation, -180, 180, 1), Numeric(GalleryText.Get("Shared scale"), _scale, .25f, 3, .05f),
                    Fluent.Checkbox(GalleryText.Get("Visual transform (keep layout size)"), _visual.Binding),
                    Fluent.Button(GalleryText.Get("Change rotation origin (visual transform)"), () => _originIndex = (_originIndex + 1) % 3),
                    Fluent.Button(GalleryText.Get("Reset shared transform"), () => { _rotation.Value = 0; _scale.Value = 1; _visual.Value = false; _originIndex = 1; })));
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                controls,
                Fluent.Body1(() => GalleryText.Format("Margins: left {0:F0} / top {1:F0} / right {2:F0} / bottom {3:F0} px",
                    _left.Value, _top.Value, _right.Value, _bottom.Value)),
                Fluent.Body1(() => GalleryText.Format("Button layout: {0:F0} × {1:F0} px", _buttonSize.X, _buttonSize.Y)),
                Fluent.Body1(() => GalleryText.Format("Rotation: {0:F0}° / Scale: {1:F2} / Origin: {2} / {3}", _rotation.Value, _scale.Value,
                    GalleryText.Get(_originIndex switch { 0 => "Top left", 1 => "Center", _ => "Bottom right" }),
                    GalleryText.Get(_transform.Mode == UiWidgetTransformMode.Layout ? "Lay out at transformed size" : "Keep original layout"))),
                button, checkbox, slider, input, popover, menu, tooltip, legacy,
                Fluent.Body1(() => GalleryText.Format("Button actions: {0} / Checkbox: {1} / Value: {2:F2}", Activations,
                    GalleryText.Get(_checked.Value == true ? "Checked" : "Unchecked"), _value.Value)),
                Fluent.Body1(() => GalleryText.Format("Edited text: {0}", _text.Value))));
        }

        private GallerySample<T> Record<T, TState>(GallerySample<T> sample, Func<TState> read, Func<TState, string> setup,
            string declaration = "") where T : UiWidget
        {
            var factory = sample.Source;
            sample.BindSource(() => (State : read(), sample.Preview.Margin, sample.Preview.Transform), state =>
            {
                var margin = state.Margin;
                var transform = state.Transform;
                var origin = transform.Origin is { } pivot ? $"new UiVector2({Number(pivot.X)}, {Number(pivot.Y)})" : "null";
                return setup(state.State) + "\n\n" + factory
                    + $"\nexample.Margin = new UiEdgeInsets({Number(margin.Left)}, {Number(margin.Top)}, {Number(margin.Right)}, {Number(margin.Bottom)});"
                    + $"\nexample.Transform = new UiWidgetTransform(Scale : {Number(transform.Scale)}, RotationDegrees : {Number(transform.RotationDegrees)},\n"
                    + $"    Origin : {origin}, Mode : UiWidgetTransformMode.{transform.Mode});"
                    + (declaration.Length == 0 ? string.Empty : "\n\n" + declaration);
            });
            _samples.Add(sample);
            return sample;
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";
        private static UiWidget Edge(string label, UiState<float> state) => Numeric(label, state, 0, 40, 1);
        private static UiWidget Numeric(string label, UiState<float> state, float minimum, float maximum, float step)
        {
            var slider = Fluent.Slider(label, state.Binding, minimum, maximum, step);
            slider.Size = new(220, 32);
            slider.Options = slider.Options with { ShowStepMarkers = false };
            return Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label), slider);
        }

        internal void Prepare(UiImmediateContext ui)
        {
            var margin = new UiEdgeInsets(_left.Value, _top.Value, _right.Value, _bottom.Value);
            var pivot = _originIndex switch
            {
                0 => new UiVector2(0, 0),
                1 => new(.5f, .5f),
                _ => new(1, 1)
            };
            var mode = _visual.Value == true ? UiWidgetTransformMode.Visual : UiWidgetTransformMode.Layout;
            if (_transform.Scale != _scale.Value || _transform.RotationDegrees != _rotation.Value || _transform.Origin != pivot || _transform.Mode != mode)
                _transform = new(_scale.Value, _rotation.Value, pivot, mode);
            foreach (var target in _targets)
            {
                target.Margin = margin;
                target.Transform = _transform;
            }
            _buttonSize = _targets[0].MeasureConstrained(ui, new UiBoxConstraints());
        }
        public void Dispose() => Root.Dispose();
    }
}
