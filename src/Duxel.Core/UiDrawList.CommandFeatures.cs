using System.Runtime.CompilerServices;

namespace Duxel.Core;

[Flags]
public enum UiDrawCommandFeatures { None = 0, Callbacks = 1, GpuParticles = 2 }

public sealed partial record class UiDrawList
{
    private sealed record CommandFeaturesSnapshot(ulong Stamp, int Count, UiDrawCommandFeatures Features);
    private sealed class CommandFeaturesCache { public CommandFeaturesSnapshot? Snapshot; }
    private static readonly ConditionalWeakTable<UiPooledList<UiDrawCommand>, CommandFeaturesCache> CommandFeatures = new();

    /// <summary>Finds special commands. Nonzero CommandStateStamp permits reuse across immutable retained views.</summary>
    public UiDrawCommandFeatures GetCommandFeatures()
    {
        var stamp = CommandStateStamp;
        // Unstamped caller-owned commands may change between submissions.
        if (stamp == 0) return ScanCommandFeatures();
        var cache = CommandFeatures.GetValue(Commands, static _ => new());
        var snapshot = Volatile.Read(ref cache.Snapshot);
        if (snapshot is not null && snapshot.Stamp == stamp && snapshot.Count == Commands.Count) return snapshot.Features;
        var features = ScanCommandFeatures();
        Volatile.Write(ref cache.Snapshot, new(stamp, Commands.Count, features));
        return features;
    }
    private UiDrawCommandFeatures ScanCommandFeatures()
    {
        var features = UiDrawCommandFeatures.None;
        foreach (ref readonly var command in Commands.AsSpan())
        {
            if (command.Callback is not null) features |= UiDrawCommandFeatures.Callbacks;
            if (command.UserData is UiGpuParticleFrame) features |= UiDrawCommandFeatures.GpuParticles;
            if (features == (UiDrawCommandFeatures.Callbacks | UiDrawCommandFeatures.GpuParticles)) break;
        }
        return features;
    }
}

public sealed partial record class UiDrawData
{
    private sealed record FrameCommandFeatures(UiPooledList<UiDrawList> Lists, ulong FrameId, int Count, UiDrawCommandFeatures Features);
    private FrameCommandFeatures? _commandFeatures;

    /// <summary>Shares feature inspection between producer and renderer for one immutable, identified frame.</summary>
    public UiDrawCommandFeatures GetCommandFeatures()
    {
        var cached = Volatile.Read(ref _commandFeatures);
        if (DrawDataFrameId != 0 && cached is not null && ReferenceEquals(cached.Lists, DrawLists)
            && cached.FrameId == DrawDataFrameId && cached.Count == DrawLists.Count) return cached.Features;
        var features = UiDrawCommandFeatures.None;
        foreach (var list in DrawLists.AsSpan()) features |= list.GetCommandFeatures();
        if (DrawDataFrameId != 0) Volatile.Write(ref _commandFeatures, new(DrawLists, DrawDataFrameId, DrawLists.Count, features));
        return features;
    }
}
