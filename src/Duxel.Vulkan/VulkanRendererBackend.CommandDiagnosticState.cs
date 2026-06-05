using Duxel.Core;

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
            classification.IsFontCommand,
            classification.TriangleUsesColorPipeline,
            classification.PrimitiveUsesTexture);
        EmitCommandDiag(
            $"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={commandIndex} static={(hasStaticBinding ? 1 : 0)} pipe={pipeLabel} kind={command.Kind} tex={command.TextureId.Value} needsTex={(classification.CommandNeedsTexture ? 1 : 0)} elem={command.ElementCount} idxOff={command.IndexOffset} vtxOff={command.VertexOffset} clip=({command.ClipRect.X:0.###},{command.ClipRect.Y:0.###},{command.ClipRect.Width:0.###},{command.ClipRect.Height:0.###}) trans=({command.Translation.X:0.###},{command.Translation.Y:0.###}) opacity={command.Opacity:0.###}");
        state.EmittedCommandDiag++;
    }
}
