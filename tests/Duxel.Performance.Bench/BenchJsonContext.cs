using System.Text.Json.Serialization;
using Duxel.Core;

internal sealed record DynamicBenchResult(int layerCount,int primitivesPerLayer,double allocatedBytesPerFrame,BenchFrameStatistics stats);
internal sealed record DynamicBenchReport(string framework,double warmupSeconds,int phaseSeconds,List<DynamicBenchResult> results);
internal sealed record RetainedBenchResult(int layerCount,int commandsPerLayer,int sameSceneRun,double allocatedBytesPerFrame,string rawFrames,BenchFrameStatistics stats);
internal sealed record RetainedBenchReport(string scope,string framework,double warmupSeconds,int phaseSeconds,List<RetainedBenchResult> results);
internal sealed record OverlayBenchResult(int tiers,int itemsPerTier,double allocatedBytesPerFrame,long rootedManagedBytes,long privateBytes,string rawFrames,BenchFrameStatistics stats);
internal sealed record OverlayBenchReport(string scope,string framework,double warmupSeconds,int phaseSeconds,List<OverlayBenchResult> results);
internal sealed record WidgetBenchResult(string scenario,int buttonCount,double allocatedBytesPerFrame,long privateBefore,long privateAfter,long workingSetBefore,long workingSetAfter,int drawDataCommands,string rawFrames,BenchFrameStatistics stats);
internal sealed record WidgetBenchReport(string scope,string framework,string viewport,double warmupSeconds,int phaseSeconds,bool forcedGc,List<WidgetBenchResult> results);

[JsonSourceGenerationOptions(WriteIndented=true)]
[JsonSerializable(typeof(DynamicBenchReport))]
[JsonSerializable(typeof(RetainedBenchReport))]
[JsonSerializable(typeof(OverlayBenchReport))]
[JsonSerializable(typeof(WidgetBenchReport))]
[JsonSerializable(typeof(DirectTextBenchReport))]
internal partial class BenchJsonContext:JsonSerializerContext;
