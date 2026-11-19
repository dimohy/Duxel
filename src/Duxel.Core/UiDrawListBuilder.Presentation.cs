using System.Numerics;
using System.Runtime.InteropServices;

namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    private sealed class PresentationSnapshot(UiDrawList owner, UiDrawList replay)
    {
        internal UiDrawList Owner { get; } = owner;
        internal UiDrawList Replay { get; set; } = replay;
    }
    private List<PresentationSnapshot>? _presentationSnapshots;
    private UiLayerFrameLifetime? _presentationLifetime;
    private string? _presentationKey;
    private UiLayer3D _presentationScene;
    private int _presentationCursor;
    internal bool HasPresentationFrame => _presentationCursor != 0;
    internal int ReusedPresentationLists
    {
        get; private set;
    }
    internal int RebuiltPresentationLists
    {
        get; private set;
    }

    internal void ConfigurePresentation(string key, int tier, UiLayerFrameLifetime lifetime)
    {
        _presentationKey = key;
        _presentationLifetime = lifetime;
        _presentationSnapshots = [];
        _presentationScene = new(Matrix4x4.CreateTranslation(0, 0, .5f / (tier + 1)), WriteDepth: false)
        {
            DepthScopeId = UiLayer3D.PresentationDepthScopeId,
            IsScreenSpacePresentation = true,
        };
    }

    internal void AppendBuiltTo(List<UiDrawList> destination)
    {
        Flush();
        destination.AddRange(_drawLists);
        _drawLists.Clear();
        if (_presentationSnapshots is not { } snapshots)
            return;
        for (var i = snapshots.Count - 1; i >= _presentationCursor; i--)
        {
            _presentationLifetime!.Retire(snapshots[i].Owner);
            snapshots.RemoveAt(i);
        }
        if (_presentationCursor == 0)
        {
            _vertices.TrimExcess();
            _indices.TrimExcess();
            _commands.TrimExcess();
            _rectFilledPrimitives.TrimExcess();
            _circleFilledPrimitives.TrimExcess();
        }
    }

    internal void ReleasePresentation()
    {
        if (_presentationSnapshots is not { } snapshots)
            return;
        foreach (var snapshot in snapshots)
            _presentationLifetime!.Retire(snapshot.Owner);
        snapshots.Clear();
    }

    private bool TryFlushPresentation(ulong scheduleStamp)
    {
        if (_presentationSnapshots is not { } snapshots)
            return false;
        // Callbacks and GPU simulation payloads have frame-specific execution semantics.
        foreach (ref readonly var command in _commands.AsSpan())
            if (command.Callback is not null || command.UserData is not null)
                return false;

        var slot = _presentationCursor++;
        var previous = slot < snapshots.Count ? snapshots[slot] : null;
        if (previous is not null && previous.Replay.Scene3D == (_scene3D ?? _presentationScene)
            && MemoryMarshal.AsBytes(_vertices.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(previous.Owner.Vertices.AsSpan()))
            && _indices.AsSpan().SequenceEqual(previous.Owner.Indices.AsSpan())
            && _commands.AsSpan().SequenceEqual(previous.Owner.Commands.AsSpan())
            && MemoryMarshal.AsBytes(_rectFilledPrimitives.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(previous.Owner.RectFilledPrimitives!.AsSpan()))
            && MemoryMarshal.AsBytes(_circleFilledPrimitives.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(previous.Owner.CircleFilledPrimitives!.AsSpan())))
        {
            if (previous.Replay.StaticGeometryKey is null)
                previous.Replay = CreateResidentPresentation(previous.Owner, slot, scheduleStamp);
            _vertices.Clear();
            _indices.Clear();
            _commands.Clear();
            _rectFilledPrimitives.Clear();
            _circleFilledPrimitives.Clear();
            _drawLists.Add(previous.Replay);
            ReusedPresentationLists++;
            return true;
        }

        UiDrawList owner;
        if (previous is null)
        {
            // Resident snapshots outlive the builder scratch. Keep exact owned
            // arrays, sharing one scratch allocation across all initial surfaces.
            owner = new(UiPooledList<UiDrawVertex>.FromArray(_vertices.AsSpan().ToArray()),
                UiPooledList<uint>.FromArray(_indices.AsSpan().ToArray()),
                UiPooledList<UiDrawCommand>.FromArray(_commands.AsSpan().ToArray()),
                RectFilledPrimitives: UiPooledList<UiRectFilledPrimitive>.FromArray(_rectFilledPrimitives.AsSpan().ToArray()),
                CircleFilledPrimitives: UiPooledList<UiCircleFilledPrimitive>.FromArray(_circleFilledPrimitives.AsSpan().ToArray()));
            _vertices.Clear();
            _indices.Clear();
            _commands.Clear();
            _rectFilledPrimitives.Clear();
            _circleFilledPrimitives.Clear();
        }
        else
            owner = new(_vertices.TransferToPooledList(_retainScratchCapacity), _indices.TransferToPooledList(_retainScratchCapacity), _commands.TransferToPooledList(_retainScratchCapacity),
                RectFilledPrimitives: _rectFilledPrimitives.TransferToPooledList(_retainScratchCapacity),
                CircleFilledPrimitives: _circleFilledPrimitives.TransferToPooledList(_retainScratchCapacity));
        // A changing surface uses the shared dynamic upload stream. Its stable
        // peers stay resident; do not allocate/retire GPU buffers on every move.
        var replay = previous is null ? CreateResidentPresentation(owner, slot, scheduleStamp)
            : owner with
            {
                OwnsBuffers = false,
                Scene3D = _scene3D ?? _presentationScene
            };
        var replacement = new PresentationSnapshot(owner, replay);
        if (previous is null)
            snapshots.Add(replacement);
        else
        {
            _presentationLifetime!.Retire(previous.Owner);
            snapshots[slot] = replacement;
        }
        _drawLists.Add(replay);
        RebuiltPresentationLists++;
        return true;
    }

    private UiDrawList CreateResidentPresentation(UiDrawList owner, int slot, ulong scheduleStamp)
        => owner with
        {
            OwnsBuffers = false,
            StaticGeometryKey = _presentationKey + ":" + slot,
            StaticGeometryStamp = owner.GetStaticGeometryStamp(),
            CommandStateStamp = UiDrawList.ComputeCommandStateStamp(owner.Commands.AsSpan()),
            CommandScheduleStamp = scheduleStamp,
            Scene3D = _scene3D ?? _presentationScene,
        };
}
