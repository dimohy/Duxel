namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private bool TryApplyStaticGeometryCacheHit(
        in StaticGeometryBuffer existing,
        ulong contentHash,
        in StaticGeometryShape shape,
        out StaticGeometryBuffer staticBuffer)
    {
        if (!StaticGeometryCacheEntryMatches(in existing, contentHash, in shape))
        {
            staticBuffer = default;
            return false;
        }

        _profileStaticGeometryHitCount++;
        staticBuffer = existing;
        return true;
    }

    private StaticGeometryBuffer ApplyStaticGeometryReusableBuffer(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryBuffer reusable,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        var reused = ReuploadStaticGeometryBufferContent(
            in reusable,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);
        RetireStaticGeometryBufferForReuse(existing);
        _profileStaticGeometryReuseCount++;
        SetActiveStaticGeometryBuffer(staticTag, reused);
        return reused;
    }

    private StaticGeometryBuffer ApplyStaticGeometryInPlaceUpdate(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        WaitForAllInFlightFrameFences();

        var updated = ReuploadStaticGeometryBufferContent(
            in existing,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);
        _profileStaticGeometryUpdateCount++;
        SetActiveStaticGeometryBuffer(staticTag, updated);
        return updated;
    }

    private StaticGeometryBuffer ApplyStaticGeometryReplacement(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryContent content,
        in StaticGeometryShape shape,
        bool canUpdateSameShape)
    {
        _profileStaticGeometryReplaceCount++;
        if (_staticGeometryRotatingUpdateEnabled && canUpdateSameShape)
        {
            RetireStaticGeometryBufferForReuse(existing);
        }
        else
        {
            QueueStaticGeometryBufferDestroy(existing);
            QueueRetiredStaticGeometryBuffersDestroy(staticTag);
        }

        return MaterializeAndActivateStaticGeometryBuffer(staticTag, in content, in shape);
    }

    private StaticGeometryBuffer ApplyStaticGeometryCreation(
        string staticTag,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        _profileStaticGeometryCreateCount++;
        return MaterializeAndActivateStaticGeometryBuffer(staticTag, in content, in shape);
    }

    private StaticGeometryBuffer MaterializeAndActivateStaticGeometryBuffer(
        string staticTag,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        var created = CreateStaticGeometryBuffer(
            staticTag,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);

        SetActiveStaticGeometryBuffer(staticTag, created);
        return created;
    }
}
