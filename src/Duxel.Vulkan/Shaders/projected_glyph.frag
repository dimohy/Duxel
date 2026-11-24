#version 450
#extension GL_GOOGLE_include_directive : require
#extension GL_EXT_buffer_reference : require
#extension GL_EXT_buffer_reference_uvec2 : require
#include "vector_protocol.glsl"
#include "projected_glyph_winding.glsl"

layout(buffer_reference, std430, buffer_reference_align = 16) readonly buffer GlyphBatch {
    vec4 data[];
};
layout(location = 0) flat in mat3 clipToGlyphPlane;
layout(location = 3) flat in uvec2 glyphAddress;
layout(location = 4) flat in uint glyphCommand;
layout(location = 5) flat in vec3 glyphPlacement;
layout(location = 6) flat in uint glyphColor;
layout(location = 7) flat in vec4 glyphClip;
layout(location = 8) flat in uint useGlyphClip;
layout(location = 0, index = 0) out vec4 outColor;
#ifdef DUXEL_SINGLE_SOURCE_BLEND
vec4 outBlendFactor;
#else
layout(location = 0, index = 1) out vec4 outBlendFactor;
#endif
layout(constant_id = 0) const uint SampleCount = 1u;
layout(push_constant) uniform GlyphFragmentParameters {
    // The renderer's viewport metadata in the unused plane-transform row.
    layout(offset = 80) vec4 viewport;
    layout(offset = 112) vec4 localClip;
} pc;

const vec2 Standard4x[4] = vec2[](
    vec2(.375, .125), vec2(.875, .375), vec2(.125, .625), vec2(.625, .875));

int CoveredSamples(GlyphBatch batch, vec4 specification, vec4 placement, vec2 points[4], int eligibleMask)
{
    uint flags = uint(specification.w);
    uint first = uint(specification.x);
    uint count = uint(specification.y);
    bool cubic = (flags & 4u) != 0u;
    bool placed = (flags & PlacedCurvePath) != 0u;
    int winding[4] = int[4](0, 0, 0, 0);
    for (uint i = 0u; i < count; i++)
    {
        uint offset = first + i * (cubic ? 3u : 1u);
        vec4 ab = batch.data[offset];
        if (placed) ab = PlaceGlyphControls(ab, placement);
        vec4 cd = vec4(0.0);
        vec4 roots = vec4(0.0);
        if (cubic)
        {
            cd = batch.data[offset + 1u];
            if (placed) cd = PlaceGlyphControls(cd, placement);
            roots = batch.data[offset + 2u];
        }
        for (uint sampleIndex = 0u; sampleIndex < SampleCount; sampleIndex++)
        {
            if ((eligibleMask & (1 << sampleIndex)) == 0) continue;
            vec2 point = points[sampleIndex];
            winding[sampleIndex] += cubic ? GlyphCurveWinding(point, ab, cd, roots) : GlyphEdgeWinding(point, ab.xy, ab.zw);
        }
    }
    int mask = 0;
    for (uint sampleIndex = 0u; sampleIndex < SampleCount; sampleIndex++)
    {
        int windingAtSample = winding[sampleIndex];
        if ((flags & 1u) != 0u) windingAtSample &= 1;
        if (windingAtSample != 0) mask |= 1 << sampleIndex;
    }
    return mask;
}

void main()
{
    GlyphBatch batch = GlyphBatch(glyphAddress);
    vec4 specification = batch.data[glyphCommand + 1u];
    vec4 placement = batch.data[glyphCommand + 4u];
    vec2 origin = pc.viewport.xy;
    vec2 extent = vec2(abs(pc.viewport.z), pc.viewport.w);
    if (pc.viewport.z > 0.0)
    {
        origin = floor(origin);
        extent = ceil(pc.viewport.xy + extent) - origin;
    }
    vec2 pixel = floor(gl_FragCoord.xy);
    vec4 localClip = useGlyphClip != 0u ? glyphClip : pc.localClip;
    vec2 points[4];
    int eligibleMask = 0;
    for (uint i = 0u; i < SampleCount; i++)
    {
        if ((gl_SampleMaskIn[0] & (1 << i)) == 0) continue;
        vec2 samplePosition = pixel + (SampleCount == 4u ? Standard4x[i] : vec2(.5));
        if (pc.viewport.z > 0.0 && (any(lessThan(samplePosition, pc.viewport.xy))
            || any(greaterThanEqual(samplePosition, pc.viewport.xy + pc.viewport.zw))))
            continue;
        vec3 h = clipToGlyphPlane * vec3((samplePosition - origin) * (2.0 / extent) - 1.0, 1.0);
        if (h.z <= 0.0) continue;
        vec2 point = h.xy / h.z;
        vec2 local = glyphPlacement.xy + point * glyphPlacement.z;
        if (any(lessThan(local, localClip.xy)) || any(greaterThanEqual(local, localClip.xy + localClip.zw)))
            continue;
        points[i] = point;
        eligibleMask |= 1 << i;
    }
    if (eligibleMask == 0) discard;
    int mask = CoveredSamples(batch, specification, placement, points, eligibleMask);
    if (mask == 0) discard;
    gl_SampleMask[0] = mask;
    vec4 color = unpackUnorm4x8(glyphColor);
    outColor = vec4(color.rgb * color.a, color.a);
    outBlendFactor = vec4(color.a);
}
