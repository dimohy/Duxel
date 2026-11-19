using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Duxel.Core;

/// <summary>Pairs an actual immediate-mode callback with its captured source and typed state.</summary>
public abstract class ImmediateSampleSection(string title)
{
    public string Title { get; } = title;
    public abstract string Source { get; }
    public abstract void Render(UiImmediateContext ui);

    public static ImmediateSampleSection Define<T>(string title, Action<UiImmediateContext> render,
        Func<T> read, Func<string> declarations,
        [CallerArgumentExpression(nameof(render))] string expression = "")
        => new Section<T>(title, render, read, declarations, expression);

    public static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";
    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture) + "d";
    public static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Boolean(bool value) => value ? "true" : "false";
    public static string Text(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var character in value)
            text.Append(character switch
            {
                '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture),
                _ => character.ToString()
            });
        return text.Append('"').ToString();
    }
    public static string Array<T>(IEnumerable<T> values, Func<T, string> format)
    {
        var text = new StringBuilder("[");
        var separator = "";
        foreach (var value in values)
        {
            text.Append(separator).Append(format(value));
            separator = ", ";
        }
        return text.Append(']').ToString();
    }

    private sealed class Section<T>(string title, Action<UiImmediateContext> render, Func<T> read,
        Func<string> declarations, string expression) : ImmediateSampleSection(title)
    {
        private bool _initialized;
        private T? _previous;
        private string _source = "";
        private readonly string _renderSource = Normalize(expression);
        public override void Render(UiImmediateContext ui) => render(ui);
        public override string Source
        {
            get
            {
                var current = read();
                if (!_initialized || !EqualityComparer<T>.Default.Equals(_previous!, current))
                {
                    var setup = declarations();
                    _source = (setup.Length == 0 ? "" : setup + "\n\n")
                        + "void Render(UiImmediateContext ui)\n" + _renderSource + "\n\nRender(ui);";
                    _previous = current;
                    _initialized = true;
                }
                return _source;
            }
        }
        private static string Normalize(string expression)
        {
            var arrow = expression.IndexOf("=>", StringComparison.Ordinal);
            if (arrow < 0)
                throw new ArgumentException("Immediate sample source requires an inline block callback.", nameof(expression));
            var body = expression[(arrow + 2)..].Trim();
            if (!body.StartsWith('{') || !body.EndsWith('}'))
                throw new ArgumentException("Immediate sample source requires an inline block callback.", nameof(expression));
            var lines = body.Replace("\r", "", StringComparison.Ordinal).Split('\n');
            var indent = int.MaxValue;
            for (var i = 1; i < lines.Length; i++)
                if (lines[i].Trim().Length != 0)
                    indent = Math.Min(indent, lines[i].Length - lines[i].TrimStart().Length);
            for (var i = 1; i < lines.Length; i++)
                lines[i] = lines[i].Length >= indent ? lines[i][indent..] : "";
            return string.Join("\n", lines);
        }
    }
}
