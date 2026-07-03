#version 450
#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D uTextures[];

layout(push_constant) uniform PushConstants
{
    layout(offset = 40) uint texIndexAndMode;
} pc;

const uint SubpixelCoverageModeBit = 0x80000000u;
const uint TextureIndexMask = 0x7FFFFFFFu;

void main()
{
    // The push-constant index is dynamically uniform, so no nonuniformEXT is needed.
    vec4 texel = texture(uTextures[pc.texIndexAndMode & TextureIndexMask], fragUV);

    if ((pc.texIndexAndMode & SubpixelCoverageModeBit) != 0u)
    {
        // ClearType subpixel rendering with per-channel coverage.
        // Coverage values from DWrite ClearType represent per-subpixel opacity.
        vec3 coverage = texel.rgb * fragColor.a;
        float alphaCoverage = texel.a * fragColor.a;
        outColor = vec4(fragColor.rgb * coverage, alphaCoverage);
        outBlendFactor = vec4(coverage, alphaCoverage);
    }
    else
    {
        // Standard alpha blending expressed through dual-source factors:
        // premultiplied src.rgb with blendFactor = alpha reproduces
        // SrcAlpha/OneMinusSrcAlpha exactly under One/OneMinusSrc1Color.
        vec4 c = fragColor * texel;
        outColor = vec4(c.rgb * c.a, c.a);
        outBlendFactor = vec4(c.a);
    }
}
