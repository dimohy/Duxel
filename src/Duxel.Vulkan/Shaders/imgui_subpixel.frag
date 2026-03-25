#version 450

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D sTexture;

void main()
{
    vec4 texel = texture(sTexture, fragUV);

    // ClearType subpixel rendering with per-channel coverage.
    // Coverage values from DWrite ClearType represent per-subpixel opacity.
    vec3 coverage = texel.rgb * fragColor.a;
    float alphaCoverage = texel.a * fragColor.a;

    outColor = vec4(fragColor.rgb * coverage, alphaCoverage);
    outBlendFactor = vec4(coverage, alphaCoverage);
}
