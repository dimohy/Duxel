#version 450
#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec4 fragColor;

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D uTextures[];

layout(push_constant) uniform PushConstants
{
    layout(offset = 20) uint texIndexAndMode;
} pc;

const uint TextureIndexMask = 0x7FFFFFFFu;

void main()
{
    // The push-constant index is dynamically uniform, so no nonuniformEXT is needed.
    vec4 sampled = texture(uTextures[pc.texIndexAndMode & TextureIndexMask], vec2(0.5, 0.5));

    // Standard alpha blending expressed through dual-source factors.
    vec4 c = fragColor * sampled;
    outColor = vec4(c.rgb * c.a, c.a);
    outBlendFactor = vec4(c.a);
}
