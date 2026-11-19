using System.Runtime.InteropServices;

namespace Duxel.Core;

/// <summary>Initial state and immutable appearance for a GPU-resident circular body. Exactly 48 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct UiGpuParticle
{
    public UiVector2 Position;
    public UiVector2 Direction;
    public float Rotation;
    public float AngularSpeed;
    public float HitResponse;
    public float SpeedScale;
    public float SizeScale;
    public int SideOffset;
    public uint Color;
    public uint Reserved;
}

/// <summary>Immutable seeds and stable identities captured by one submitted frame.</summary>
public sealed class UiGpuParticleSnapshot
{
    private readonly object _owner;
    private readonly long[] _identities;
    public ReadOnlyMemory<UiGpuParticle> Initial
    {
        get;
    }
    public int Count => Initial.Length;
    public int MaximumSides
    {
        get;
    }
    public float MaximumSizeScale
    {
        get;
    }
    public int MaximumSideOffset { get; } = int.MinValue;

    internal UiGpuParticleSnapshot(object owner, UiGpuParticle[] initial, long[] identities, int maximumSides)
    {
        _owner = owner;
        _identities = identities;
        Initial = initial;
        MaximumSides = maximumSides;
        foreach (var body in initial)
        {
            if (!float.IsFinite(body.Position.X) || !float.IsFinite(body.Position.Y)
                || !float.IsFinite(body.Direction.X) || !float.IsFinite(body.Direction.Y)
                || !float.IsFinite(body.Rotation) || !float.IsFinite(body.AngularSpeed)
                || !float.IsFinite(body.HitResponse) || body.HitResponse < 0 || body.HitResponse > 1
                || !float.IsFinite(body.SizeScale) || body.SizeScale <= 0
                || !float.IsFinite(body.SpeedScale) || body.SpeedScale < 0)
                throw new ArgumentOutOfRangeException(nameof(initial));
            MaximumSizeScale = MathF.Max(MaximumSizeScale, body.SizeScale);
            MaximumSideOffset = Math.Max(MaximumSideOffset, body.SideOffset);
        }
        _ = checked(Count * MaximumSides * 20);
        _ = checked(Count * (MaximumSides - 2) * 3);
    }

    /// <summary>Leading bodies whose live state survives even coalesced truncate/append edits.</summary>
    public int GetPreservedPrefixCount(UiGpuParticleSnapshot previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (!ReferenceEquals(_owner, previous._owner))
            return 0;
        var count = Math.Min(Count, previous.Count);
        var preserved = 0;
        while (preserved < count && _identities[preserved] == previous._identities[preserved])
            preserved++;
        return preserved;
    }

    internal long[] CopyIdentities(int count) => _identities.AsSpan(0, count).ToArray();
}

/// <summary>Published seeds change only on edits. Each renderer owns the live GPU simulation state.</summary>
public sealed class UiGpuParticleSystem : IDisposable
{
    private int _disposed;
    private readonly object _mutationLock = new();
    private readonly object _identity = new();
    private long _nextIdentity;
    private UiGpuParticleSnapshot _snapshot;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    public UiGpuParticleSnapshot CaptureSnapshot() => Volatile.Read(ref _snapshot);
    public ReadOnlyMemory<UiGpuParticle> Initial => CaptureSnapshot().Initial;
    public int Count => CaptureSnapshot().Count;
    public int MaximumSides => CaptureSnapshot().MaximumSides;
    public float MaximumSizeScale => CaptureSnapshot().MaximumSizeScale;
    public int MaximumSideOffset => CaptureSnapshot().MaximumSideOffset;

    public UiGpuParticleSystem(ReadOnlySpan<UiGpuParticle> initial, int maximumSides = 12)
    {
        if (initial.IsEmpty)
            throw new ArgumentException("At least one body is required.", nameof(initial));
        if (maximumSides < 3 || maximumSides > 64)
            throw new ArgumentOutOfRangeException(nameof(maximumSides));
        var identities = new long[initial.Length];
        for (var i = 0; i < identities.Length; i++)
            identities[i] = ++_nextIdentity;
        _snapshot = new(_identity, initial.ToArray(), identities, maximumSides);
    }

    /// <summary>Adds seeds without replacing the live state of existing bodies. No GPU readback.</summary>
    public void Append(ReadOnlySpan<UiGpuParticle> added)
    {
        lock (_mutationLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (added.IsEmpty)
                return;
            var previous = _snapshot;
            var count = checked(previous.Count + added.Length);
            var initial = new UiGpuParticle[count];
            previous.Initial.Span.CopyTo(initial);
            added.CopyTo(initial.AsSpan(previous.Count));
            var identities = new long[count];
            previous.CopyIdentities(previous.Count).CopyTo(identities, 0);
            var next = _nextIdentity;
            for (var i = previous.Count; i < count; i++)
                identities[i] = checked(++next);
            var snapshot = new UiGpuParticleSnapshot(_identity, initial, identities, previous.MaximumSides);
            _nextIdentity = next;
            Volatile.Write(ref _snapshot, snapshot);
        }
    }

    /// <summary>Keeps the leading bodies and their live state; a zero-body world should be disposed.</summary>
    public void Truncate(int count)
    {
        lock (_mutationLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var previous = _snapshot;
            if (count < 1 || count > previous.Count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == previous.Count)
                return;
            Volatile.Write(ref _snapshot, new(_identity, previous.Initial.Span[..count].ToArray(),
                previous.CopyIdentities(count), previous.MaximumSides));
        }
    }
}

/// <summary>One simulation step, submitted once per renderer frame even when drawn in several views.</summary>
public sealed record UiGpuParticleFrame(UiGpuParticleSystem System, UiRect Bounds, float DeltaSeconds,
    float Radius = 6, float Speed = 160, int Sides = 5, float MaximumAngularSpeed = 4.5f,
    int CollisionIterations = 2)
{
    private UiGpuParticleSystem _system = System;
    public UiGpuParticleSnapshot Snapshot { get; private init; } = System.CaptureSnapshot();
    public UiGpuParticleSystem System
    {
        get => _system;
        init
        {
            _system = value;
            Snapshot = value.CaptureSnapshot();
        }
    }

    public void Validate(bool allowDisposedSystem = false)
    {
        ArgumentNullException.ThrowIfNull(System);
        if (!allowDisposedSystem)
            ObjectDisposedException.ThrowIf(System.IsDisposed, System);
        if (!float.IsFinite(DeltaSeconds) || DeltaSeconds < 0 || DeltaSeconds > .05f
            || !float.IsFinite(Radius) || Radius <= 0 || !float.IsFinite(Speed) || Speed < 0
            || !float.IsFinite(MaximumAngularSpeed) || MaximumAngularSpeed < 0
            || CollisionIterations < 1 || CollisionIterations > 8
            || !float.IsFinite(Bounds.X) || !float.IsFinite(Bounds.Y)
            || !float.IsFinite(Bounds.Width) || !float.IsFinite(Bounds.Height)
            || Bounds.Width < Radius * Snapshot.MaximumSizeScale * 2 || Bounds.Height < Radius * Snapshot.MaximumSizeScale * 2)
            throw new ArgumentOutOfRangeException(nameof(UiGpuParticleFrame));
        if (Math.Max(3, (long)Sides + Snapshot.MaximumSideOffset) > Snapshot.MaximumSides)
            throw new ArgumentOutOfRangeException(nameof(Sides));
        var cellSize = Radius * Snapshot.MaximumSizeScale * 2;
        if (!float.IsFinite(cellSize) || cellSize <= 0
            || Math.Ceiling(Bounds.Width / (double)cellSize) * Math.Ceiling(Bounds.Height / (double)cellSize) > 1_048_576)
            throw new ArgumentOutOfRangeException(nameof(Bounds), "GPU particle grid cell budget exceeded.");
    }
}

public sealed partial class UiDrawListBuilder
{
    /// <summary>Queues GPU simulation and vector geometry generation. No per-frame particle readback is required.</summary>
    public void AddGpuParticles(UiGpuParticleFrame frame)
    {
        frame.Validate();
        if (!IsPresentationPass)
            return;
        // A marker triangle keeps this ordinary draw-list command alive. The backend
        // binds the GPU-generated stream; these three vertices are never displayed.
        EnsureCapacityFor(3, 3);
        var vertex = (uint)_vertices.Count;
        var index = (uint)_indices.Count;
        for (var i = 0; i < 3; i++)
        {
            _vertices.Add(default);
            _indices.Add(vertex);
        }
        // ElementCount describes the marker's CPU range. GPU range comes from the typed frame.
        _commands.Add(new(_currentClipRect, _currentTexture, index, 3, 0, UserData: frame,
            Bounds: frame.Bounds, HasBounds: true));
    }
}
