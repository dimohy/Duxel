using System.IO;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
}
