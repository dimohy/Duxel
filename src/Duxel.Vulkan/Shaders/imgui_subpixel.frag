#version 450
#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D uTextures[];

layout(push_constant) uniform PushConstants
{
    layout(offset = 20) uint texIndex;
} pc;

void main()
{
    // The push-constant index is dynamically uniform, so no nonuniformEXT is needed.
    vec4 texel = texture(uTextures[pc.texIndex], fragUV);

    // ClearType subpixel rendering with per-channel coverage.
    // Coverage values from DWrite ClearType represent per-subpixel opacity.
    vec3 coverage = texel.rgb * fragColor.a;
    float alphaCoverage = texel.a * fragColor.a;

    outColor = vec4(fragColor.rgb * coverage, alphaCoverage);
    outBlendFactor = vec4(coverage, alphaCoverage);
}
