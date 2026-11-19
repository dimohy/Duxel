using Duxel.Core;
using TextareaPresentation = Duxel.Core.UiFluentTextareaPresentation<Duxel.Core.UiFluentMediumTextareaSize, Duxel.Core.UiFluentOutlineInput>;

internal static class DelegatedLayoutRetentionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene(true);
        using var uncached = new Scene(false);
        LeafLayoutRetentionRenderingTests.Compare("delegation", retained, uncached, capture, check);
    }
    private sealed class Scene : ILayoutRetentionScene
    {
        private readonly UiState<string> _value = new("First line\nSecond line");
        private readonly Editor _editor;
        private readonly Tooltip _tooltip;
        private readonly UiFluentButton _target = new("Tooltip target");
        private readonly Composed _composed;
        private readonly UiSizedBox _root;
        public int Measurements => _editor.Measurements + _tooltip.Measurements + _composed.Measurements;
        internal Scene(bool retain)
        {
            _editor = new(retain, _value.Binding);
            _tooltip = new(retain, _target) { Visible = true };
            _composed = new(retain, Fluent.Button("Composed target"));
            _root = Dux.SizedBox(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min), _editor, _tooltip, _composed), 320, 340);
        }
        public void Change(int phase)
        {
            if (phase == 1) _value.Value = "Changed live first line\nChanged second line";
            if (phase == 2) { _editor.Height = 100; _tooltip.Text = "Changed tooltip text"; }
            if (phase == 3) _root.Options = _root.Options with { Width = 170 };
            if (phase == 4) { _tooltip.Visible = false; _target.Label = "Changed target"; _editor.Options = new() { Wrap = false, Flags = UiInputTextFlags.ReadOnly }; }
        }
        public void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            _root.Render(ui);
        }
        public void Dispose() => _root.Dispose();
    }
    private sealed class Editor(bool retain, UiBinding<string> value) : UiMultilineInput<UiFluentMultilineInputBehavior, TextareaPresentation>("Editor", value, width : 280, height : 80)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Tooltip(bool retain, UiWidget target) : UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(target, "Tooltip text")
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Composed : UiWidget
    {
        private readonly bool _retain;
        private readonly UiWidget _root;
        internal int Measurements;
        internal Composed(bool retain, UiWidget root) { _retain = retain; _root = root; OwnChild("root", root); }
        protected override bool CanRetainMeasurement => _retain && CanRetainOwnedMeasurement(_root);
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
        protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
    }
}
