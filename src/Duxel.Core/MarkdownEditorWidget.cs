namespace Duxel.Core;

public sealed class MarkdownEditorWidget(string id, string? label = null) : IUiCustomWidget
{
    private string _text = string.Empty;

    public string Id { get; } = ValidateId(id);

    public string Label { get; set; } = string.IsNullOrWhiteSpace(label) ? "Markdown" : label;

    public string Text
    {
        get => _text;
        set => _text = NormalizeLineEndings(value);
    }

    public int MaxLength { get; set; } = 16_384;

    public float Height { get; set; } = 320f;

    public bool ShowStats { get; set; } = true;

    public bool LastChanged { get; private set; }

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        var value = Text;
        LastChanged = ui.InputTextMultiline($"##{Id}", ref value, MaxLength, Height);
        if (LastChanged)
        {
            Text = value;
        }

        if (ShowStats)
        {
            ui.TextDisabled($"Lines: {CountLines(Text)}  Chars: {Text.Length}");
        }
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string NormalizeLineEndings(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id;
    }
}