using Duxel.Core;
using InputPresentation = Duxel.Core.UiFluentInputPresentation<Duxel.Core.UiFluentMediumInputSize, Duxel.Core.UiFluentOutlineInput>;
using FieldPresentation = Duxel.Core.UiFluentFieldPresentation<Duxel.Core.UiFluentMediumFieldSize, Duxel.Core.UiVerticalFieldLayout>;

internal static class FormLayoutRetentionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene(true);
        using var uncached = new Scene(false);
        LeafLayoutRetentionRenderingTests.Compare("form", retained, uncached, capture, check);
    }
    private sealed class Scene : ILayoutRetentionScene
    {
        private readonly UiState<string> _value = new("Original value");
        private readonly Input _input;
        private readonly Divider _divider;
        private readonly Menu _menu;
        private readonly Field _field;
        private readonly UiSizedBox _root;
        public int Measurements => _input.Measurements + _divider.Measurements + _menu.Measurements + _field.Measurements;
        internal Scene(bool retain)
        {
            _input = new(retain, _value.Binding);
            _input.Options = new() { ContentBefore = "$", ContentAfter = "USD" };
            _divider = new(retain);
            _menu = new(retain);
            _field = new(retain, Fluent.Input("Field editor", _value.Binding, 128));
            _field.Options = new() { Label = "Field", Hint = "Description" };
            _root = Dux.SizedBox(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), _input, _divider, _menu, _field), 320, 340);
        }
        public void Change(int phase)
        {
            if (phase == 1) _value.Value = "Changed live value";
            if (phase == 2)
            {
                _input.Options = _input.Options with { ContentBefore = "Prefix", ContentAfter = "suffix" };
                _divider.Source = "Changed divider";
                _field.Options = _field.Options with { Label = "Changed label", ValidationMessage = "Validation" };
            }
            if (phase == 3) _root.Options = _root.Options with { Width = 190 };
            if (phase == 4)
            {
                _input.Options = _input.Options with { Invalid = true, ContentAfter = null };
                _field.Options = _field.Options with { Disabled = true };
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
    private sealed class Input(bool retain, UiBinding<string> value) : UiInput<UiFluentTextInputBehavior, InputPresentation>("Editor", value, 128, 280)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Divider(bool retain) : UiDivider<UiFluentDividerPresentation>("Divider")
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Menu(bool retain) : UiMenu<UiFluentMenuPresentation>("Menu", [new("item", "Item")], static _ => { })
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Field(bool retain, UiWidget child) : UiField<UiLabelFieldBehavior, FieldPresentation>(child)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
}
