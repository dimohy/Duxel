using Duxel.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private void RecordCommandDrawPath(
        in UiDrawCommand command,
        in CommandClassification classification,
        in TextureResource texture,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        ref CommandPipelineState pipelineState,
        ref CommandDescriptorState descriptorState,
        ref CommandBufferBindingState bufferBindingState,
        ref CommandPushConstantState pushConstantState,
        ref CommandDrawDispatchState drawDispatchState)
    {
        // Draw mode 0 = indexed triangle pulling; mode 1 = primitive instance expansion.
        var usesTriangleGeometry = classification.IsTriangleCommand
            || classification.StaticPrimitiveUsesTriangleGeometry;
        PushDrawModeIfNeeded(
            frameContext.CommandBuffer,
            usesTriangleGeometry ? 0u : 1u,
            command.Kind,
            classification.IsFontCommand,
            frameContext.ProfileEnabled,
            ref pipelineState);

        ApplyPushConstantsIfNeeded(
            in frameContext,
            command.Translation.X,
            command.Translation.Y,
            command.Opacity,
            ref pushConstantState);

        // Every draw samples from the bindless array; the subpixel coverage mode
        // for ClearType text is packed into the high bit of the texture index.
        var textureIndexAndMode = texture.SlotIndex
            | (classification.IsFontCommand ? SubpixelCoverageModeBit : 0u);
        PushTextureIndexIfNeeded(
            frameContext.CommandBuffer,
            textureIndexAndMode,
            frameContext.ProfileEnabled,
            ref descriptorState);

        if (usesTriangleGeometry)
        {
            var vertexAddress = hasStaticBinding ? staticBinding.VertexAddress : frameContext.DynamicVertexAddress;
            var indexBuffer = hasStaticBinding ? staticBinding.IndexBuffer : frameContext.DynamicIndexBuffer;
            BindTriangleGeometryIfNeeded(
                frameContext.CommandBuffer,
                vertexAddress,
                indexBuffer,
                frameContext.ProfileEnabled,
                ref bufferBindingState);

            if (classification.IsTriangleCommand)
            {
                DrawTriangleCommand(
                    frameContext.CommandBuffer,
                    in command,
                    hasStaticBinding,
                    drawListContext.GlobalIndexOffset,
                    drawListContext.GlobalVertexOffset,
                    frameContext.ProfileEnabled,
                    ref drawDispatchState);
            }
            else
            {
                DrawExpandedStaticPrimitiveCommand(
                    frameContext.CommandBuffer,
                    in command,
                    in staticBinding,
                    frameContext.ProfileEnabled,
                    ref drawDispatchState);
            }

            return;
        }

        var primitiveAddress = hasStaticBinding ? staticBinding.PrimitiveAddress : frameContext.DynamicPrimitiveAddress;
        if (primitiveAddress is 0)
        {
            throw new InvalidOperationException($"Primitive draw command has no instance buffer address: {command.Kind}.");
        }

        PushPrimitiveAddressIfNeeded(
            frameContext.CommandBuffer,
            primitiveAddress,
            frameContext.ProfileEnabled,
            ref bufferBindingState);

        DrawPrimitiveInstanceCommand(
            frameContext.CommandBuffer,
            in command,
            hasStaticBinding,
            in staticBinding,
            drawListContext.GlobalPrimitiveOffset,
            drawListContext.DrawListRectPrimitiveCount,
            frameContext.ProfileEnabled,
            ref drawDispatchState);
    }

    private struct CommandDrawDispatchState
    {
        public int DrawCallCount;
        public long DrawCallTicks;
    }

    private void DrawTriangleCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        bool hasStaticBinding,
        uint globalIndexOffset,
        uint globalVertexOffset,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        var firstIndex = hasStaticBinding
            ? command.IndexOffset
            : command.IndexOffset + globalIndexOffset;
        var vertexOffsetCommand = hasStaticBinding
            ? (int)command.VertexOffset
            : (int)(command.VertexOffset + globalVertexOffset);
        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDrawIndexed(commandBuffer, command.ElementCount, 1, firstIndex, vertexOffsetCommand, 0);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private void DrawExpandedStaticPrimitiveCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        in StaticGeometryBuffer staticBinding,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        uint firstIndex;
        uint indexCount;
        if (command.Kind is UiDrawCommandKind.RectFilledPrimitives)
        {
            firstIndex = checked((uint)(staticBinding.RectExpandedIndexBase + (int)command.IndexOffset * 6));
            indexCount = checked(command.ElementCount * 6u);
        }
        else
        {
            var circleOffsets = staticBinding.CircleExpandedIndexOffsets
                ?? throw new InvalidOperationException("Static circle primitive command has no expanded index table.");
            var start = checked((int)command.IndexOffset);
            var end = checked(start + (int)command.ElementCount);
            if ((uint)start >= (uint)circleOffsets.Length || (uint)end >= (uint)circleOffsets.Length)
            {
                throw new InvalidOperationException("Static circle primitive command is outside the expanded index table.");
            }

            firstIndex = checked((uint)circleOffsets[start]);
            indexCount = checked((uint)(circleOffsets[end] - circleOffsets[start]));
        }

        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDrawIndexed(commandBuffer, indexCount, 1, firstIndex, 0, 0);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private void DrawPrimitiveInstanceCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        uint globalPrimitiveOffset,
        int drawListRectPrimitiveCount,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        var firstInstance = command.Kind is UiDrawCommandKind.RectFilledPrimitives
            ? (hasStaticBinding
                ? command.IndexOffset
                : command.IndexOffset + globalPrimitiveOffset)
            : (hasStaticBinding
                ? command.IndexOffset + (uint)staticBinding.RectPrimitiveCount
                : command.IndexOffset + globalPrimitiveOffset + (uint)drawListRectPrimitiveCount);
        var vertexCount = command.Kind is UiDrawCommandKind.RectFilledPrimitives
            ? 6u
            : command.VertexOffset * 3u;
        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDraw(commandBuffer, vertexCount, command.ElementCount, 0, firstInstance);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private static void RecordDrawCallProfile(
        bool profileEnabled,
        long drawCallStart,
        ref CommandDrawDispatchState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.DrawCallTicks += Stopwatch.GetTimestamp() - drawCallStart;
        state.DrawCallCount++;
    }

    private readonly struct CommandClassification
    {
        public readonly bool IsTriangleCommand;
        public readonly bool IsPrimitiveCommand;
        public readonly bool StaticPrimitiveUsesTriangleGeometry;
        public readonly bool IsFontCommand;

        public CommandClassification(
            bool isTriangleCommand,
            bool isPrimitiveCommand,
            bool staticPrimitiveUsesTriangleGeometry,
            bool isFontCommand)
        {
            IsTriangleCommand = isTriangleCommand;
            IsPrimitiveCommand = isPrimitiveCommand;
            StaticPrimitiveUsesTriangleGeometry = staticPrimitiveUsesTriangleGeometry;
            IsFontCommand = isFontCommand;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CommandClassification ClassifyCommand(
        in UiDrawCommand command,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding)
    {
        var isTriangleCommand = command.Kind is UiDrawCommandKind.Triangles;
        var isPrimitiveCommand = !isTriangleCommand;
        var staticPrimitiveUsesTriangleGeometry = hasStaticBinding
            && isPrimitiveCommand
            && staticBinding.HasExpandedPrimitiveGeometry;
        var isFontCommand = isTriangleCommand && IsFontTextureId(command.TextureId);

        return new CommandClassification(
            isTriangleCommand,
            isPrimitiveCommand,
            staticPrimitiveUsesTriangleGeometry,
            isFontCommand);
    }

    private struct CommandTextureState
    {
        public bool HasLastTexture;
        public UiTextureId LastTextureId;
        public bool LastTextureValid;
        public TextureResource LastTexture;
        public long LookupTicks;
    }

    private bool TryResolveCommandTexture(
        in UiDrawCommand command,
        in CommandClassification classification,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        bool profileEnabled,
        ref CommandFontDiagnosticState fontDiagnosticState,
        ref CommandTextureState state,
        out TextureResource texture)
    {
        var textureLookupStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        texture = default;

        if (!state.HasLastTexture || !command.TextureId.Equals(state.LastTextureId))
        {
            state.LastTextureId = command.TextureId;
            state.HasLastTexture = true;
            if (!_textures.TryGetValue(command.TextureId, out state.LastTexture))
            {
                EmitMissingTextureFontCommandDiagnosticIfNeeded(
                    in command,
                    in classification,
                    imageIndex,
                    listIndex,
                    commandIndex,
                    ref fontDiagnosticState);

                state.LastTextureValid = false;
                RecordCommandTextureLookupProfile(profileEnabled, textureLookupStart, ref state);
                return false;
            }

            state.LastTextureValid = true;
            texture = state.LastTexture;
        }
        else if (!state.LastTextureValid)
        {
            RecordCommandTextureLookupProfile(profileEnabled, textureLookupStart, ref state);
            return false;
        }
        else
        {
            texture = state.LastTexture;
        }

        RecordCommandTextureLookupProfile(profileEnabled, textureLookupStart, ref state);
        return true;
    }

    private static void RecordCommandTextureLookupProfile(
        bool profileEnabled,
        long textureLookupStart,
        ref CommandTextureState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.LookupTicks += Stopwatch.GetTimestamp() - textureLookupStart;
    }

    private const uint PrimitiveRectPayloadFlag = 0x80000000u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PrimitiveInstance CreateRectPrimitiveInstance(in UiRectFilledPrimitive primitive)
    {
        var rect = primitive.Rect;
        return new PrimitiveInstance
        {
            DataX = rect.X,
            DataY = rect.Y,
            DataZ = rect.Width,
            Payload = PrimitiveRectPayloadFlag | BitConverter.SingleToUInt32Bits(rect.Height),
            Color = primitive.Color.Rgba,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PrimitiveInstance CreateCirclePrimitiveInstance(in UiCircleFilledPrimitive primitive)
    {
        return new PrimitiveInstance
        {
            DataX = primitive.Center.X,
            DataY = primitive.Center.Y,
            DataZ = primitive.Radius,
            Payload = (uint)primitive.Segments,
            Color = primitive.Color.Rgba,
        };
    }
}

