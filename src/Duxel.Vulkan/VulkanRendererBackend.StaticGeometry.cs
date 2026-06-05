using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private StaticGeometryBuffer EnsureStaticGeometryBuffer(string staticTag, UiDrawList drawList, bool requiresContentHash)
    {
        var hasExisting = TryGetActiveStaticGeometryBuffer(staticTag, out var existing);
        var content = CreateStaticGeometryContent(
            staticTag,
            drawList,
            requiresContentHash,
            hasExisting,
            in existing);
        var shape = content.Shape;
        if (hasExisting
            && TryApplyStaticGeometryCacheHit(in existing, content.ContentHash, in shape, out var cached))
        {
            return cached;
        }

        var canUpdateSameShape = CanUpdateStaticGeometryBufferInPlace(in existing, in shape);
        if (_staticGeometryRotatingUpdateEnabled
            && canUpdateSameShape
            && TryTakeReusableStaticGeometryBuffer(
                staticTag,
                in shape,
                out var reusable))
        {
            return ApplyStaticGeometryReusableBuffer(
                staticTag,
                in existing,
                in reusable,
                in content,
                in shape);
        }

        if (_staticGeometryInPlaceUpdateEnabled
            && !_staticGeometryRotatingUpdateEnabled
            && canUpdateSameShape)
        {
            return ApplyStaticGeometryInPlaceUpdate(
                staticTag,
                in existing,
                in content,
                in shape);
        }

        if (HasStaticGeometryBufferResources(in existing))
        {
            return ApplyStaticGeometryReplacement(
                staticTag,
                in existing,
                in content,
                in shape,
                canUpdateSameShape);
        }

        return ApplyStaticGeometryCreation(staticTag, in content, in shape);
    }

}
