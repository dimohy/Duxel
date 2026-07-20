#version 450
#extension GL_EXT_buffer_reference : require

// Unified GPU-driven vertex shader: pulls either UiVertex data (triangle mode)
// or packed primitive instances (primitive mode) from buffer-device-address
// storage. Triangle records are 5 dwords (20 bytes); primitive records are
// 8 dwords (32 bytes).
layout(buffer_reference, std430, buffer_reference_align = 4) readonly buffer GeometryData
{
    uint data[];
};

layout(push_constant) uniform PushConstants
{
    vec2 scale;              // offset 0
    vec2 translate;          // offset 8
    float opacity;           // offset 16
    uint drawMode;           // offset 20: 0 = indexed triangles, 1 = primitive instances
    GeometryData vertices;   // offset 24: UiVertex stream
    GeometryData primitives; // offset 32: PrimitiveInstance stream
} pc;

layout(location = 0) out vec2 fragUV;
layout(location = 1) out vec4 fragColor;
layout(location = 2) out vec4 fragPrimitiveData;
layout(location = 3) out vec2 fragPrimitiveParams;
layout(location = 4) out vec4 fragBorderColor;
layout(location = 5) flat out uint fragPrimitiveKind;

const vec2 RectCorners[6] = vec2[](
    vec2(0.0, 0.0),
    vec2(1.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 1.0)
);

// Eight non-overlapping cells of a 3x3 grid, excluding the center. Stroke-only
// rounded rectangles use these cells so the fragment shader never shades the
// large, guaranteed-empty interior of a border.
const ivec2 BorderCells[8] = ivec2[](
    ivec2(0, 0), ivec2(1, 0), ivec2(2, 0),
    ivec2(0, 1),                 ivec2(2, 1),
    ivec2(0, 2), ivec2(1, 2), ivec2(2, 2)
);

float BorderCut(int cutIndex, float halfExtent, float bandExtent)
{
    if (cutIndex == 0)
    {
        return -halfExtent;
    }
    if (cutIndex == 1)
    {
        return -halfExtent + bandExtent;
    }
    if (cutIndex == 2)
    {
        return halfExtent - bandExtent;
    }
    return halfExtent;
}

void main()
{
    vec2 pos;
    fragPrimitiveData = vec4(0.0);
    fragPrimitiveParams = vec2(0.0);
    fragBorderColor = vec4(0.0);
    fragPrimitiveKind = 0u;

    if (pc.drawMode == 0u)
    {
        // Triangle mode: gl_VertexIndex already includes the draw's vertexOffset.
        uint base = uint(gl_VertexIndex) * 5u;
        pos = vec2(uintBitsToFloat(pc.vertices.data[base]), uintBitsToFloat(pc.vertices.data[base + 1u]));
        fragUV = vec2(uintBitsToFloat(pc.vertices.data[base + 2u]), uintBitsToFloat(pc.vertices.data[base + 3u]));
        fragColor = unpackUnorm4x8(pc.vertices.data[base + 4u]);
    }
    else
    {
        // Primitive mode: gl_InstanceIndex already includes firstInstance.
        uint base = uint(gl_InstanceIndex) * 8u;
        vec4 rect = vec4(
            uintBitsToFloat(pc.primitives.data[base]),
            uintBitsToFloat(pc.primitives.data[base + 1u]),
            uintBitsToFloat(pc.primitives.data[base + 2u]),
            uintBitsToFloat(pc.primitives.data[base + 3u]));
        float encodedRadius = uintBitsToFloat(pc.primitives.data[base + 4u]);
        float borderThickness = uintBitsToFloat(pc.primitives.data[base + 5u]);
        uint fillPacked = pc.primitives.data[base + 6u];
        fragColor = unpackUnorm4x8(fillPacked);
        fragBorderColor = unpackUnorm4x8(pc.primitives.data[base + 7u]);
        fragUV = vec2(0.5, 0.5);

        vec2 halfSize = rect.zw * 0.5;
        vec2 localPosition;
        bool strokeOnly = fillPacked == 0u && borderThickness > 0.0;
        if (strokeOnly)
        {
            int patchIndex = gl_VertexIndex / 6;
            int patchVertex = gl_VertexIndex % 6;
            ivec2 cell = BorderCells[patchIndex];
            vec2 bandExtent = min(
                halfSize,
                vec2(max(abs(encodedRadius), borderThickness) + 1.0));
            vec2 minimum = vec2(
                BorderCut(cell.x, halfSize.x, bandExtent.x),
                BorderCut(cell.y, halfSize.y, bandExtent.y));
            vec2 maximum = vec2(
                BorderCut(cell.x + 1, halfSize.x, bandExtent.x),
                BorderCut(cell.y + 1, halfSize.y, bandExtent.y));
            localPosition = mix(minimum, maximum, RectCorners[patchVertex]);
        }
        else
        {
            vec2 corner = RectCorners[gl_VertexIndex];
            localPosition = (corner - vec2(0.5)) * rect.zw;
        }

        pos = rect.xy + halfSize + localPosition;
        fragPrimitiveData = vec4(localPosition, halfSize);
        fragPrimitiveParams = vec2(abs(encodedRadius), borderThickness);
        fragPrimitiveKind = strokeOnly
            ? 3u
            : (encodedRadius < 0.0
            ? 1u
            : ((encodedRadius > 0.0 || borderThickness > 0.0) ? 2u : 0u));
    }

    if (pc.opacity < 0.999999)
    {
        fragColor.a *= pc.opacity;
        fragBorderColor.a *= pc.opacity;
    }

    vec2 clip = pos * pc.scale + pc.translate;
    gl_Position = vec4(clip, 0.0, 1.0);
}
