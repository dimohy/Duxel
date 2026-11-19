using Duxel.Core;

// Owned native test surface; the ordinary sample and Observer remain unchanged.
internal sealed class MarkdownProbeScreen : UiScreen, IDisposable
{
    private string _source = "# 정😀\n\n본문 **bold** and `code`";
    private string _unwrapped = "정😀\n\nend\n";
    private string _single = "정😀";
    private readonly UiMarkdownEditor _editor;
    private readonly UiMarkdownView _viewer;
    internal ManualResetEventSlim FirstFrame { get; } = new();

    internal MarkdownProbeScreen()
    {
        _editor = new("Markdown editor", new(() => _source, value => _source = value), new(Width: 600, Height: 210, MaxLength: 512));
        _viewer = new("Markdown viewer", new(() => "| Left | Right |\n| --- | --- |\n| A | B |",
            _ => throw new InvalidOperationException("The owned Markdown viewer is read-only.")), new(Width: 280, Height: 210));
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.SetAccessibilityFontNameLanguage("en");
        ui.SetCursorScreenPos(new(10, 10));
        _editor.Render(ui);
        ui.SetCursorScreenPos(new(10, 250));
        _viewer.Render(ui);
        ui.SetCursorScreenPos(new(320, 250));
        ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
            "Unwrapped editor", ref _unwrapped, 4096, new() { Wrap = false }, 300, 210);
        ui.SetCursorScreenPos(new(10, 480));
        ui.InputText<UiFluentTextInputBehavior, UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentOutlineInput>>(
            "Single-line editor", ref _single, 4096, 300);
        FirstFrame.Set();
    }

    public void Dispose()
    {
        _viewer.Dispose();
        _editor.Dispose();
        FirstFrame.Dispose();
    }
}
