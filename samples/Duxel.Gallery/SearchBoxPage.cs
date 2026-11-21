using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SearchBoxPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "검색 입력";
    public string Description => "Enter and clear a search query. Try read-only mode or reject changes to the value.";
    internal UiFluentSearchBox? Preview => _content?.Preview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private string _query = GalleryText.Get("Dimoi");
        private bool _reject;
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiFluentSearchBox Preview
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal Content()
        {
            var label = GalleryText.Get("Search query");
            var sample = GallerySample.Create(() => Fluent.SearchBox(label, new UiBinding<string>(() => _query, value => { if (!_reject) _query = value; }),
                options: new(Width: 360, Placeholder: GalleryText.Get("Enter a name or search term"), ClearLabel: GalleryText.Get("Clear search query"))));
            Preview = sample.Preview;
            sample.BindSource(() => (Query: _query, Reject: _reject, Preview.Options),
                state => Source(label, state.Query, state.Reject, state.Options));
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change search box size"), () => Preview.Options = Preview.Options with { Size = (UiSearchBoxSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Change search box appearance"), () => Preview.Options = Preview.Options with { Appearance = (UiInputAppearance)(((int)Preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button(GalleryText.Get("Toggle read-only search"), () => Preview.Options = Preview.Options with { ReadOnly = !Preview.Options.ReadOnly }),
                    Fluent.Button(GalleryText.Get("Toggle rejection of edits"), () => _reject = !_reject),
                    Fluent.Button(GalleryText.Get("Reverse search box direction"), () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("Reset search query"), () => _query = GalleryText.Get("Dimoi"))),
                Fluent.Body1(() => GalleryText.Format("Query: {0} / Read-only: {1} / Reject edits: {2}", _query,
                    Preview.Options.ReadOnly ? GalleryText.Get("On") : GalleryText.Get("Off"), _reject ? GalleryText.Get("On") : GalleryText.Get("Off"))),
                Fluent.Text(GalleryText.Get("The clear button appears when the search box has focus. You can start typing again immediately after clearing it.")),
                Fluent.Text(GalleryText.Get("The code bound to the search box runs the search and filters the results. The value is preserved when you leave the page and return.")),
                Fluent.Label(label, Preview), sample));
        }
        private static string Source(string label, string query, bool reject, UiSearchBoxOptions options)
            => FormattableString.Invariant($$"""
                var query = {{GallerySample.String(query)}};
                var rejectChanges = {{GallerySample.Boolean(reject)}};

                UiWidget example = Fluent.SearchBox({{GallerySample.String(label)}}, new UiBinding<string>(
                    () => query,
                    value => { if (!rejectChanges) query = value; }),
                    options: new(
                        Width: {{options.Width}}f,
                        Size: UiSearchBoxSize.{{options.Size}},
                        Appearance: UiInputAppearance.{{options.Appearance}},
                        Placeholder: {{GallerySample.String(options.Placeholder)}},
                        ReadOnly: {{GallerySample.Boolean(options.ReadOnly)}},
                        Invalid: {{GallerySample.Boolean(options.Invalid)}},
                        RightToLeft: {{GallerySample.Boolean(options.RightToLeft)}},
                        ClearLabel: {{GallerySample.String(options.ClearLabel)}}));
                """);
        public void Dispose() => Root.Dispose();
    }
}
