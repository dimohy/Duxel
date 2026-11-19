using Duxel.Core;

internal static class TabMeasurementRetentionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene<Retained>();
        using var uncached = new Scene<Uncached>();
        LeafLayoutRetentionRenderingTests.Compare("tab-metrics", retained, uncached, capture, check);
    }
    private interface ITracked : IUiTabListPresentation
    {
        static abstract int Calls { get; }
    }
    private sealed class Scene<TPresentation> : ILayoutRetentionScene where TPresentation : ITracked
    {
        private readonly UiState<string?> _value = new("a");
        private readonly UiTabList<UiFluentTabListBehavior, TPresentation> _tabs;
        private readonly UiSizedBox _root;
        public int Measurements => TPresentation.Calls;
        internal Scene()
        {
            _tabs = new("Measured tabs", _value.Binding, new UiTabOption[]
                { new("a", "Alpha"), new("b", "Beta"), new("c", "Gamma"), new("d", "Delta") },
                new() { Overflow = new() });
            _root = Dux.SizedBox(_tabs, 420, 40);
        }
        public void Change(int phase)
        {
            if (phase == 1) _value.Value = "b";
            if (phase == 2) _tabs.SetItems(new UiTabOption[]
                { new("a", "Changed Alpha"), new("b", "Changed Beta"), new("c", "Changed Gamma"), new("d", "Changed Delta") });
            if (phase == 3) _root.Options = _root.Options with { Width = 150 };
            if (phase == 4) { _value.Value = "c"; _tabs.Options = _tabs.Options with { RightToLeft = true }; }
        }
        public void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            _root.Render(ui);
            _tabs.RenderPanel(ui, "a", static context => context.Text("Alpha body"), focusable : false);
            _tabs.RenderPanel(ui, "b", static context => context.Text("Beta body"), focusable : false);
            _tabs.RenderPanel(ui, "c", static context => context.Text("Gamma body"), focusable : false);
            _tabs.RenderPanel(ui, "d", static context => context.Text("Delta body"), focusable : false);
        }
        public void Dispose() => _root.Dispose();
    }
    private readonly struct Retained : ITracked
    {
        public static bool CanRetainMeasurement => true;
        public static int Calls { get; private set; }
        public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options)
        {
            Calls++;
            return UiFluentTabListPresentation.MeasureTab(context, label, selected, options);
        }
        public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected, UiButtonInteraction interaction, UiTabListOptions options) =>
            UiFluentTabListPresentation.PaintTab(context, label, bounds, selected, interaction, options);
        public static UiVector2 MeasureOverflow(UiControlContext context, UiTabListOptions options) => UiFluentTabListPresentation.MeasureOverflow(context, options);
        public static void PaintOverflow(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiTabListOptions options) => UiFluentTabListPresentation.PaintOverflow(context, bounds, interaction, options);
    }
    private readonly struct Uncached : ITracked
    {
        public static int Calls { get; private set; }
        public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options)
        {
            Calls++;
            return UiFluentTabListPresentation.MeasureTab(context, label, selected, options);
        }
        public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected, UiButtonInteraction interaction, UiTabListOptions options) =>
            UiFluentTabListPresentation.PaintTab(context, label, bounds, selected, interaction, options);
        public static UiVector2 MeasureOverflow(UiControlContext context, UiTabListOptions options) => UiFluentTabListPresentation.MeasureOverflow(context, options);
        public static void PaintOverflow(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiTabListOptions options) => UiFluentTabListPresentation.PaintOverflow(context, bounds, interaction, options);
    }
}
