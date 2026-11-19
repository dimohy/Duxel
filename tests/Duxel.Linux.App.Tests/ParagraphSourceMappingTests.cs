using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Platform.Linux;

internal static class ParagraphSourceMappingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Source-map results require a fresh directory.");
        Directory.CreateDirectory(output);
        var passed = new List<string>();
        var failed = new List<string>();
        void Check(bool valid, string name) => (valid ? passed : failed).Add(name);
        // Hand-authored maps describe UTF16 boundaries after native tab expansion and UTF8 encoding.
        (int[] Map, int[] Bytes, int[] Expected, string Name)[] cases =
        [
            ([0, 4, 7], [0, 1, 2, 3, 4], [0, 0, 0, 0, 1], "tab before object"),
            ([0, 0, 4, 8, 11], [0, 4, 5, 6, 7, 8], [0, 2, 2, 2, 2, 3], "surrogate and tab before object"),
            ([0, 3, 7, 8], [0, 3, 4, 5, 6, 7], [0, 1, 1, 1, 1, 2], "object before tab and text"),
            ([0, 1, 3, 7, 10], [0, 1, 3, 4, 5, 6, 7], [0, 1, 2, 2, 2, 2, 3], "combining sequence and tab before object")
        ];
        foreach (var item in cases)
            Check(item.Bytes.Select(position => LinuxParagraphSourceMap.GlyphPosition(item.Map, position)).SequenceEqual(item.Expected),
                "Native glyph bytes retain their original source character : " + item.Name);
        var shape = LinuxParagraphFontSet.ObjectShape(4, 7, 11.25f, 8, 3.5f);
        Check(shape.Start == 4 && shape.End == 7 && shape.Logical.Width == 11520 && shape.Logical.Height == 8192
            && shape.Logical.Y == -3584, "Shape metrics preserve byte range and fixed-point fractional size/baseline");
        Check(LinuxParagraphFontSet.ObjectShape(0, 3, 0, 0, 5.25f).Logical.Y == -5376,
            "Zero-height objects preserve a baseline outside their rectangle");
        Check(LinuxParagraphFontSet.ObjectShape(0, 3, 1, 4, -2.5f).Logical.Y == 2560,
            "A negative baseline places the native shape below its text baseline");
        var overflow = false;
        try
        {
            _ = LinuxParagraphFontSet.ObjectShape(0, 3, float.MaxValue, 1, 0);
        }
        catch (OverflowException) { overflow = true; }
        Check(overflow, "Unrepresentable native shape metrics fail before allocating a Pango shape");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
            new ParagraphSourceMappingResult(failed.Count == 0, passed.Count, passed.ToArray(), failed.ToArray(),
                "Pure managed source-map/shape conversion; no Pango, X11, accessibility or Ubuntu execution"),
            ParagraphSourceMappingJson.Default.ParagraphSourceMappingResult));
        foreach (var name in passed)
            Console.WriteLine("PASS " + name);
        foreach (var name in failed)
            Console.WriteLine("FAIL " + name);
        if (failed.Count != 0)
            throw new InvalidOperationException("Paragraph source mapping failed : " + failed.Count);
    }
}
internal sealed record ParagraphSourceMappingResult(bool Completed, int Passed, string[] Checks, string[] Failed, string Scope);
[JsonSerializable(typeof(ParagraphSourceMappingResult))]
internal partial class ParagraphSourceMappingJson : JsonSerializerContext;
