using System.Text.Json.Serialization;
using Duxel.Core;

// Keep result field names stable while making the same fixtures executable under NativeAOT.
internal sealed record LinuxPlatformResult(string framework, string os, string architecture, string mode,
    List<string> devices, int passed, List<string> checks);
internal sealed record LinuxTextSample(string sample, PlatformTextLayoutResult layout, string sha256);
internal sealed record LinuxTextResult(string framework, string os, string architecture, string mode,
    string[] fonts, int passed, List<string> checks, List<LinuxTextSample> samples);
internal sealed record LinuxVectorComparison(string content, string font, int triangles, int samples, int filled, int mismatch);
internal sealed record LinuxVectorResult(string framework, string os, string architecture, string mode,
    string[] fonts, int passed, List<string> checks, List<LinuxVectorComparison> comparisons);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LinuxPlatformResult))]
[JsonSerializable(typeof(LinuxTextResult))]
[JsonSerializable(typeof(LinuxVectorResult))]
[JsonSerializable(typeof(string))]
internal partial class LinuxValidationJson : JsonSerializerContext;
