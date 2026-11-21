using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToolbarPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "도구 모음";
    public string Description => "Try horizontal and vertical toolbars, arrow-key navigation, and toggled states.";
    internal UiFluentToolbar? Preview => _content?.Toolbar;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private int _count;
        private readonly UiState<bool> _bold = new(false);
        internal UiFluentToolbar Toolbar
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            var toolbarOptions = new UiToolbarOptions();
            var sample = GallerySample.Create(() =>
            {
                static UiWidget Action(string text, Action activated) => new UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>>(text, activated);
                return Fluent.Toolbar(GalleryText.Get("Document tools"), [Action(GalleryText.Get("New document"), () => _count++), Action(GalleryText.Get("Save"), () => _count++),
                    Fluent.ToggleButton(GalleryText.Get("Bold"), _bold.Binding), Action(GalleryText.Get("Print"), () => _count++)], toolbarOptions);
            });
            Toolbar = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (Count : _count, Bold : _bold.Value, Toolbar.Options), state =>
                $"var _count = {state.Count};\nvar _bold = new UiState<bool>({GallerySample.Boolean(state.Bold)});\n"
                + $"var toolbarOptions = new UiToolbarOptions(Size : UiToolbarSize.{state.Options.Size}, Vertical : {GallerySample.Boolean(state.Options.Vertical)}, RightToLeft : {GallerySample.Boolean(state.Options.RightToLeft)});\n\n" + factory);
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change toolbar orientation"), () => Toolbar.Options = Toolbar.Options with { Vertical = !Toolbar.Options.Vertical }),
                    Fluent.Button(GalleryText.Get("Change toolbar size"), () => Toolbar.Options = Toolbar.Options with { Size = (UiToolbarSize)(((int)Toolbar.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Reverse toolbar direction"), () => Toolbar.Options = Toolbar.Options with { RightToLeft = !Toolbar.Options.RightToLeft })),
                Fluent.Button(GalleryText.Get("Run next toolbar action"), () => _count++),
                Fluent.Body1(() => GalleryText.Format("Actions: {0} / Bold: {1} / Orientation: {2} / Size: {3}", _count,
                    GalleryText.Get(_bold.Value ? "On" : "Off"),
                    GalleryText.Get(Toolbar.Options.Vertical ? "Vertical" : "Horizontal"),
                    GalleryText.Get(Toolbar.Options.Size switch
                    {
                        UiToolbarSize.Small => "Small",
                        UiToolbarSize.Large => "Large",
                        _ => "Medium"
                    }))),
                Fluent.Text(GalleryText.Get("Press Tab to enter the toolbar, then use the arrow keys or Home and End to move between commands. Press Tab again to go to the next control.")),
                Fluent.Text(GalleryText.Get("Press Enter or Space to activate a button or toggle its state. The toolbar keeps its state when you return to this page.")),
                sample));
        }
        public void Dispose() => Root.Dispose();
    }
}
