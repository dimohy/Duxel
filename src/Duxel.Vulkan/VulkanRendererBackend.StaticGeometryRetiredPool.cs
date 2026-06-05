using System;
using System.Collections.Generic;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const int StaticGeometryRetiredReuseGraceFrames = 120;

    private readonly Dictionary<string, List<RetiredStaticGeometryBuffer>> _retiredStaticGeometryBuffers = new(StringComparer.Ordinal);
    private readonly List<string> _retiredStaticGeometryStaleTags = new();

    private bool HasRetiredStaticGeometryBuffers()
    {
        return _retiredStaticGeometryBuffers.Count is not 0;
    }

    private bool TryTakeReusableStaticGeometryBuffer(
        string staticTag,
        in StaticGeometryShape shape,
        out StaticGeometryBuffer reusable)
    {
        reusable = default;
        if (!_retiredStaticGeometryBuffers.TryGetValue(staticTag, out var retiredBuffers)
            || retiredBuffers.Count is 0)
        {
            return false;
        }

        for (var i = 0; i < retiredBuffers.Count; i++)
        {
            var retired = retiredBuffers[i];
            var retiredBuffer = retired.Buffer;
            if (_frameIndex < retired.AvailableFrame)
            {
                continue;
            }

            if (!CanUpdateStaticGeometryBufferInPlace(
                    in retiredBuffer,
                    in shape))
            {
                QueueStaticGeometryBufferDestroy(retiredBuffer);
                retiredBuffers.RemoveAt(i);
                if (retiredBuffers.Count is 0)
                {
                    _retiredStaticGeometryBuffers.Remove(staticTag);
                }

                i--;
                continue;
            }

            reusable = retiredBuffer;
            retiredBuffers.RemoveAt(i);
            if (retiredBuffers.Count is 0)
            {
                _retiredStaticGeometryBuffers.Remove(staticTag);
            }

            return true;
        }

        return false;
    }

    private void RetireStaticGeometryBufferForReuse(StaticGeometryBuffer staticBuffer)
    {
        if (staticBuffer.VertexBuffer.Handle is 0
            && staticBuffer.IndexBuffer.Handle is 0
            && staticBuffer.PrimitiveBuffer.Handle is 0)
        {
            return;
        }

        var frameCount = _frames.Length;
        var availableFrame = _frameIndex + Math.Max(1, frameCount);
        if (!_retiredStaticGeometryBuffers.TryGetValue(staticBuffer.Tag, out var retiredBuffers))
        {
            retiredBuffers = new List<RetiredStaticGeometryBuffer>();
            _retiredStaticGeometryBuffers[staticBuffer.Tag] = retiredBuffers;
        }

        retiredBuffers.Add(new RetiredStaticGeometryBuffer(staticBuffer, availableFrame));
        TrimRetiredStaticGeometryBufferList(retiredBuffers, Math.Max(1, frameCount));
    }

    private void QueueRetiredStaticGeometryBuffersDestroy(string staticTag)
    {
        if (!_retiredStaticGeometryBuffers.Remove(staticTag, out var retiredBuffers))
        {
            return;
        }

        for (var i = 0; i < retiredBuffers.Count; i++)
        {
            QueueStaticGeometryBufferDestroy(retiredBuffers[i].Buffer);
        }

        retiredBuffers.Clear();
    }

    private void TrimRetiredStaticGeometryBufferList(List<RetiredStaticGeometryBuffer> retiredBuffers, int maxRetiredBuffers)
    {
        while (retiredBuffers.Count > maxRetiredBuffers)
        {
            var retired = retiredBuffers[0];
            QueueStaticGeometryBufferDestroy(retired.Buffer);
            retiredBuffers.RemoveAt(0);
        }
    }

    private void PruneRetiredStaticGeometryBuffers(int currentFrameIndex)
    {
        if (_retiredStaticGeometryBuffers.Count is 0)
        {
            return;
        }

        var staleTags = _retiredStaticGeometryStaleTags;
        staleTags.Clear();
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            var write = 0;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                var retired = retiredBuffers[i];
                if (currentFrameIndex >= retired.AvailableFrame + StaticGeometryRetiredReuseGraceFrames)
                {
                    QueueStaticGeometryBufferDestroy(retired.Buffer);
                    continue;
                }

                retiredBuffers[write++] = retired;
            }

            if (write < retiredBuffers.Count)
            {
                retiredBuffers.RemoveRange(write, retiredBuffers.Count - write);
            }

            if (retiredBuffers.Count is 0)
            {
                staleTags.Add(pair.Key);
            }
        }

        for (var i = 0; i < staleTags.Count; i++)
        {
            _retiredStaticGeometryBuffers.Remove(staleTags[i]);
        }

        staleTags.Clear();
    }

    private (int Entries, ulong Bytes) GetRetiredStaticGeometryMemoryStats()
    {
        var retiredEntries = 0;
        var retiredBytes = 0UL;
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                retiredEntries++;
                retiredBytes += GetStaticGeometryBufferByteSize(retiredBuffers[i].Buffer);
            }
        }

        return (retiredEntries, retiredBytes);
    }

    private void DestroyRetiredStaticGeometryBuffersImmediate()
    {
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                DestroyStaticGeometryBufferImmediate(retiredBuffers[i].Buffer);
            }

            retiredBuffers.Clear();
        }

        _retiredStaticGeometryBuffers.Clear();
        _retiredStaticGeometryStaleTags.Clear();
    }

    private void ClearRetiredStaticGeometryBuffers()
    {
        _retiredStaticGeometryBuffers.Clear();
        _retiredStaticGeometryStaleTags.Clear();
    }
}
