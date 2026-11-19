using Duxel.Core;

internal static class MarkdownDocumentTests
{
    internal static void Run(Action<bool, string> check)
    {
        MarkdownStructuralParagraphTests.Run(check);
        MarkdownEntityTests.Run(check);
        const string source = "# title\n\nPlain **bold** and *emphasis*.\n\nSecond paragraph.";
        var document = new UiMarkdownDocument(source);
        var projection = new UiMarkdownTextSource(document);
        projection.Prepare(source, -1);
        check(projection.Projection.Text == "title\n\nPlain bold and emphasis.\n\nSecond paragraph.",
            "Markdown rendered projection hides heading and emphasis delimiters");
        projection.Prepare(source, 3);
        check(projection.Projection.Text.StartsWith("# title\n\nPlain bold and emphasis.", StringComparison.Ordinal),
            "Markdown active heading reveals its source markers while other blocks stay rendered");
        var bodyIndex = source.IndexOf("bold", StringComparison.Ordinal);
        projection.Prepare(source, bodyIndex);
        check(projection.Projection.Text.Contains("Plain **bold** and *emphasis*.", StringComparison.Ordinal)
            && projection.Projection.Text.StartsWith("title", StringComparison.Ordinal),
            "Markdown active paragraph reveals only its own delimiters");
        check(document.ParseCount == 1 && document.Source == source,
            "Markdown active block movement preserves the source and reuses one syntax parse");
        var cached = projection.Projection;
        projection.Prepare(source, bodyIndex + 1);
        check(ReferenceEquals(cached, projection.Projection) && document.ParseCount == 1,
            "Markdown caret movement within one block reuses its display projection");
        var map = projection.Projection.SourceToDisplay;
        check(map.Length == source.Length + 1 && map[0] == 0 && map[^1] == projection.Projection.Text.Length
            && Enumerable.Range(1, map.Length - 1).All(i => map[i] >= map[i - 1]),
            "Markdown projection maps every source boundary monotonically to display text");
        check(projection.Projection.Runs.Any(run => (run.Style.Flags & UiStyledTextFlags.Bold) != 0)
            && projection.Projection.Runs.Any(run => (run.Style.Flags & UiStyledTextFlags.Syntax) != 0),
            "Markdown active source retains semantic bold styling and separate marker styling");
        var updated = source + "\n\n## 새 제목";
        projection.Prepare(updated, -1);
        check(document.ParseCount == 2 && projection.Projection.Text.EndsWith("새 제목", StringComparison.Ordinal),
            "Markdown source revision reparses once and updates the rendered Korean heading");
        var composition = projection.Composition;
        composition.Prepare(updated + "한", updated.Length);
        check(document.Source == updated && document.ParseCount == 2,
            "Markdown composition projection cannot overwrite committed source syntax");
        var empty = new UiMarkdownTextSource(new());
        empty.Prepare(string.Empty, 0);
        check(empty.Projection.Text.Length == 0 && empty.Projection.SourceToDisplay.SequenceEqual(new[] { 0 }),
            "Empty Markdown retains one valid source boundary");
        const string inlineWords = "A **bold** and *italic* paragraph.";
        var wordSource = new UiMarkdownTextSource(new(inlineWords));
        wordSource.Prepare(inlineWords, -1);
        check(wordSource.Projection.Text == "A bold and italic paragraph.",
            "Markdown rendered inline styles preserve surrounding spaces: " + wordSource.Projection.Text);
        foreach (var fenced in new[]
        {
            "\u0060\u0060\u0060csharp\nvar greeting = \"안녕하세요\";\n\u0060\u0060\u0060\n",
            "\u0060\u0060\u0060csharp\nvar greeting = \"안녕하세요\";\n\u0060\u0060\u0060",
            "\u0060\u0060\u0060csharp\r\nvar greeting = \"안녕하세요\";\r\n\u0060\u0060\u0060\r\n",
            "~~~csharp\nvar greeting = \"안녕하세요\";\n~~~\n",
            "```csharp\rvar greeting = \"안녕하세요\";\r```\r"
        })
        {
            var code = new UiMarkdownTextSource(new(fenced));
            code.Prepare(fenced, -1);
            check(code.Projection.Text.TrimEnd('\r', '\n') == "var greeting = \"안녕하세요\";",
                "Markdown inactive fenced code renders only its one content line: " + fenced.Replace("\r", "\\r").Replace("\n", "\\n"));
            code.Prepare(fenced, fenced.IndexOf("var", StringComparison.Ordinal));
            check(code.Projection.Text.Contains(fenced[0] == '~' ? "~~~csharp" : "\u0060\u0060\u0060csharp", StringComparison.Ordinal)
                && code.Projection.Text.TrimEnd('\r', '\n').EndsWith(fenced[0] == '~' ? "~~~" : "\u0060\u0060\u0060", StringComparison.Ordinal),
                "Markdown active code block reveals both fences without changing its source");
        }
        foreach (var (sourceCode, expected) in new[]
        {
            ("````csharp\nvar greeting = 1;\n```", "var greeting = 1;\n```"),
            ("~~~csharp\n```", "```"),
            ("```csharp", ""),
            ("```csharp\n", ""),
            ("```csharp\n```", ""),
            ("````csharp\nvar greeting = 1;\n````", "var greeting = 1;")
        })
        {
            var code = new UiMarkdownTextSource(new(sourceCode));
            code.Prepare(sourceCode, -1);
            check(code.Projection.Text.TrimEnd('\r', '\n') == expected,
                "Markdown hides only parsed fence delimiters and preserves incomplete code content: " + sourceCode.Replace("\n", "\\n")
                + " actual=" + code.Projection.Text.Replace("\n", "\\n"));
            var contentIndex = sourceCode.IndexOf("var", StringComparison.Ordinal);
            code.Prepare(sourceCode, contentIndex >= 0 ? contentIndex : sourceCode.IndexOf("csharp", StringComparison.Ordinal));
            check(code.Projection.Text == sourceCode,
                "Markdown active incomplete code block exposes its complete source without losing info or content");
        }
        const string following = "```csharp\nvar greeting = 1;\n```   \n\nAfter code";
        var followedCode = new UiMarkdownTextSource(new(following));
        followedCode.Prepare(following, following.Length);
        check(followedCode.Projection.Text == "var greeting = 1;\n\nAfter code",
            "Markdown inactive code fences do not insert an extra line or leave trailing delimiter whitespace before the next block");
        const string combined = "***both***";
        var combinedSource = new UiMarkdownTextSource(new(combined));
        combinedSource.Prepare(combined, -1);
        check(combinedSource.Projection.Text == "both" && combinedSource.Projection.Runs.All(run =>
            (run.Style.Flags & (UiStyledTextFlags.Bold | UiStyledTextFlags.Italic)) == (UiStyledTextFlags.Bold | UiStyledTextFlags.Italic)),
            "Markdown triple emphasis combines bold and italic styles without visible markers");
        foreach (var literal in new[] { "~literal~", "^literal^", "==literal==", "++literal++" })
        {
            var literalSource = new UiMarkdownTextSource(new(literal));
            literalSource.Prepare(literal, -1);
            check(literalSource.Projection.Text == literal && literalSource.Projection.Runs.All(run => run.Style.Flags == UiStyledTextFlags.None),
                "Markdown unsupported emphasis extensions remain literal rather than receiving an unrelated style: " + literal);
        }
        foreach (var link in new[] { "https://example.test/path", "hello@example.test" })
        {
            var linkSource = new UiMarkdownTextSource(new("<" + link + ">"));
            linkSource.Prepare(linkSource.Document.Source, -1);
            check(linkSource.Projection.Text == link && linkSource.Projection.Runs.All(run => (run.Style.Flags & UiStyledTextFlags.Link) != 0),
                "Markdown angle autolinks render only the linked label with link styling: " + link);
        }
        const string tasks = "- [ ] 할 일\n- [x] 완료\n- [X] 완료2";
        var taskSource = new UiMarkdownTextSource(new(tasks));
        taskSource.Prepare(tasks, -1);
        check(taskSource.Projection.Text == "\uFFFC 할 일\n\uFFFC 완료\n\uFFFC 완료2",
            "Markdown task lists compile checked and unchecked markers into inline objects without duplicate bullets");
        taskSource.Prepare(tasks, tasks.IndexOf("할", StringComparison.Ordinal));
        check(taskSource.Projection.Text == tasks,
            "Markdown editing a task list exposes its exact original source and delimiters");
    }
}
