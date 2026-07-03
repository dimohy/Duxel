using Duxel.Core;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandDiagnosticState
    {
        public bool FrameEnabled;
        public int EmittedCommandDiag;
    }

    private CommandDiagnosticState CreateCommandDiagnosticState()
    {
        return new CommandDiagnosticState
        {
            FrameEnabled = ShouldEmitCommandDiagFrame(),
        };
    }

    private void EmitCommandDiagnosticIfNeeded(
        in UiDrawCommand command,
        in CommandClassification classification,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        bool hasStaticBinding,
        ref CommandDiagnosticState state)
    {
        const int maxCommandDiagPerPass = 512;
        if (!state.FrameEnabled || state.EmittedCommandDiag >= maxCommandDiagPerPass)
        {
            return;
        }

        var pipeLabel = GetCommandDiagPipelineLabel(
            command.Kind,
            classification.IsFontCommand);
        EmitCommandDiag(
            $"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} static={(hasStaticBinding ? 1 : 0)} pipe={pipeLabel} kind={command.Kind} tex={command.TextureId.Value} elem={command.ElementCount} idxOff={command.IndexOffset} vtxOff={command.VertexOffset} clip=({command.ClipRect.X:0.###},{command.ClipRect.Y:0.###},{command.ClipRect.Width:0.###},{command.ClipRect.Height:0.###}) trans=({command.Translation.X:0.###},{command.Translation.Y:0.###}) opacity={command.Opacity:0.###}");
        state.EmittedCommandDiag++;
    }

    private const int MaxFontCommandDiagPerPass = 256;

    private struct CommandFontDiagnosticState
    {
        public int EmittedCommandDiag;
    }

    private void EmitMissingTextureFontCommandDiagnosticIfNeeded(
        in UiDrawCommand command,
        in CommandClassification classification,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        ref CommandFontDiagnosticState state)
    {
        if (!_fontCommandDiagEnabled || state.EmittedCommandDiag >= MaxFontCommandDiagPerPass)
        {
            return;
        }

        EmitFontCommandDiag($"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} tex={command.TextureId.Value} isFont={classification.IsFontCommand} missingTexture=1 elem={command.ElementCount} idxOff={command.IndexOffset} vtxOff={command.VertexOffset}");
        state.EmittedCommandDiag++;
    }

    private void AnalyzeAndValidateFontCommandIfNeeded(
        UiDrawList drawList,
        in UiDrawCommand command,
        in CommandClassification classification,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        ref CommandFontDiagnosticState state)
    {
        if (!classification.IsTriangleCommand || !classification.IsFontCommand)
        {
            return;
        }

        EmitFontCommandDiagnosticIfNeeded(
            in command,
            imageIndex,
            listIndex,
            commandIndex,
            ref state);
        AnalyzeAndValidateFontCommandBounds(
            drawList,
            in command,
            imageIndex,
            listIndex,
            commandIndex,
            ref state);
    }

    private void EmitFontCommandDiagnosticIfNeeded(
        in UiDrawCommand command,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        ref CommandFontDiagnosticState state)
    {
        if (!_fontCommandDiagEnabled || state.EmittedCommandDiag >= MaxFontCommandDiagPerPass)
        {
            return;
        }

        EmitFontCommandDiag($"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} tex={command.TextureId.Value} isFont=1 elem={command.ElementCount} idxOff={command.IndexOffset} vtxOff={command.VertexOffset} clip=({command.ClipRect.X:0.###},{command.ClipRect.Y:0.###},{command.ClipRect.Width:0.###},{command.ClipRect.Height:0.###}) trans=({command.Translation.X:0.###},{command.Translation.Y:0.###})");
        state.EmittedCommandDiag++;
    }

    private void EmitFontCommandDiag(string message)
    {
        if (string.IsNullOrWhiteSpace(_fontCommandDiagLogPath))
        {
            Console.WriteLine($"[duxel-vk-font] {message}");
            return;
        }

        var path = _fontCommandDiagLogPath!;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (FontCommandDiagLogLock)
        {
            File.AppendAllText(path, $"[duxel-vk-font] {message}{Environment.NewLine}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldAnalyzeFontCommandBounds()
    {
        return _fontCommandDiagEnabled || _fontCommandBoundsAssertEnabled;
    }

    private void AnalyzeAndValidateFontCommandBounds(
        UiDrawList drawList,
        in UiDrawCommand command,
        uint imageIndex,
        int listIndex,
        int commandIndex,
        ref CommandFontDiagnosticState state)
    {
        if (!ShouldAnalyzeFontCommandBounds())
        {
            return;
        }

        var indexStart = (int)command.IndexOffset;
        var indexEndExclusive = indexStart + (int)command.ElementCount;
        var indexCount = drawList.Indices.Count;
        var vertexCount = drawList.Vertices.Count;
        var indexRangeInvalid = indexStart < 0 || indexEndExclusive < indexStart || indexEndExclusive > indexCount;

        var rawIndexMin = int.MaxValue;
        var rawIndexMax = int.MinValue;
        var invalidVertexRefCount = 0;
        var uvMinX = float.PositiveInfinity;
        var uvMinY = float.PositiveInfinity;
        var uvMaxX = float.NegativeInfinity;
        var uvMaxY = float.NegativeInfinity;
        var uvOutOfRangeCount = 0;
        var scannedIndexCount = 0;

        if (!indexRangeInvalid)
        {
            for (var i = indexStart; i < indexEndExclusive; i++)
            {
                var localVertexIndex = (int)drawList.Indices[i];
                scannedIndexCount++;
                if (localVertexIndex < rawIndexMin)
                {
                    rawIndexMin = localVertexIndex;
                }

                if (localVertexIndex > rawIndexMax)
                {
                    rawIndexMax = localVertexIndex;
                }

                if ((uint)localVertexIndex >= (uint)vertexCount)
                {
                    invalidVertexRefCount++;
                    continue;
                }

                var vertex = drawList.Vertices[localVertexIndex];
                var uv = vertex.UV;
                if (uv.X < uvMinX)
                {
                    uvMinX = uv.X;
                }

                if (uv.Y < uvMinY)
                {
                    uvMinY = uv.Y;
                }

                if (uv.X > uvMaxX)
                {
                    uvMaxX = uv.X;
                }

                if (uv.Y > uvMaxY)
                {
                    uvMaxY = uv.Y;
                }

                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f)
                {
                    uvOutOfRangeCount++;
                }
            }
        }

        var hasUvBounds = !float.IsPositiveInfinity(uvMinX) && !float.IsNegativeInfinity(uvMaxX);
        if ((_fontCommandDiagEnabled || _fontCommandBoundsAssertEnabled) && state.EmittedCommandDiag < MaxFontCommandDiagPerPass)
        {
            var uvBoundsText = hasUvBounds
                ? $"({uvMinX:0.######},{uvMinY:0.######})-({uvMaxX:0.######},{uvMaxY:0.######})"
                : "(n/a)";
            var rawIndexText = rawIndexMin <= rawIndexMax
                ? $"{rawIndexMin}..{rawIndexMax}"
                : "n/a";
            EmitFontCommandDiag(
                $"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} tex={command.TextureId.Value} isFont=1 bounds idxRange={indexStart}..{indexEndExclusive - 1} idxCount={indexCount} scanned={scannedIndexCount} rawIdx={rawIndexText} vtxCount={vertexCount} idxRangeInvalid={(indexRangeInvalid ? 1 : 0)} invalidVtxRef={invalidVertexRefCount} uvBounds={uvBoundsText} uvOutOfRange={uvOutOfRangeCount}");
            state.EmittedCommandDiag++;
        }

        if (_fontCommandBoundsAssertEnabled && (indexRangeInvalid || invalidVertexRefCount > 0 || uvOutOfRangeCount > 0))
        {
            throw new InvalidOperationException(
                $"Font draw command bounds validation failed: frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} tex={command.TextureId.Value} idxRangeInvalid={indexRangeInvalid} invalidVtxRef={invalidVertexRefCount} uvOutOfRange={uvOutOfRangeCount}");
        }
    }

    private struct CommandRecordProfileState
    {
        public int StaticDrawListCount;
        public int DynamicDrawListCount;
        public int StaticSecondaryCandidateDrawListCount;
        public int StaticSecondaryCandidateCommandCount;
        public int StaticSecondaryCandidateDrawCallCount;
        public int StaticCommandCount;
        public int DynamicCommandCount;
        public int StaticDrawCallCount;
        public int DynamicDrawCallCount;
        public int StaticPipelineBindCount;
        public int DynamicPipelineBindCount;
        public int StaticScissorComputeCount;
        public int DynamicScissorComputeCount;
        public int StaticScissorSetCount;
        public int DynamicScissorSetCount;
        public int StaticPushConstantCount;
        public int DynamicPushConstantCount;
        public int StaticGeometryBindCount;
        public int DynamicGeometryBindCount;
        public int StaticPrimitiveBindCount;
        public int DynamicPrimitiveBindCount;
        public int CommandCount;
        public int TriangleToPrimitiveTransitionCount;
        public int PrimitiveToTriangleTransitionCount;
        public int RectCircleTransitionCount;
        public int SchedulerProbeCount;
        public int SchedulerCacheHitCount;
        public int SchedulerCacheMissCount;
        public int SchedulerNoChangeCount;
        public int SchedulerScheduledListCount;
        public int SchedulerMergedCommandCount;
        public long SchedulerTicks;
        public UiDrawCommandKind LastRecordedKind;
        public bool HasLastRecordedKind;
    }

    private static void RecordDrawListProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        if (hasStaticBinding)
        {
            state.StaticDrawListCount++;
        }
        else
        {
            state.DynamicDrawListCount++;
        }
    }

    private static void RecordDrawListWorkProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        int commandCount,
        int drawCallCount,
        int pipelineBindCount,
        int scissorComputeCount,
        int scissorSetCount,
        int pushConstantCount,
        int geometryBindCount,
        int primitiveBindCount,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        if (hasStaticBinding)
        {
            state.StaticCommandCount += commandCount;
            state.StaticDrawCallCount += drawCallCount;
            state.StaticPipelineBindCount += pipelineBindCount;
            state.StaticScissorComputeCount += scissorComputeCount;
            state.StaticScissorSetCount += scissorSetCount;
            state.StaticPushConstantCount += pushConstantCount;
            state.StaticGeometryBindCount += geometryBindCount;
            state.StaticPrimitiveBindCount += primitiveBindCount;
        }
        else
        {
            state.DynamicCommandCount += commandCount;
            state.DynamicDrawCallCount += drawCallCount;
            state.DynamicPipelineBindCount += pipelineBindCount;
            state.DynamicScissorComputeCount += scissorComputeCount;
            state.DynamicScissorSetCount += scissorSetCount;
            state.DynamicPushConstantCount += pushConstantCount;
            state.DynamicGeometryBindCount += geometryBindCount;
            state.DynamicPrimitiveBindCount += primitiveBindCount;
        }
    }

    private static void RecordStaticSecondaryCandidateProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        int recordedCommandCount,
        int recordedDrawCallCount,
        int minCommandCount,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled
            || !hasStaticBinding
            || recordedCommandCount < minCommandCount)
        {
            return;
        }

        state.StaticSecondaryCandidateDrawListCount++;
        state.StaticSecondaryCandidateCommandCount += recordedCommandCount;
        state.StaticSecondaryCandidateDrawCallCount += recordedDrawCallCount;
    }

    private static long BeginCommandSchedulerProfile(bool profileEnabled)
    {
        return profileEnabled ? Stopwatch.GetTimestamp() : 0;
    }

    private static void RecordCommandSchedulerProfile(
        CommandScheduleResult scheduleResult,
        long schedulerStart,
        bool profileEnabled,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.SchedulerTicks += Stopwatch.GetTimestamp() - schedulerStart;
        switch (scheduleResult)
        {
            case CommandScheduleResult.CacheHit:
                state.SchedulerProbeCount++;
                state.SchedulerCacheHitCount++;
                state.SchedulerScheduledListCount++;
                break;
            case CommandScheduleResult.CacheMiss:
                state.SchedulerProbeCount++;
                state.SchedulerCacheMissCount++;
                state.SchedulerScheduledListCount++;
                break;
            case CommandScheduleResult.NoChange:
                state.SchedulerProbeCount++;
                state.SchedulerNoChangeCount++;
                break;
        }
    }

    private static void RecordMergedScheduledCommandProfile(
        bool profileEnabled,
        int mergedCommandCount,
        ref CommandRecordProfileState state)
    {
        if (profileEnabled && mergedCommandCount > 0)
        {
            state.SchedulerMergedCommandCount += mergedCommandCount;
        }
    }

    private static void RecordCommandProfile(
        in UiDrawCommand command,
        in CommandClassification classification,
        bool profileEnabled,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.CommandCount++;
        if (state.HasLastRecordedKind)
        {
            var lastWasTriangle = state.LastRecordedKind is UiDrawCommandKind.Triangles;
            if (lastWasTriangle && classification.IsPrimitiveCommand)
            {
                state.TriangleToPrimitiveTransitionCount++;
            }
            else if (!lastWasTriangle && classification.IsTriangleCommand)
            {
                state.PrimitiveToTriangleTransitionCount++;
            }
            else if (!lastWasTriangle
                && classification.IsPrimitiveCommand
                && state.LastRecordedKind != command.Kind)
            {
                state.RectCircleTransitionCount++;
            }
        }

        state.LastRecordedKind = command.Kind;
        state.HasLastRecordedKind = true;
    }
}

