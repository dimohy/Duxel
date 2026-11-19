using Duxel.Core;

internal static unsafe partial class StyledParagraphTests
{
    private static void RunRequestContracts(Action<bool, string> check)
    {
        PlatformTextParagraphStyle[] styles = [new(0, 1, "explicit.ttf", null, 14)];
        var request = new PlatformTextParagraphRequest("A", styles, 0, PlatformTextParagraphDirection.RightToLeft);
        styles[0] = styles[0] with
        {
            FontPath = "changed.ttf"
        };
        check(request.Styles.Span[0].FontPath == "explicit.ttf" && request.Width == 0
            && request.Direction == PlatformTextParagraphDirection.RightToLeft,
            "Paragraph request owns typography and preserves explicit zero width and base direction");
        PlatformTextParagraphObject[] objects = [new(0, 0, 12, 0)];
        var atomic = new PlatformTextParagraphRequest("\uFFFC", [new(0, 1, "font.ttf", null, 14)], objects: objects);
        objects[0] = new(0, 99, 12, 12);
        check(atomic.Objects.Span[0] == new PlatformTextParagraphObject(0, 0, 12, 0),
            "Paragraph request owns atomic metrics and preserves zero width and top baseline");
        var invalidObjects = new PlatformTextParagraphObject[][]
        {
            [new(-1, 1, 1, 1)], [new(2, 1, 1, 1)], [new(0, -1, 1, 1)],
            [new(0, float.NaN, 1, 1)], [new(0, 1, float.PositiveInfinity, 1)],
            [new(0, 1, -1, 0)], [new(0, 1, 1, float.NegativeInfinity)], [new(0, 1, 1, float.PositiveInfinity)],
            [new(0, 1, 1, float.NaN)], [new(0, 1, 1, 1), new(0, 2, 2, 2)],
            [new(1, 1, 1, 1), new(0, 1, 1, 1)]
        };
        for (var index = 0; index < invalidObjects.Length; index++)
        {
            var rejected = false;
            try
            {
                _ = new PlatformTextParagraphRequest("\uFFFC\uFFFC", [new(0, 2, "font.ttf", null, 14)], objects: invalidObjects[index]);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Paragraph rejects invalid atomic geometry or source ordering before native execution : " + index);
        }
        var replacedTextRejected = false;
        try
        {
            _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, 14)], objects: [new(0, 1, 1, 1)]);
        }
        catch (ArgumentException) { replacedTextRejected = true; }
        check(replacedTextRejected, "Atomic paragraph metrics cannot silently suppress an ordinary source character");
        var invalid = new (string Name, Action Create)[]
        {
            ("missing styles", () => _ = new PlatformTextParagraphRequest("A", [])),
            ("source gap", () => _ = new PlatformTextParagraphRequest("A", [new(1, 1, "font.ttf", null, 14)])),
            ("missing coverage", () => _ = new PlatformTextParagraphRequest("AB", [new(0, 1, "font.ttf", null, 14)])),
            ("overlap", () => _ = new PlatformTextParagraphRequest("AB", [new(0, 1, "font.ttf", null, 14), new(0, 1, "font.ttf", null, 14)])),
            ("invalid UTF16", () => _ = new PlatformTextParagraphRequest("\uD800", [new(0, 1, "font.ttf", null, 14)])),
            ("split surrogate", () => _ = new PlatformTextParagraphRequest("😀", [new(0, 1, "font.ttf", null, 14), new(1, 1, "font.ttf", null, 14)])),
            ("empty font", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, " ", null, 14)])),
            ("empty secondary font", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", " ", 14)])),
            ("zero size", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, 0)])),
            ("infinite size", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, float.PositiveInfinity)])),
            ("negative width", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, 14)], -1)),
            ("NaN width", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, 14)], float.NaN)),
            ("invalid direction", () => _ = new PlatformTextParagraphRequest("A", [new(0, 1, "font.ttf", null, 14)], direction: (PlatformTextParagraphDirection)2))
        };
        foreach (var item in invalid)
        {
            var rejected = false;
            try
            {
                item.Create();
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Paragraph request rejects " + item.Name + " before native execution");
        }
        PlatformTextParagraphGlyph[] glyphs = [new(1, 0, 7, 0, 0, 0, 10)];
        var run = new PlatformTextParagraphRun(0, "font.ttf", 14, 1, new(7, 10), glyphs);
        glyphs[0] = glyphs[0] with
        {
            X = 99
        };
        check(run.Glyphs.Span[0].X == 0 && run.BidiLevel == 1 && run.Origin == new UiVector2(7, 10),
            "Paragraph run owns native glyphs and preserves direction and fractional-origin contract");
    }
}
