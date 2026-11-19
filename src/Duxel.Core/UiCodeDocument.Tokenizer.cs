namespace Duxel.Core;

internal sealed partial class UiCodeDocument
{
    private readonly record struct TokenSpan(int Start, int Length, UiCodeToken Kind);

    // Lexical colors preserve the original UTF-16 buffer; no compiler or runtime
    // semantic model is required for a read-only source example.
    private sealed class Tokenizer(string source)
    {
        private readonly List<TokenSpan> _spans = [];
        private int _at;

        internal List<TokenSpan> Scan()
        {
            while (_at < source.Length)
                Token();
            return _spans;
        }

        private void Add(int start, UiCodeToken kind)
        {
            if (_at <= start)
                return;
            if (_spans.Count > 0 && _spans[^1].Kind == kind && _spans[^1].Start + _spans[^1].Length == start)
                _spans[^1] = _spans[^1] with { Length = _at - _spans[^1].Start };
            else
                _spans.Add(new(start, _at - start, kind));
        }

        private int Repeated(char value, int start)
        {
            var end = start;
            while (end < source.Length && source[end] == value)
                end++;
            return end - start;
        }

        private void Token()
        {
            var start = _at;
            var c = source[_at];
            if (TryString())
                return;
            _at++;
            var kind = UiCodeToken.Plain;
            if (c == '/' && _at < source.Length && source[_at] is '/' or '*')
            {
                kind = UiCodeToken.Comment;
                if (source[_at++] == '/')
                {
                    while (_at < source.Length && source[_at] != '\n')
                        _at++;
                }
                else
                {
                    while (_at < source.Length && !(source[_at - 1] == '*' && source[_at] == '/'))
                        _at++;
                    if (_at < source.Length)
                        _at++;
                }
            }
            else if (c == '#')
            {
                kind = UiCodeToken.Directive;
                while (_at < source.Length && source[_at] != '\n')
                    _at++;
            }
            else if (char.IsDigit(c))
            {
                kind = UiCodeToken.Number;
                while (_at < source.Length && (char.IsLetterOrDigit(source[_at]) || source[_at] is '.' or '_'))
                    _at++;
            }
            else if (char.IsLetter(c) || c == '_' || c == '@' && _at < source.Length && char.IsLetter(source[_at]))
            {
                while (_at < source.Length && (char.IsLetterOrDigit(source[_at]) || source[_at] == '_'))
                    _at++;
                var word = source.Substring(start, _at - start);
                var next = _at;
                while (next < source.Length && char.IsWhiteSpace(source[next]))
                    next++;
                kind = Keywords.Contains(word) ? UiCodeToken.Keyword
                    : next < source.Length && source[next] == '(' ? UiCodeToken.Method
                    : char.IsUpper(c) ? UiCodeToken.Type : UiCodeToken.Plain;
            }
            else if ("+-*/%=!<>?&|^~".Contains(c))
                kind = UiCodeToken.Operator;
            else if (!char.IsWhiteSpace(c))
                kind = UiCodeToken.Punctuation;
            Add(start, kind);
        }

        private bool TryString()
        {
            var start = _at;
            var cursor = start;
            var dollars = 0;
            var verbatim = false;
            while (cursor < source.Length && source[cursor] is '$' or '@')
            {
                if (source[cursor] == '$')
                    dollars++;
                else
                    verbatim = true;
                cursor++;
            }
            if (cursor >= source.Length || source[cursor] is not ('"' or '\''))
                return false;
            var quote = source[cursor];
            if (quote == '\'' && cursor != start)
                return false;
            var quotes = quote == '"' ? Repeated('"', cursor) : 1;
            var raw = quotes >= 3;
            _at = cursor + (raw ? quotes : 1);
            var segment = start;
            while (_at < source.Length)
            {
                if (source[_at] == quote && (!raw || Repeated(quote, _at) >= quotes))
                {
                    if (!raw && verbatim && _at + 1 < source.Length && source[_at + 1] == quote)
                    {
                        Add(segment, UiCodeToken.String);
                        var escape = _at;
                        _at += 2;
                        Add(escape, UiCodeToken.Escape);
                        segment = _at;
                        continue;
                    }
                    _at += raw ? quotes : 1;
                    Add(segment, UiCodeToken.String);
                    return true;
                }
                if (!raw && !verbatim && source[_at] == '\\')
                {
                    Add(segment, UiCodeToken.String);
                    var escape = _at++;
                    if (_at < source.Length)
                    {
                        var length = source[_at] == 'u' ? 5 : source[_at] == 'U' ? 9 : 1;
                        _at = Math.Min(source.Length, _at + length);
                    }
                    Add(escape, UiCodeToken.Escape);
                    segment = _at;
                    continue;
                }
                if (dollars > 0 && source[_at] == '{')
                {
                    var braces = Repeated('{', _at);
                    if (!raw && braces >= 2)
                    {
                        Add(segment, UiCodeToken.String);
                        var escape = _at;
                        _at += 2;
                        Add(escape, UiCodeToken.Escape);
                        segment = _at;
                        continue;
                    }
                    var delimiter = raw ? dollars : 1;
                    if (braces >= delimiter)
                    {
                        _at += raw ? braces - delimiter : 0;
                        Add(segment, UiCodeToken.String);
                        var open = _at;
                        _at += delimiter;
                        Add(open, UiCodeToken.Interpolation);
                        Interpolation(delimiter);
                        segment = _at;
                        continue;
                    }
                }
                if (dollars > 0 && !raw && source[_at] == '}' && Repeated('}', _at) >= 2)
                {
                    Add(segment, UiCodeToken.String);
                    var escape = _at;
                    _at += 2;
                    Add(escape, UiCodeToken.Escape);
                    segment = _at;
                    continue;
                }
                _at++;
            }
            Add(segment, UiCodeToken.String);
            return true;
        }

        private void Interpolation(int delimiter)
        {
            var nesting = 0;
            var conditional = 0;
            while (_at < source.Length)
            {
                var c = source[_at];
                if (nesting == 0 && c == '}' && Repeated('}', _at) >= delimiter)
                {
                    var close = _at;
                    _at += delimiter;
                    Add(close, UiCodeToken.Interpolation);
                    return;
                }
                if (nesting == 0 && c == ':' && conditional == 0)
                {
                    var format = _at++;
                    Add(format, UiCodeToken.Interpolation);
                    format = _at;
                    while (_at < source.Length && !(source[_at] == '}' && Repeated('}', _at) >= delimiter))
                        _at++;
                    Add(format, UiCodeToken.String);
                    continue;
                }
                if (c is '(' or '[' or '{')
                    nesting++;
                else if (c is ')' or ']' or '}' && nesting > 0)
                    nesting--;
                else if (nesting == 0 && c == '?' && (_at + 1 == source.Length || source[_at + 1] is not ('?' or '.' or '['))
                    && (_at == 0 || source[_at - 1] != '?'))
                    conditional++;
                else if (nesting == 0 && c == ':' && conditional > 0)
                    conditional--;
                Token();
            }
        }
    }
}
