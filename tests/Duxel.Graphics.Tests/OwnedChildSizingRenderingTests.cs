using Duxel.Core;

internal static class OwnedChildSizingRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene(true);
        using var uncached = new Scene(false);
        LeafLayoutRetentionRenderingTests.Compare("owned-sizing", retained, uncached, capture, check);
    }
    private sealed class Scene : ILayoutRetentionScene
    {
        private readonly UiState<string> _value = new("Original value");
        private readonly Input _input;
        private readonly Target _target;
        private readonly UiFluentField _field;
        private readonly UiFluentTooltip _tooltip;
        private readonly UiSizedBox _root;
        public int Measurements => _input.Measurements + _target.Measurements;
        internal Scene(bool retain)
        {
            _input = new(retain, _value.Binding);
            _target = new(retain);
            _field = Fluent.Field(_input);
            _tooltip = Fluent.Tooltip(_target, "Tooltip text");
            _tooltip.Visible = true;
            _root = Dux.SizedBox(Dux.Column(new(Spacing : 80, MainAxisSize : UiMainAxisSize.Min), _field, _tooltip), 360, 320);
        }
        public void Change(int phase)
        {
            if (phase == 1) _value.Value = "Changed value";
            if (phase == 2)
            {
                _field.Options = new() { Label = "Changed field", Hint = "Description" };
                _target.Label = "Changed target";
                _tooltip.Text = "Changed tooltip text";
            }
            if (phase == 3) _root.Options = _root.Options with { Width = 220 };
            if (phase == 4) { _field.Options = _field.Options with { Disabled = true }; _tooltip.Visible = false; }
        }
        public void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            _root.Render(ui);
        }
        public void Dispose() => _root.Dispose();
    }
    private sealed class Input(bool retain, UiBinding<string> value) : UiFluentInput("Input", value, 128)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Target(bool retain) : UiFluentButton("Target")
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
}
