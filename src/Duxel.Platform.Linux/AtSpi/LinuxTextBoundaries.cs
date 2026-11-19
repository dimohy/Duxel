using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Linux;

// Pango supplies Unicode/language segmentation; the bridge retains only scalar source offsets.
internal sealed unsafe partial class LinuxTextBoundaries
{
    private const string Library = "libpango-1.0.so.0";
    private readonly int[] _words, _sentences, _paragraphs, _wordEnds, _sentenceEnds;
    private readonly bool _wordEndAtEnd, _sentenceEndAtEnd;
    private readonly bool _trailingParagraph;
    internal UiAccessibilityTextContent Content
    {
        get;
    }
    internal string Language
    {
        get;
    }

    internal static string EnsureAvailable()
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Native Pango logical flags require the supported little-endian ABI.");
        if (pango_version() < 10000)
            throw new PlatformNotSupportedException("Native text segmentation requires Pango 1.0 or later.");
        return Marshal.PtrToStringUTF8(pango_version_string()) ?? throw new InvalidOperationException("Pango did not publish its version.");
    }
    internal static LinuxTextBoundaries Retain(LinuxTextBoundaries? previous, UiAccessibilityTextContent content, string language)
        => previous is not null && ReferenceEquals(previous.Content, content) && previous.Language == language
            ? previous : new(content, language);

    private LinuxTextBoundaries(UiAccessibilityTextContent content, string language)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Content = content;
        Language = language;
        var nativeLanguage = pango_language_from_string(language);
        if (nativeLanguage == 0)
            throw new InvalidOperationException("Pango did not resolve the requested language tag.");
        var length = Encoding.UTF8.GetByteCount(content.Text);
        var bytes = new byte[checked(length + 1)];
        Encoding.UTF8.GetBytes(content.Text.AsSpan(), bytes.AsSpan());
        List<int> words = [0], sentences = [0], paragraphs = [0];
        List<int> wordEnds = [0], sentenceEnds = [0];
        var byteStart = 0;
        var scalarStart = 0;
        fixed (byte* data = bytes)
        {
            while (byteStart < length)
            {
                int delimiter, next;
                pango_find_paragraph_boundary(data + byteStart, length - byteStart, &delimiter, &next);
                if (delimiter < 0 || next <= 0 || delimiter > next || next > length - byteStart)
                    throw new InvalidDataException("Pango returned an invalid paragraph byte range.");
                var bodyCount = CountScalars(bytes.AsSpan(byteStart, delimiter));
                var separatorCount = CountScalars(bytes.AsSpan(byteStart + delimiter, next - delimiter));
                if (bodyCount > 0)
                {
                    var attrs = new uint[checked(bodyCount + 1)];
                    fixed (uint* flags = attrs)
                        pango_get_log_attrs(data + byteStart, delimiter, -1, nativeLanguage, flags, attrs.Length);
                    if ((attrs[0] & (1u << 4)) == 0 || (attrs[^1] & (1u << 4)) == 0)
                        throw new InvalidDataException("Pango did not publish paragraph cursor boundaries.");
                    for (var index = 0; index < attrs.Length; index++)
                    {
                        if ((attrs[index] & (1u << 5)) != 0)
                            Add(words, scalarStart + index);
                        if ((attrs[index] & (1u << 8)) != 0)
                            Add(sentences, scalarStart + index);
                        if ((attrs[index] & (1u << 6)) != 0)
                        {
                            Add(wordEnds, scalarStart + index);
                            if (scalarStart + index == content.CharacterCount)
                                _wordEndAtEnd = true;
                        }
                        if ((attrs[index] & (1u << 9)) != 0)
                        {
                            Add(sentenceEnds, scalarStart + index);
                            if (scalarStart + index == content.CharacterCount)
                                _sentenceEndAtEnd = true;
                        }
                    }
                }
                scalarStart += bodyCount + separatorCount;
                byteStart += next;
                if (separatorCount > 0)
                {
                    Add(paragraphs, scalarStart);
                    _trailingParagraph = byteStart == length;
                }
                else
                    _trailingParagraph = false;
            }
        }
        if (scalarStart != content.CharacterCount)
            throw new InvalidDataException("Native paragraph ranges do not cover the published scalar text.");
        Add(words, content.CharacterCount);
        Add(sentences, content.CharacterCount);
        Add(paragraphs, content.CharacterCount);
        Add(wordEnds, content.CharacterCount);
        Add(sentenceEnds, content.CharacterCount);
        _words = words.ToArray();
        _sentences = sentences.ToArray();
        _paragraphs = paragraphs.ToArray();
        _wordEnds = wordEnds.ToArray();
        _sentenceEnds = sentenceEnds.ToArray();
    }
    private static void Add(List<int> values, int offset)
    {
        if (values[^1] != offset)
            values.Add(offset);
    }
    private static int CountScalars(ReadOnlySpan<byte> bytes)
    {
        var count = 0;
        while (!bytes.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(bytes, out _, out var consumed) != OperationStatus.Done)
                throw new InvalidDataException("A native paragraph boundary split a UTF8 scalar.");
            bytes = bytes[consumed..];
            count++;
        }
        return count;
    }
    internal (int Start, int End) GetRange(int offset, uint granularity)
    {
        if ((uint)offset > (uint)Content.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var boundaries = granularity switch
        {
            1 => _words,
            2 => _sentences,
            4 => _paragraphs,
            _ => throw new ArgumentOutOfRangeException(nameof(granularity))
        };
        var lookup = offset == Content.CharacterCount && offset > 0 && !(granularity == 4 && _trailingParagraph) ? offset - 1 : offset;
        var index = Array.BinarySearch(boundaries, lookup);
        if (index < 0)
            index = ~index - 1;
        return (boundaries[index], index + 1 < boundaries.Length ? boundaries[index + 1] : Content.CharacterCount);
    }
    internal (int Start, int End) GetLegacyRange(int offset, uint boundary, int relative)
    {
        var sequence = boundary switch
        {
            1 => new LinuxTextBoundarySequence(_words, Content.CharacterCount, false),
            2 => new LinuxTextBoundarySequence(_wordEnds, Content.CharacterCount, _wordEndAtEnd),
            3 => new LinuxTextBoundarySequence(_sentences, Content.CharacterCount, false),
            4 => new LinuxTextBoundarySequence(_sentenceEnds, Content.CharacterCount, _sentenceEndAtEnd),
            _ => throw new ArgumentOutOfRangeException(nameof(boundary))
        };
        return sequence.GetRange(offset, relative);
    }
    [LibraryImport(Library)] private static partial int pango_version();
    [LibraryImport(Library)] private static partial nint pango_version_string();
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint pango_language_from_string(string language);
    [LibraryImport(Library)] private static partial void pango_find_paragraph_boundary(byte* text, int length, int* delimiter, int* next);
    [LibraryImport(Library)] private static partial void pango_get_log_attrs(byte* text, int length, int level, nint language, uint* attrs, int count);
}
