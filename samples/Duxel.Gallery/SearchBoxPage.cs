using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SearchBoxPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "검색 입력";
    public string Description => "검색어를 입력하고 지워 보세요. 읽기 전용으로 바꾸거나 값 변경을 거부하도록 설정할 수 있습니다.";
    internal UiFluentSearchBox? Preview => _content?.Preview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private string _query = GalleryText.Get("디모이");
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
            var label = GalleryText.Get("검색어");
            var sample = GallerySample.Create(() => Fluent.SearchBox(label, new UiBinding<string>(() => _query, value => { if (!_reject) _query = value; }),
                options: new(Width: 360, Placeholder: GalleryText.Get("이름이나 검색어를 입력하세요"), ClearLabel: GalleryText.Get("검색어 지우기"))));
            Preview = sample.Preview;
            sample.BindSource(() => (Query: _query, Reject: _reject, Preview.Options),
                state => Source(label, state.Query, state.Reject, state.Options));
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("검색 크기"), () => Preview.Options = Preview.Options with { Size = (UiSearchBoxSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("검색 외관"), () => Preview.Options = Preview.Options with { Appearance = (UiInputAppearance)(((int)Preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button(GalleryText.Get("검색 읽기 전용"), () => Preview.Options = Preview.Options with { ReadOnly = !Preview.Options.ReadOnly }),
                    Fluent.Button(GalleryText.Get("검색 변경 거부"), () => _reject = !_reject),
                    Fluent.Button(GalleryText.Get("검색 방향"), () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("검색어 복원"), () => _query = GalleryText.Get("디모이"))),
                Fluent.Body1(() => GalleryText.Format("검색어 : {0} / 읽기 전용 : {1} / 변경 거부 : {2}", _query,
                    Preview.Options.ReadOnly ? GalleryText.Get("켜짐") : GalleryText.Get("꺼짐"), _reject ? GalleryText.Get("켜짐") : GalleryText.Get("꺼짐"))),
                Fluent.Text(GalleryText.Get("검색창에 포커스가 있으면 지우기 버튼이 표시됩니다. 지운 뒤 바로 다시 입력할 수 있습니다.")),
                Fluent.Text(GalleryText.Get("검색 실행과 결과 필터링은 바인딩으로 연결한 코드에서 구현합니다. 다른 페이지로 이동했다 돌아와도 값은 유지됩니다.")),
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
