using Duxel.Core;

internal static class ContentLayoutRetentionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var retained = new Scene(true);
        using var uncached = new Scene(false);
        LeafLayoutRetentionRenderingTests.Compare("content", retained, uncached, capture, check);
    }
    private sealed class Scene : ILayoutRetentionScene
    {
        private readonly Text _text;
        private readonly Link _link;
        private readonly Progress _progress;
        private readonly Badge _badge;
        private readonly Counter _counter;
        private readonly Spinner _spinner;
        private readonly UiState<UiProgressValue> _value = new(new(.25f));
        private readonly UiSizedBox _root;
        public int Measurements => _text.Measurements + _link.Measurements + _progress.Measurements + _badge.Measurements + _counter.Measurements + _spinner.Measurements;
        internal Scene(bool retain)
        {
            _text = new(retain);
            _link = new(retain);
            _progress = new(retain, () => _value.Value) { Size = new(240, 4) };
            _badge = new(retain);
            _counter = new(retain);
            _spinner = new(retain);
            _root = Dux.SizedBox(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), _text, _link, _progress, _badge, _counter, _spinner), 280, 320);
        }
        public void Change(int phase)
        {
            if (phase == 1) _value.Value = new(.75f);
            if (phase == 2)
            {
                _text.Source = "Changed content wraps across a narrower parent";
                _link.Label = "Changed link with descriptive words";
                _badge.Source = "Changed badge";
                _progress.Size = new(240, 6);
                _spinner.Label = "Changed spinner";
            }
            if (phase == 3) _root.Options = _root.Options with { Width = 190 };
            if (phase == 4) { _text.Source = string.Empty; _counter.Count = 0; }
        }
        public void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            _root.Render(ui);
        }
        public void Dispose() => _root.Dispose();
    }
    private sealed class Text(bool retain) : UiText<UiFluentWrappedTextPresentation>("Static content")
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Link(bool retain) : UiLink<UiLinkNavigationBehavior, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
        "Static link", new("/reference", UriKind.Relative), static _ => { })
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Progress(bool retain, Func<UiProgressValue> source) : UiProgress<Func<UiProgressValue>, UiGetterProgressSource, UiFluentProgressPresentation>(source)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Badge(bool retain) : UiBadge<string, UiTextBadgeBehavior, UiFluentBadgePresentation>("Badge")
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Counter(bool retain) : UiCounterBadge<UiFluentBadgePresentation>(5)
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
    private sealed class Spinner(bool retain) : UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>("Spinner", new() { Paused = true })
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => retain && base.CanRetainMeasurement;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return base.MeasureContent(context); }
    }
}
