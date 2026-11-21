using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MarkdownPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private const string TableExample = """
        # 네이티브 표 셀

        표 앞 문단의 **אב** abc 123에서도 글자 굵기와 양방향 배치를 확인할 수 있습니다.

        | 왼쪽 | 가운데 | 오른쪽 |
        | :--- | :---: | ---: |
        | **אב** abc 123 | بِت لا · English | 한글과 `code` |
        | AV é | 가운데 셀 | 정렬 확인 |

        표 뒤 문단의 **גד** tail에도 같은 조판 방식을 적용합니다.

        각 셀은 내용에 필요한 너비를 유지합니다. 스마트 편집에서 표를 누르면 마크다운 원문이 나타납니다.
        """;
    private const string StructuralExample = """
        # 인용문과 작업 항목

        > abc **אב גד** 123 · 한글과 English

        - [ ] 완료할 작업 · abc אב 123
        - [x] 완료한 작업 · **굵은 글자**와 `code`

        ---

        스마트 편집에서 작업 항목을 누르면 마크다운 원문이 나타납니다.
        """;
    private const string ParagraphExample = """
        # 공통 문단 조판

        한글과 **굵은 글자**, *강조*, `inline code`와 ~~취소선~~을 확인하세요.

        abc **אב גד** 123 · بِت لا · English

        엔티티 원문 &amp; &eacute; &fjlig; → 표시 문자와 커서 경계를 확인하세요.

        [Duxel 문서](https://github.com/dimohy/Duxel)를 열거나 스마트 편집으로 전환해 커서·Shift 선택을 확인하세요.
        """;
    private static readonly string NestedLinkExample = "[처음 링크](https://github.com/dimohy/Duxel)\n\n"
        + string.Concat(Enumerable.Repeat("Tab으로 마지막 링크까지 이동한 뒤 Shift+Tab으로 돌아와 보세요.\n\n", 30))
        + "[마지막 링크](https://github.com/dimohy/Duxel#readme)";
    private readonly UiScrollView _nestedPreview;
    private readonly UiScrollView _nestedInner;
    private const string ImageExample = """
        # 이미지와 SVG

        아래 이미지는 같은 SVG 자원을 재사용합니다.

        ![원·삼각형·사각형 SVG](markdown-image.svg)

        메모리에 있는 이미지도 같은 방식으로 표시합니다.

        ![색상 팔레트](memory:palette)

        ![사용할 수 없는 이미지](missing.svg)

        스마트 편집으로 전환해 이미지를 누르면 원문을 편집할 수 있습니다.
        """;
    private readonly MarkdownImageResources _images;
    private readonly GallerySample<MarkdownSurface> _sample;
    public IReadOnlyList<IGallerySample> Samples { get; }
    private const string Initial = """
        # 한글 Markdown

        본문의 **굵은 글자**와 *강조*, `inline code`를 편집해 보세요.

        ## 편집 위치만 원문 표시

        제목이나 문단을 누르면 해당 블록의 마크다운 기호가 나타납니다.
        나머지 블록에는 마크다운을 적용한 모습이 표시됩니다.

        - 첫 번째 항목
        - 한글 입력과 Ctrl+Z 되돌리기

        > 인용문과 [Duxel](https://github.com/dimohy/Duxel) 링크

        ```csharp
        var greeting = "안녕하세요";
        ```

        ## 작업 목록과 구분선

        - [ ] 검토할 항목
        - [x] 완료한 항목

        ***굵은 기울임***과 ~~취소선~~

        ---

        자동 링크: <https://github.com/dimohy/Duxel>

        ## 표

        | 항목 | 수량 | 상태 |
        | :--- | ---: | :---: |
        | 한글 \| English | 12 | 진행 중 |
        | 완료한 항목 | 2 | 완료 |
        | 보류 | 0 | |

        표를 누르면 마크다운 원문을 편집할 수 있습니다. 넓은 표는 가로로 스크롤해 확인하세요.

        """;
    private readonly UiState<string> _text = new(Initial);
    private readonly UiState<string?> _mode = new("스마트 편집");
    private readonly UiFluentTabList _tabs;
    private readonly UiWidget _toolbar;
    private readonly UiWidget _content;
    UiWidget IObserverNaturalPage.MeasureRoot => _content;
    internal UiMarkdownView View
    {
        get;
    }
    internal UiMarkdownEditor Editor
    {
        get;
    }
    public string Title => "Markdown";
    public string Description => "Preview or edit the same document. Markdown markers such as # and ** appear in the block being edited.";
    internal MarkdownPage()
    {
        var imagesDefinition = GallerySample.Define("images", () => new MarkdownImageResources());
        _images = imagesDefinition.Value;
        var viewDefinition = GallerySample.Define("viewer", () => Dux.MarkdownView("마크다운 미리보기", _text.Binding, new(Height : 580)));
        var viewer = viewDefinition.Value;
        View = viewer;
        var innerDefinition = GallerySample.Define("inner", () => Dux.ScrollView(viewer, new(Width : 480, Height : 280)));
        var inner = innerDefinition.Value;
        _nestedInner = inner;
        var nestedDefinition = GallerySample.Define("nested", () => Dux.ScrollView(inner, new(Width : 520, Height : 160)));
        _nestedPreview = nestedDefinition.Value;
        var editorDefinition = GallerySample.Define("editor", () => Dux.MarkdownEditor("마크다운 스마트 편집", _text.Binding, new(Height : 580)));
        Editor = editorDefinition.Value;
        View.ResolveImage = Editor.ResolveImage = _images.Resolve;
        View.Options = View.Options with { BaseUri = _images.BaseUri };
        Editor.Options = Editor.Options with { BaseUri = _images.BaseUri };
        _sample = GallerySample.Create(() => new MarkdownSurface(this));
        _sample.BindSource(() => (Text : _text.Value, Mode : _mode.Value, Options : SelectedWidget.Options), state =>
        {
            var editing = state.Mode == "스마트 편집";
            var name = editing ? "editor" : "viewer";
            var definition = editing ? editorDefinition.Source : viewDefinition.Source;
            var options = state.Options;
            var source = $"var _text = new UiState<string>({GallerySample.DocumentString(state.Text)});\n\n"
                + imagesDefinition.Source + "\n\n" + definition
                + $"\n{name}.ResolveImage = images.Resolve;\n"
                + $"{name}.Options = new UiMarkdownOptions(Width : {GallerySample.Number(options.Width)}, Height : {GallerySample.Number(options.Height)},\n"
                + $"    MaxLength : {options.MaxLength}, Wrap : {GallerySample.Boolean(options.Wrap)}, ShowBorder : {GallerySample.Boolean(options.ShowBorder)},\n"
                + $"    ReadOnly : {GallerySample.Boolean(options.ReadOnly)}, BaseUri : images.BaseUri);\n\n";
            source += state.Mode == "중첩 미리보기"
                ? innerDefinition.Source + "\n" + nestedDefinition.Source + "\nUiWidget example = nested;"
                : $"UiWidget example = {name};";
            return source + "\n\n" + GalleryPageSource.Declaration(nameof(MarkdownImageResources));
        });
        Samples = [_sample];
        _tabs = Fluent.TabList("마크다운 표시 방식", _mode.Binding,
            new UiTabOption[] { new("스마트 편집", "스마트 편집"), new("미리보기", "미리보기"), new("중첩 미리보기", "중첩 미리보기") }, new(Size: UiTabListSize.Small));
        _toolbar = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                Fluent.Button("예제 복원", () => _text.Value = Initial),
                Fluent.Button("문단 조판 예제", () => ShowExample(ParagraphExample)),
                Fluent.Button("이미지 예제", () => ShowExample(ImageExample)),
                Fluent.Button("구조적 문단 예제", () => ShowExample(StructuralExample)),
                Fluent.Button("표 셀 예제", () => ShowExample(TableExample)),
                Fluent.Button("중첩 링크 예제", () =>
                {
                    _text.Value = NestedLinkExample;
                    _nestedInner.Offset = default;
                    _nestedPreview.Offset = default;
                    _mode.Value = "중첩 미리보기";
                }),
                Fluent.Button("줄바꿈 전환", () =>
                {
                    Editor.Options = Editor.Options with
                    {
                        Wrap = !Editor.Options.Wrap
                    };
                    View.Options = View.Options with
                    {
                        Wrap = Editor.Options.Wrap
                    };
                })),
            Fluent.Caption1("제목이나 문단을 눌러 편집하세요. Tab으로 편집기를 벗어나면 모든 블록의 마크다운 기호가 숨겨집니다. 미리보기의 링크는 클릭하거나 Tab·Shift+Tab으로 선택한 뒤 Enter로 이동합니다."));
        _content = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), _toolbar, _tabs, _sample);
    }
    private void ShowExample(string source)
    {
        _text.Value = source;
        _mode.Value = "미리보기";
    }
    private bool ExampleReady(string source) => _mode.Value == "미리보기" && View.Document.Source == source;
    internal bool ParagraphExampleReady => ExampleReady(ParagraphExample);
    internal bool ImageExampleReady => ExampleReady(ImageExample);
    internal bool StructuralExampleReady => ExampleReady(StructuralExample);
    internal bool TableExampleReady => ExampleReady(TableExample);
    private UiMarkdownWidget SelectedWidget => _mode.Value == "스마트 편집" ? Editor : View;
    private UiWidget SelectedContent => _mode.Value == "중첩 미리보기" ? _nestedPreview
        : _mode.Value == "미리보기" ? View : Editor;
    private sealed class MarkdownSurface(MarkdownPage page) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureConstrained(page.SelectedContent, context.LayoutConstraints);
        protected override void RenderControl(UiImmediateContext ui) => page.SelectedContent.Render(ui);
    }
    public void Render(UiImmediateContext ui)
    {
        _sample.SynchronizeSource();
        _content.Render(ui);
    }
    public void Dispose()
    {
        _content.Dispose();
        _nestedPreview.Dispose();
        Editor.Dispose();
        _images.Dispose();
    }

}
