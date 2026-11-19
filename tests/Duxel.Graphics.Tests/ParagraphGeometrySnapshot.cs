using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ParagraphGeometrySnapshot
{
    internal static void Write(string output)
    {
        Directory.CreateDirectory(output);
        using var stream = File.Create(Path.Combine(output, "geometry.json"));
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartArray();
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var source in new[]
        {
            "var greeting = $\"디모이 {name}, {value:F2}\";\nreturn greeting;",
            "office ffi fi AV e\u0301x 한글 abc אבג العربية",
            "\tIndented\tcode\r\n가나다\n", "a\u200db\u200bc\u2060d", "", "אבג abc 123 سلام"
        })
        foreach (var width in new[] { 90f, 420f, float.PositiveInfinity })
        {
            var paragraph = WindowsPlatformTextBackend.Instance.CreateParagraph(new(source,
                [new(0, source.Length, Path.Combine(fonts, "segoeui.ttf"), Path.Combine(fonts, "malgun.ttf"), 16)], width), false);
            json.WriteStartObject();
            json.WriteString("text", source);
            json.WriteNumber("width", float.IsFinite(width) ? width : -1);
            json.WriteStartArray("carets");
            foreach (var caret in paragraph.Carets.Span)
            {
                json.WriteStartArray(); json.WriteNumberValue(caret.X); json.WriteNumberValue(caret.Y); json.WriteEndArray();
            }
            json.WriteEndArray();
            json.WriteStartArray("lines");
            foreach (var line in paragraph.Lines.Span)
            {
                json.WriteStartObject();
                json.WriteNumber("start", line.Start); json.WriteNumber("length", line.Length);
                json.WriteNumber("advance", line.Advance); json.WriteNumber("visibleWidth", line.WidthWithoutTrailingWhitespace);
                json.WriteStartArray("selections");
                for (var start = 0; start <= line.Length; start++)
                for (var end = start; end <= line.Length; end++)
                {
                    json.WriteStartArray();
                    foreach (var fragment in line.Geometry.SelectionFragments(start, end))
                    {
                        json.WriteNumberValue(fragment.Left); json.WriteNumberValue(fragment.Width);
                    }
                    json.WriteEndArray();
                }
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }
}
