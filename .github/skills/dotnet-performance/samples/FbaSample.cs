// Sample: FBA 기반 성능/할당 비교 ( .NET 10 / C# 14 )
// 목표: Span 기반 파싱 vs string 할당 기반 파싱 비교 + stackalloc 예시

using System;
using System.Buffers.Text;

const int Iterations = 1_000_000;

FastBench.Run("SpanParser", Iterations, SpanParserBench);
FastBench.Run("StringParser", Iterations, StringParserBench);
FastBench.Run("StackallocHex", Iterations, StackallocHexBench);

static void SpanParserBench()
{
    ReadOnlySpan<char> input = "A=12345";
    _ = SpanParser.TryParse(input, out _, out _);
}

static void StringParserBench()
{
    string input = "A=12345";
    _ = StringParser.TryParse(input, out _, out _);
}

static void StackallocHexBench()
{
    Span<byte> buffer = stackalloc byte[16];
    _ = Utf8Formatter.TryFormat(0xDEADBEEF, buffer, out _, 'X');
}

public static class SpanParser
{
    public static bool TryParse(ReadOnlySpan<char> input, out ReadOnlySpan<char> key, out int value)
    {
        int idx = input.IndexOf('=');
        if (idx <= 0 || idx >= input.Length - 1)
        {
            key = default;
            value = default;
            return false;
        }

        key = input.Slice(0, idx);
        ReadOnlySpan<char> valueSpan = input.Slice(idx + 1);
        return TryParseInt(valueSpan, out value);
    }

    private static bool TryParseInt(ReadOnlySpan<char> s, out int value)
    {
        int acc = 0;
        for (int i = 0; i < s.Length; i++)
        {
            int d = s[i] - '0';
            if ((uint)d > 9)
            {
                value = default;
                return false;
            }
            acc = acc * 10 + d;
        }
        value = acc;
        return true;
    }
}

public static class StringParser
{
    public static bool TryParse(string input, out string key, out int value)
    {
        int idx = input.IndexOf('=');
        if (idx <= 0 || idx >= input.Length - 1)
        {
            key = string.Empty;
            value = default;
            return false;
        }

        key = input.Substring(0, idx);
        string valueText = input.Substring(idx + 1);
        return int.TryParse(valueText, out value);
    }
}
