using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Svg;

// Result shapes retain their existing field names, casing and order. Native
// probes share generated metadata instead of runtime object-type discovery.
internal sealed record LinuxChecksResult(string framework, string scope, int passed, IReadOnlyList<string> checks);
internal sealed record LinuxBoundaryResult(string pangoVersion, int passed, IReadOnlyList<string> checks);
internal sealed record LinuxFontResult(string harfbuzzVersion, int passed, IReadOnlyList<string> checks);
internal sealed record LinuxApplicationResult(string framework, string os, string architecture, int passed,
    IReadOnlyList<string> checks, IReadOnlyList<LinuxApplicationObservation> observations, bool validationHooks);
internal sealed record LinuxApplicationObservation(bool registered, int RenderCount, int idleFrames, int workerFrames,
    double idleCpuMilliseconds, int Clicks, string Value, bool nativeCloseSent, int capturedColors,
    bool imagePixelsMatch, bool transformedSvgReused, bool nativeAccessibility, bool nativeAccessibilityWindowFocus,
    SvgStoreStatistics imageStatistics, bool copied, bool pasted, bool unicodeTextState,
    bool userClipboardRead, bool userClipboardWritten, long elapsedMilliseconds);
internal sealed record LinuxTransportResult(string framework, string failureKind, int expectedFailures,
    int measuredFailures, int beforeDescriptors, int afterDescriptors, int ownedThreadsRemaining,
    IReadOnlyList<double> elapsedMilliseconds);
internal sealed record LinuxAncestorResult(string framework, bool rightToLeft, bool virtualized, bool variableExtents,
    string? popupMode, int passed, IReadOnlyList<string> checks, string qualification);
internal sealed record LinuxCleanupResult(string framework, uint parentPid, bool nativeCleanupCompleted,
    int passed, IReadOnlyList<string> checks);
internal sealed record LinuxRegistryResult(string framework, uint parentPid, bool registered);
internal sealed record LinuxQualifiedResult(string framework, int passed, IReadOnlyList<string> checks, string qualification);
internal sealed record LinuxMarkdownResult(string framework, int passed, IReadOnlyList<string> checks,
    bool userClipboardRead, bool userClipboardWritten, string qualification);
internal sealed record LinuxPolicyResult(int passed, IReadOnlyList<string> checks);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LinuxChecksResult))]
[JsonSerializable(typeof(LinuxBoundaryResult))]
[JsonSerializable(typeof(LinuxFontResult))]
[JsonSerializable(typeof(LinuxApplicationResult))]
[JsonSerializable(typeof(LinuxApplicationObservation))]
[JsonSerializable(typeof(LinuxTransportResult))]
[JsonSerializable(typeof(LinuxAncestorResult))]
[JsonSerializable(typeof(LinuxCleanupResult))]
[JsonSerializable(typeof(LinuxRegistryResult))]
[JsonSerializable(typeof(LinuxQualifiedResult))]
[JsonSerializable(typeof(LinuxMarkdownResult))]
[JsonSerializable(typeof(LinuxPolicyResult))]
[JsonSerializable(typeof(JsonElement))]
internal partial class LinuxProbeResultsJson : JsonSerializerContext;
