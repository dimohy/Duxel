using System.Diagnostics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
}
