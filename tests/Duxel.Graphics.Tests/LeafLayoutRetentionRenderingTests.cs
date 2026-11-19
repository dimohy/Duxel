using Duxel.Core;

internal static class LeafLayoutRetentionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene(true);
        using var uncached = new Scene(false);
        Compare("leaf", retained, uncached, capture, check);
    }
    internal static void Compare(string prefix, ILayoutRetentionScene retained, ILayoutRetentionScene uncached,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        byte[]? previous = null;
        for (var phase = 0; phase < 5; phase++)
        {
            retained.Change(phase); uncached.Change(phase);
            for (var warm = 0; warm < 3; warm++)
            {
                capture($"{prefix}-{phase}-retained-warm-{warm}", retained.Render);
                capture($"{prefix}-{phase}-uncached-warm-{warm}", uncached.Render);
            }
            var count = retained.Measurements;
            var expected = capture($"{prefix}-{phase}-uncached", uncached.Render);
            var actual = capture($"{prefix}-{phase}-retained", retained.Render);
            check(expected.AsSpan().SequenceEqual(actual), prefix + " retained real parent matches measurement-bypassed reference pixels at phase " + phase);
            var stable = true;
            for (var frame = 0; frame < 4; frame++)
                stable &= actual.AsSpan().SequenceEqual(capture($"{prefix}-{phase}-replay-{frame}", retained.Render));
            check(stable && retained.Measurements == count,
                "retained leaf parent replays unchanged geometry across acquired frame images without child remeasurement at phase " + phase);
            if (previous is not null)
                check(!previous.AsSpan().SequenceEqual(actual), "changed value/property/allocation/items produce visibly changed real pixels at phase " + phase);
            previous = actual;
        }
    }
    private sealed class Scene : ILayoutRetentionScene
    {
        private readonly UiState<bool?> _checked = new(null);
        private readonly UiState<float> _slider = new(.25f);
        private readonly UiState<string?> _radio = new("a");
        private readonly Checkbox _checkbox;
        private readonly Slider _sliderWidget;
        private readonly Radio _radioWidget;
        private readonly UiSizedBox _root;
        public int Measurements => _checkbox.Measurements + _sliderWidget.Measurements + _radioWidget.Measurements;
        internal Scene(bool retain)
        {
            _checkbox = new(retain, _checked.Binding);
            _sliderWidget = new(retain, _slider.Binding);
            _radioWidget = new(retain, _radio.Binding);
            _root = Dux.SizedBox(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), _checkbox, _sliderWidget, _radioWidget), 280, 300);
        }
        public void Change(int phase)
        {
            if (phase == 1) { _checked.Value = true; _slider.Value = .75f; _radio.Value = "b"; }
            if (phase == 2)
            {
                _checkbox.Label = "Changed label";
                _sliderWidget.Size = new(240, 48);
                _radioWidget.SetItems(new UiRadioOption[] { new("a", "Changed Alpha", Description : "A longer description wraps naturally."), new("b", "Beta") });
            }
            if (phase == 3) _root.Options = _root.Options with { Width = 190 };
            if (phase == 4)
            {
                _radio.Value = "a";
                _radioWidget.SetItems(new UiRadioOption[] { new("a", "One remaining option") });
            }
        }
        public void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            _root.Render(ui);
        }
        public void Dispose() => _root.Dispose();
    }
    private sealed class Checkbox(bool retain, UiBinding<bool?> value) : UiToggle<bool?, UiNullableToggleValue, UiReleaseButtonBehavior, UiFluentCheckboxPresentation>("Checkbox", value)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Slider(bool retain, UiBinding<float> value) : UiSlider<UiFluentSliderBehavior, UiFluentSliderPresentation>("Slider", value, 0, 1)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Radio(bool retain, UiBinding<string?> value) : UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
        "Choice", value, new UiRadioOption[] { new("a", "Alpha"), new("b", "Beta") })
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
}

internal interface ILayoutRetentionScene : IDisposable
{
    int Measurements { get; }
    void Change(int phase);
    void Render(UiImmediateContext ui);
}
