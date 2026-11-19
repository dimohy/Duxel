using Duxel.Core;

internal static class CodeHighlightTests
{
    internal static void Run(Action<bool, string> check)
    {
        var cases = new[]
        {
            "var s = $\"hello {name}, {value + 12:F2}\";",
            "var s = $@\"{{escaped}} {Get(\"inner\", new { Value = 3 })}\";",
            "var s = @$\"디모이 {name} \"\"quoted\"\"\";",
            "var s = $$\"\"\"literal {x} {{value + 1}}\"\"\";",
            "var s = $\"{(ready ? \"yes\" : \"no\")} {$\"nested {value}\"}\";",
            "var s = $\"{value ?? other} {ready ? 1 : 2:D2}\";",
            "var s = $\"unfinished {Get(\"nested\")",
            "var s = \"plain {name}\\n\"; // {not code}",
            "var s = \"\"; var ch = '\\n'; /* {comment} */"
        };
        foreach (var source in cases)
        {
            var document = new UiCodeDocument();
            document.Prepare(source, 0);
            check(string.Concat(document.Runs.Select(run => run.Display)) == source,
                "syntax highlighting preserves every source character including nested/raw/unfinished strings");
            var previous = document.ParseCount;
            document.Prepare(source, 0);
            check(document.ParseCount == previous, "unchanged interpolation syntax is not reparsed");
        }
        var sample = new UiCodeDocument();
        const string text = "var s = $\"name {Get(value + 12):F2} {{literal}}\\n\";";
        sample.Prepare(text, 0);
        UiCodeToken At(string target) => sample.Runs.Single(run => run.Source.Start <= text.IndexOf(target, StringComparison.Ordinal)
            && run.Source.Start + run.Source.Length > text.IndexOf(target, StringComparison.Ordinal)).Style.CodeToken;
        check(At("name") == UiCodeToken.String && At("{") == UiCodeToken.Interpolation,
            "interpolation delimiters have their own color instead of the string body color");
        check(At("Get") == UiCodeToken.Method && At("12") == UiCodeToken.Number && At("+") == UiCodeToken.Operator,
            "embedded expressions retain method, number and operator colors");
        check(At("F2") == UiCodeToken.String && At("{{") == UiCodeToken.Escape && At("\\n") == UiCodeToken.Escape,
            "format text and escaped braces/backslash escapes are separate lexical runs");
        foreach (var dark in new[] { false, true })
        {
            var palette = UiStyledTextPalette.Resolve(dark, null, new(230, 230, 230), true);
            check(palette.Text(new(CodeToken: UiCodeToken.Interpolation)) != palette.Text(new(CodeToken: UiCodeToken.String)),
                "interpolation and string colors differ in both themes");
        }
    }
}
