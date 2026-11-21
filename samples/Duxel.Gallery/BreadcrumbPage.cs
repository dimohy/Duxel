using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class BreadcrumbPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "경로";
    public string Description => "Show the path to the current document. Open hidden ancestors, navigate with the keyboard, or hover over a truncated name to see it in full.";
    internal UiFluentBreadcrumb? Preview => _content?.Preview;
    internal string LastNavigation => _content?.LastNavigation ?? string.Empty;
    internal int Navigations => _content?.Navigations ?? 0;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal string LastNavigation = GalleryText.Get("No path selected.");
        internal int Navigations;
        internal UiFluentBreadcrumb Preview
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal Content()
        {
            var caption = LastNavigation;
            var options = new UiBreadcrumbOptions(MaximumCharacters: 12, OverflowLabel: GalleryText.Get("Hidden path segments"), CurrentLabel: GalleryText.Get("Current location"));
            var maximumWidth = 520f;
            var sample = GallerySample.Create(() =>
            {
                UiBreadcrumbOption[] items = [new("home", GalleryText.Get("Home")), new("workspace", GalleryText.Get("Workspace")),
                    new("category", GalleryText.Get("Document archive"), false), new("team", GalleryText.Get("Collaborating team")),
                    new("project", GalleryText.Get("Dimoi’s project with a deliberately long name")), new("current", GalleryText.Get("Current document"))];
                var names = items.ToDictionary(item => item.Value, item => item.Label, StringComparer.Ordinal);
                return Fluent.Breadcrumb(GalleryText.Get("Project path"), items,
                    value => { LastNavigation = value; caption = names[value]; Navigations++; }, options);
            }, layout: preview => Dux.ConstrainedBox(preview, new(0, maximumWidth, 0, float.PositiveInfinity)));
            Preview = sample.Preview;
            var viewport = (UiConstrainedBox)sample.PreviewLayout;
            var factory = sample.Source;
            sample.BindSource(() => (Location: LastNavigation, Count: Navigations, Caption: caption, Preview.Options, Width: viewport.Options.MaxWidth), state =>
                FormattableString.Invariant($$"""
                    var LastNavigation = {{GallerySample.String(state.Location)}};
                    var Navigations = {{state.Count}};
                    var caption = {{GallerySample.String(state.Caption)}};
                    var maximumWidth = {{state.Width}}f;
                    var options = new UiBreadcrumbOptions(
                        Size: UiBreadcrumbSize.{{state.Options.Size}},
                        FocusMode: UiBreadcrumbFocusMode.{{state.Options.FocusMode}},
                        RightToLeft: {{GallerySample.Boolean(state.Options.RightToLeft)}},
                        MaximumCharacters: {{state.Options.MaximumCharacters}},
                        TooltipMaximumCharacters: {{state.Options.TooltipMaximumCharacters}},
                        OverflowLabel: {{GallerySample.String(state.Options.OverflowLabel)}},
                        CurrentLabel: {{GallerySample.String(state.Options.CurrentLabel)}});

                    """) + factory);
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change path width"), () => viewport.Options = viewport.Options with
                    {
                        MaxWidth = viewport.Options.MaxWidth == 520 ? 280 : viewport.Options.MaxWidth == 280 ? 80 : 520
                    }),
                    Fluent.Button(GalleryText.Get("Change keyboard navigation"), () => Preview.Options = Preview.Options with
                    {
                        FocusMode = Preview.Options.FocusMode == UiBreadcrumbFocusMode.Tab ? UiBreadcrumbFocusMode.Arrow : UiBreadcrumbFocusMode.Tab
                    }),
                    Fluent.Button(GalleryText.Get("Reverse path direction"), () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("Change path size"), () => Preview.Options = Preview.Options with
                    {
                        Size = (UiBreadcrumbSize)(((int)Preview.Options.Size + 1) % 3)
                    })),
                Fluent.Body1(() => GalleryText.Format("Destination: {0} / Activations: {1}", caption, Navigations)),
                Fluent.Body1(() => GalleryText.Format("Keyboard: {0} / Direction: {1}",
                    Preview.Options.FocusMode == UiBreadcrumbFocusMode.Tab ? GalleryText.Get("Tab navigation") : GalleryText.Get("Arrow-key navigation"),
                    Preview.Options.RightToLeft ? GalleryText.Get("Right to left") : GalleryText.Get("Left to right"))),
                Fluent.Text(GalleryText.Get("As the width shrinks, segments starting with the second move into the overflow menu. At the narrowest width, only the current location is shown.")),
                Fluent.Text(GalleryText.Get("The document archive is unavailable, but you can focus it with Tab or the arrow keys to read its description. Activating the current location keeps you on the same page.")),
                Fluent.Text(GalleryText.Get("Hover over or focus a long name to show a tooltip. Switching pages preserves the settings and navigation history.")),
                sample));
            Root.AccessibleName = GalleryText.Get("Breadcrumb example");
        }
        public void Dispose() => Root.Dispose();
    }
}
