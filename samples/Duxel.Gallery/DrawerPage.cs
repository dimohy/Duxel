using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DrawerPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiState<string> _memo = new(GalleryText.Get("Edits are kept when you close and reopen the drawer."));
    private readonly UiState<bool> _inlineOpen = new(true);
    private readonly UiFluentDrawer _overlay;
    private readonly UiFluentDrawer _inline;
    private readonly UiWidget _root;
    private readonly Action<UiImmediateContext> _renderOverlay;
    private int _saves;
    public string Title => "보조 패널";
    public string Description => "Open a panel at the edge of the screen. Change its position or modal behavior, scroll its content, and check that focus returns on close.";
    internal UiFluentDrawer Overlay => _overlay;
    internal int Saves => _saves;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal DrawerPage()
    {
        var contentsDefinition = GallerySample.Define<Func<string, UiWidget>>("contents", () => (string input) => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            [Fluent.Input(input, _memo, 4096, 260),
             .. Enumerable.Range(1, 24).Select(index => Fluent.Body1(string.Format(GalleryText.Get("Drawer content {0}"), index))).ToArray()]));
        var contents = contentsDefinition.Value;
        var overlayOptions = new UiDrawerOptions(Position : UiDrawerPosition.End, CloseLabel : GalleryText.Get("Close side drawer"),
            Description : GalleryText.Get("A side drawer that keeps your edits"));
        var overlayDefinition = GallerySample.Define("_overlay", () =>
        {
            UiFluentDrawer? drawer = null;
            drawer = Fluent.Drawer(GalleryText.Get("Additional tasks"), contents(GalleryText.Get("Drawer note")),
                [Fluent.Button(GalleryText.Get("Save drawer changes"), () => { _saves++; drawer!.Close(); }), Fluent.Button(GalleryText.Get("Close drawer"), () => drawer!.Close())],
                overlayOptions);
            return drawer;
        });
        _overlay = overlayDefinition.Value;
        var renderDefinition = GallerySample.Define<Action<UiImmediateContext>>("renderOverlays", () => (UiImmediateContext ui) => _overlay.Render(ui));
        _renderOverlay = renderDefinition.Value;
        var overlaySample = GallerySample.Create(() => Fluent.Button(GalleryText.Get("Open side drawer"), _overlay.Show));
        var overlayFactory = overlaySample.Source;
        overlaySample.BindSource(() => (Memo : _memo.Value, Saves : _saves, _overlay.Options, _overlay.IsOpen), state =>
            $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\nvar _saves = {state.Saves};\n"
            + $"var overlayOptions = {SourceOptions(state.Options)};\n\n" + contentsDefinition.Source + "\n\n" + overlayDefinition.Source + "\n\n"
            + overlayFactory + (state.IsOpen ? "\n_overlay.Show();" : string.Empty) + "\n\n" + renderDefinition.Source
            + "\n// Invoke renderOverlays(ui) after rendering example in each frame.");
        var inlineOptions = new UiDrawerOptions(Type : UiDrawerType.Inline, InlineCrossExtent : 400, Separator : true, CloseLabel : GalleryText.Get("Close inline drawer"));
        var inlineSample = GallerySample.Create(() =>
        {
            var drawer = Fluent.Drawer(GalleryText.Get("Inline drawer"), contents(GalleryText.Get("Inline drawer note")),
                [Fluent.Button(GalleryText.Get("Close inline content"), () => _inlineOpen.Value = false)], inlineOptions);
            drawer.Open = _inlineOpen.Binding;
            return drawer;
        });
        _inline = inlineSample.Preview;
        var inlineFactory = inlineSample.Source;
        inlineSample.BindSource(() => (Memo : _memo.Value, Open : _inlineOpen.Value, _inline.Options), state =>
            $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\nvar _inlineOpen = new UiState<bool>({GallerySample.Boolean(state.Open)});\n"
            + $"var inlineOptions = {SourceOptions(state.Options)};\n\n" + contentsDefinition.Source + "\n\n" + inlineFactory);
        Samples = [overlaySample, inlineSample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("An overlay opens from the edge of the window. An inline drawer can sit alongside the main content.")),
            Fluent.Subtitle2(GalleryText.Get("Overlay drawer")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button(GalleryText.Get("Change drawer position"), () => _overlay.Options = _overlay.Options with
                    { Position = (UiDrawerPosition)(((int)_overlay.Options.Position + 1) % 3) }),
                Fluent.Button(GalleryText.Get("Change drawer size"), () => _overlay.Options = _overlay.Options with
                    { Size = (UiDrawerSize)(((int)_overlay.Options.Size + 1) % 4) })),
            Fluent.Switch(GalleryText.Get("Modal drawer"), new UiBinding<bool>(() => _overlay.Options.Modal,
                value => _overlay.Options = _overlay.Options with { Modal = value })),
            Fluent.Switch(GalleryText.Get("Right-to-left drawer"), new UiBinding<bool>(() => _overlay.Options.RightToLeft,
                value => _overlay.Options = _overlay.Options with { RightToLeft = value })),
            Fluent.Body1(() => GalleryText.Format("Saves: {0}", _saves)),
            Fluent.Caption1(GalleryText.Get("When the window is tall enough, the title and bottom buttons stay in place as the body scrolls. In a shorter window, the whole drawer can scroll.")),
            overlaySample,
            Fluent.Subtitle2(GalleryText.Get("Inline drawer")),
            Fluent.Switch(GalleryText.Get("Show inline drawer"), _inlineOpen),
            inlineSample));
    }
    private static string SourceOptions(UiDrawerOptions value)
    {
        static string Number(float? number) => number is { } n ? n.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f" : "null";
        return $"new UiDrawerOptions(Type : UiDrawerType.{value.Type}, Position : UiDrawerPosition.{value.Position}, Size : UiDrawerSize.{value.Size},\n"
            + $"    Extent : {Number(value.Extent)}, InlineCrossExtent : {Number(value.InlineCrossExtent)}, Modal : {GallerySample.Boolean(value.Modal)},\n"
            + $"    LightDismiss : {GallerySample.Boolean(value.LightDismiss)}, ShowCloseButton : {GallerySample.Boolean(value.ShowCloseButton)}, CloseLabel : {GallerySample.String(value.CloseLabel)},\n"
            + $"    Description : {GallerySample.String(value.Description)}, RightToLeft : {GallerySample.Boolean(value.RightToLeft)},\n"
            + $"    StickyHeaderFooter : {GallerySample.Boolean(value.StickyHeaderFooter)}, MotionEnabled : {GallerySample.Boolean(value.MotionEnabled)}, Separator : {GallerySample.Boolean(value.Separator)}, ScrollBody : {GallerySample.Boolean(value.ScrollBody)})";
    }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _renderOverlay(ui);
    public void Render(UiImmediateContext ui) { _root.Render(ui); _renderOverlay(ui); }
    public void Dispose() { _overlay.Dispose(); _root.Dispose(); }
}
