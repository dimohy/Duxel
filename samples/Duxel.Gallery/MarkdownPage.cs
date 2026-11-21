using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MarkdownPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private const string TableExample = """
        # Native table cells

        The paragraph before the table also demonstrates bidirectional layout and the weight of **אב** abc 123.

        | Left | Center | Right |
        | :--- | :---: | ---: |
        | **אב** abc 123 | بِت لا · English | Text and `code` |
        | AV é | Center cell | Check alignment |

        The paragraph after the table uses the same typesetting for **גד** tail.

        Each cell keeps the width it needs. Select the table in Smart edit to reveal its Markdown source.
        """;
    private const string StructuralExample = """
        # Quotes and tasks

        > abc **אב גד** 123 · text and English

        - [ ] Pending task · abc אב 123
        - [x] Completed task · **bold** and `code`

        ---

        Select a task in Smart edit to reveal its Markdown source.
        """;
    private const string ParagraphExample = """
        # Paragraph typesetting

        Try **bold**, *emphasis*, `inline code`, and ~~strikethrough~~.

        abc **אב גד** 123 · بِت لا · English

        Entity source &amp; &eacute; &fjlig; → inspect the displayed characters and caret positions.

        Open the [Duxel documentation](https://github.com/dimohy/Duxel), or switch to Smart edit to try the caret and Shift selection.
        """;
    private static string NestedLinkExample() =>
        GalleryText.Get("[First link](https://github.com/dimohy/Duxel)") + "\n\n"
        + string.Concat(Enumerable.Repeat(GalleryText.Get("Press Tab to reach the last link, then Shift+Tab to return.") + "\n\n", 30))
        + GalleryText.Get("[Last link](https://github.com/dimohy/Duxel#readme)");
    private readonly UiScrollView _nestedPreview;
    private readonly UiScrollView _nestedInner;
    private const string ImageExample = """
        # Images and SVG

        The image below reuses the same SVG resource.

        ![SVG showing a circle, triangle, and square](markdown-image.svg)

        Images in memory are displayed the same way.

        ![Color palette](memory:palette)

        ![Unavailable image](missing.svg)

        Switch to Smart edit and select an image to edit its source.
        """;
    private readonly MarkdownImageResources _images;
    private readonly GallerySample<MarkdownSurface> _sample;
    public IReadOnlyList<IGallerySample> Samples { get; }
    private const string Initial = """
        # Markdown editing

        Try editing the **bold**, *emphasis*, and `inline code` in this paragraph.

        ## Show the source of the active block

        Select a heading or paragraph to show the Markdown markers for that block.
        Other blocks continue to show the formatted result.

        - First item
        - Type some text and press Ctrl+Z to undo.

        > A quote with a [Duxel](https://github.com/dimohy/Duxel) link

        ```csharp
        var greeting = "Hello";
        ```

        ## Task list and separator

        - [ ] To review
        - [x] Reviewed

        ***Bold italic*** and ~~strikethrough~~

        ---

        Automatic link: <https://github.com/dimohy/Duxel>

        ## Table

        | Item | Quantity | Status |
        | :--- | ---: | :---: |
        | Text \| English | 12 | In progress |
        | Completed items | 2 | Done |
        | On hold | 0 | |

        Select the table to edit the Markdown source. Scroll horizontally to inspect wide tables.

        """;
    private const string EditMode = "edit";
    private const string PreviewMode = "preview";
    private const string NestedMode = "nested";
    private readonly UiState<string> _text;
    private readonly UiState<string?> _mode = new(EditMode);
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
        var initial = LocalizeDocument(Initial);
        _text = new(initial);
        var imagesDefinition = GallerySample.Define("images", () => new MarkdownImageResources());
        _images = imagesDefinition.Value;
        var viewDefinition = GallerySample.Define("viewer", () => Dux.MarkdownView(GalleryText.Get("Markdown preview"), _text.Binding, new(Height : 580)));
        var viewer = viewDefinition.Value;
        View = viewer;
        var innerDefinition = GallerySample.Define("inner", () => Dux.ScrollView(viewer, new(Width : 480, Height : 280)));
        var inner = innerDefinition.Value;
        _nestedInner = inner;
        var nestedDefinition = GallerySample.Define("nested", () => Dux.ScrollView(inner, new(Width : 520, Height : 160)));
        _nestedPreview = nestedDefinition.Value;
        var editorDefinition = GallerySample.Define("editor", () => Dux.MarkdownEditor(GalleryText.Get("Markdown smart edit"), _text.Binding, new(Height : 580)));
        Editor = editorDefinition.Value;
        View.ResolveImage = Editor.ResolveImage = _images.Resolve;
        View.Options = View.Options with { BaseUri = _images.BaseUri };
        Editor.Options = Editor.Options with { BaseUri = _images.BaseUri };
        _sample = GallerySample.Create(() => new MarkdownSurface(this));
        _sample.BindSource(() => (Text : _text.Value, Mode : _mode.Value, Options : SelectedWidget.Options), state =>
        {
            var editing = state.Mode == EditMode;
            var name = editing ? "editor" : "viewer";
            var definition = editing ? editorDefinition.Source : viewDefinition.Source;
            var options = state.Options;
            var source = $"var _text = new UiState<string>({GallerySample.DocumentString(state.Text)});\n\n"
                + imagesDefinition.Source + "\n\n" + definition
                + $"\n{name}.ResolveImage = images.Resolve;\n"
                + $"{name}.Options = new UiMarkdownOptions(Width : {GallerySample.Number(options.Width)}, Height : {GallerySample.Number(options.Height)},\n"
                + $"    MaxLength : {options.MaxLength}, Wrap : {GallerySample.Boolean(options.Wrap)}, ShowBorder : {GallerySample.Boolean(options.ShowBorder)},\n"
                + $"    ReadOnly : {GallerySample.Boolean(options.ReadOnly)}, BaseUri : images.BaseUri);\n\n";
            source += state.Mode == NestedMode
                ? innerDefinition.Source + "\n" + nestedDefinition.Source + "\nUiWidget example = nested;"
                : $"UiWidget example = {name};";
            return source + "\n\n" + GalleryPageSource.Declaration(nameof(MarkdownImageResources));
        });
        Samples = [_sample];
        _tabs = Fluent.TabList(GalleryText.Get("Markdown display mode"), _mode.Binding,
            new UiTabOption[] { new(EditMode, GalleryText.Get("Smart edit")), new(PreviewMode, GalleryText.Get("Preview")), new(NestedMode, GalleryText.Get("Nested preview")) }, new(Size: UiTabListSize.Small));
        _toolbar = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                Fluent.Button(GalleryText.Get("Restore example"), () => _text.Value = initial),
                Fluent.Button(GalleryText.Get("Paragraph typesetting example"), () => ShowExample(ParagraphExample)),
                Fluent.Button(GalleryText.Get("Image example"), () => ShowExample(ImageExample)),
                Fluent.Button(GalleryText.Get("Structured paragraph example"), () => ShowExample(StructuralExample)),
                Fluent.Button(GalleryText.Get("Table cell example"), () => ShowExample(TableExample)),
                Fluent.Button(GalleryText.Get("Nested link example"), () =>
                {
                    _text.Value = NestedLinkExample();
                    _nestedInner.Offset = default;
                    _nestedPreview.Offset = default;
                    _mode.Value = NestedMode;
                }),
                Fluent.Button(GalleryText.Get("Toggle line wrapping"), () =>
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
            Fluent.Caption1(GalleryText.Get("Select a heading or paragraph to edit it. Tab leaves the editor and hides the source markers. In Preview, click a link or move to it with Tab or Shift+Tab and press Enter.")));
        _content = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), _toolbar, _tabs, _sample);
    }
    private static string LocalizeDocument(string english) =>
        string.Join("\n", english.ReplaceLineEndings("\n").Split('\n').Select(LocalizeDocumentLine));

    private static string LocalizeDocumentLine(string line)
    {
        var markerLength = line.StartsWith("## ", StringComparison.Ordinal) ? 3
            : line.StartsWith("# ", StringComparison.Ordinal) ? 2 : 0;
        return line[..markerLength] + GalleryText.Get(line[markerLength..]);
    }

    private void ShowExample(string source)
    {
        _text.Value = LocalizeDocument(source);
        _mode.Value = PreviewMode;
    }
    private bool ExampleReady(string source) => _mode.Value == PreviewMode && View.Document.Source == LocalizeDocument(source);
    internal bool ParagraphExampleReady => ExampleReady(ParagraphExample);
    internal bool ImageExampleReady => ExampleReady(ImageExample);
    internal bool StructuralExampleReady => ExampleReady(StructuralExample);
    internal bool TableExampleReady => ExampleReady(TableExample);
    private UiMarkdownWidget SelectedWidget => _mode.Value == EditMode ? Editor : View;
    private UiWidget SelectedContent => _mode.Value == NestedMode ? _nestedPreview
        : _mode.Value == PreviewMode ? View : Editor;
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
