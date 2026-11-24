#version 450
#extension GL_EXT_buffer_reference : require
#extension GL_EXT_buffer_reference_uvec2 : require

layout(buffer_reference, std430, buffer_reference_align = 16) readonly buffer GlyphBatch {
    vec4 data[];
};
struct GlyphRun {
    uvec4 placement;
    uvec2 address;
    uint first;
    uint reserved;
};
layout(buffer_reference, std430, buffer_reference_align = 16) readonly buffer GlyphRuns {
    GlyphRun data[];
};
struct GlyphPlaneRun {
    GlyphRun glyph;
    vec4 translate;
    vec4 clip;
    mat4 transform;
};
layout(buffer_reference, std430, buffer_reference_align = 16) readonly buffer GlyphPlaneRuns {
    GlyphPlaneRun data[];
};
layout(push_constant) uniform GlyphVertexParameters {
    vec2 scale;
    vec2 translate;
    uvec4 placement;
    layout(offset = 32) GlyphBatch glyphs;
    layout(offset = 48) mat4 layerTransform;
} pc;
layout(location = 0) flat out mat3 clipToGlyphPlane;
layout(location = 3) flat out uvec2 glyphAddress;
layout(location = 4) flat out uint glyphCommand;
layout(location = 5) flat out vec3 glyphPlacement;
layout(location = 6) flat out uint glyphColor;
layout(location = 7) flat out vec4 glyphClip;
layout(location = 8) flat out uint useGlyphClip;

const vec2 Corners[6] = vec2[](
    vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));

void main()
{
    uint instance = uint(gl_InstanceIndex);
    uvec4 parameters = pc.placement;
    GlyphBatch glyphs = pc.glyphs;
    mat4 transform = pc.layerTransform;
    vec2 translate = pc.translate;
    glyphClip = vec4(0.0);
    useGlyphClip = 0u;
    // Zero alpha is reserved for the batched opaque path. Each record
    // identifies the beginning of a run within the instanced draw.
    if (parameters.w == 0u)
    {
        bool planeBatch = parameters.y != 0u;
        GlyphRuns runs = GlyphRuns(uvec2(pc.glyphs));
        GlyphPlaneRuns planes = GlyphPlaneRuns(uvec2(pc.glyphs));
        uint lower = 0u;
        uint upper = parameters.x;
        while (upper - lower > 1u)
        {
            uint mid = (lower + upper) >> 1u;
            uint first = planeBatch ? planes.data[mid].glyph.first : runs.data[mid].first;
            if (first <= instance) lower = mid;
            else upper = mid;
        }
        GlyphRun run;
        if (planeBatch)
        {
            GlyphPlaneRun plane = planes.data[lower];
            run = plane.glyph;
            transform = plane.transform;
            translate = plane.translate.xy;
            glyphClip = plane.clip;
            useGlyphClip = 1u;
        }
        else
            run = runs.data[lower];
        parameters = run.placement;
        glyphs = GlyphBatch(run.address);
        instance -= run.first;
    }
    vec3 placement = uintBitsToFloat(parameters.xyz);
    glyphPlacement = placement;
    glyphColor = parameters.w;
    glyphCommand = instance * 6u;
    vec4 bounds = glyphs.data[glyphCommand];
    vec2 point = mix(bounds.xy, bounds.zw, Corners[gl_VertexIndex]);
    vec2 origin = placement.xy * pc.scale + translate;
    vec4 sceneOrigin = transform * vec4(origin, 0, 1);
    vec4 axisX = transform[0] * (placement.z * pc.scale.x);
    vec4 axisY = transform[1] * (placement.z * pc.scale.y);
    gl_Position = sceneOrigin + axisX * point.x + axisY * point.y;
    clipToGlyphPlane = inverse(mat3(vec3(axisX.xy, axisX.w),
        vec3(axisY.xy, axisY.w), vec3(sceneOrigin.xy, sceneOrigin.w)));
    glyphAddress = uvec2(glyphs);
}
