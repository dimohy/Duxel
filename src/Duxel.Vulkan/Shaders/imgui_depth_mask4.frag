#version 450
#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;
layout(location = 2) in vec4 fragPrimitiveData;
layout(location = 3) in vec2 fragPrimitiveParams;
layout(location = 4) in vec4 fragBorderColor;
layout(location = 5) flat in uint fragPrimitiveKind;
layout(location = 6) in vec2 fragLocalPosition;
layout(location = 7) flat in vec4 fragLocalClip;
layout(location = 8) in float fragGradientPosition;
layout(location = 9) flat in vec4 fragGradientMiddleColor;
layout(location = 10) flat in float fragGradientMiddleStop;
layout(location = 11) flat in mat3 depthSampleToShape;
layout(location = 0, index = 0) out vec4 outColor;
#ifdef DUXEL_SINGLE_SOURCE_BLEND
vec4 outBlendFactor;
#else
layout(location = 0, index = 1) out vec4 outBlendFactor;
#endif
layout(set = 0, binding = 0) uniform sampler2D uTextures[];
layout(push_constant) uniform PushConstants
{
    layout(offset = 40) uint texIndexAndMode;
    layout(offset = 80) vec4 viewport;
    layout(offset = 112) vec4 localClip;
} pc;
layout(constant_id = 1) const bool EnableCompiledLayers = false;

bool SampleInside(vec3 point, vec2 inset, float radius)
{
    float denominator = abs(point.z);
    vec2 corner = max(abs(point.xy) - inset * denominator, vec2(0.0));
    float scaledRadius = radius * denominator;
    return dot(corner, corner) <= scaledRadius * scaledRadius;
}

int PrimitiveSampleMask()
{
    // Positive width carries fractional clip bounds. The viewport enclosing
    // them is the same integer rectangle used by the vertex clip distances.
    vec2 origin = pc.viewport.xy;
    vec2 extent = vec2(abs(pc.viewport.z), pc.viewport.w);
    if (pc.viewport.z > 0.0)
    {
        origin = floor(origin);
        extent = ceil(pc.viewport.xy + extent) - origin;
    }
    vec2 pixelToNdc = 2.0 / extent;
    vec2 center = floor(gl_FragCoord.xy) + vec2(0.5);
    float radius = fragPrimitiveParams.x;
    vec2 inset = fragPrimitiveKind == 1u ? vec2(0.0) : max(fragPrimitiveData.zw - radius, vec2(0.0));
    vec3 h = depthSampleToShape * vec3((center - origin) * pixelToNdc - 1.0, 1.0);

    // Vulkan standard 4× sample positions relative to the pixel center.
    vec3 x = depthSampleToShape[0] * (pixelToNdc.x * 0.125);
    vec3 y = depthSampleToShape[1] * (pixelToNdc.y * 0.125);
    vec3 offset03 = x + 3.0 * y;
    vec3 offset12 = 3.0 * x - y;
    return (SampleInside(h - offset03, inset, radius) ? 1 : 0)
        | (SampleInside(h + offset12, inset, radius) ? 2 : 0)
        | (SampleInside(h - offset12, inset, radius) ? 4 : 0)
        | (SampleInside(h + offset03, inset, radius) ? 8 : 0);
}

void main()
{
    // The renderer requires opaque, borderless analytic geometry and texture.
    vec4 localClip = EnableCompiledLayers ? fragLocalClip : pc.localClip;
    vec4 texel = texture(uTextures[pc.texIndexAndMode & 0x3fffffffu], fragUV);
    if ((pc.texIndexAndMode & 0x40000000u) != 0u)
        texel.rgb /= max(texel.a, 0.00000001);

    gl_SampleMask[0] = PrimitiveSampleMask();
    if (gl_SampleMask[0] == 0 || any(lessThan(fragLocalPosition, localClip.xy))
        || any(greaterThanEqual(fragLocalPosition, localClip.xy + localClip.zw)))
    {
        discard;
    }
    vec3 fill = fragColor.rgb;
    if (fragPrimitiveKind == 4u)
    {
        float position = clamp(fragGradientPosition, 0.0, 1.0);
        fill = position <= fragGradientMiddleStop
            ? mix(fill, fragGradientMiddleColor.rgb, position / fragGradientMiddleStop)
            : mix(fragGradientMiddleColor.rgb, fragBorderColor.rgb, (position - fragGradientMiddleStop) / (1.0 - fragGradientMiddleStop));
    }
    outColor = vec4(fill * texel.rgb, 1.0);
    outBlendFactor = vec4(1.0);
}
