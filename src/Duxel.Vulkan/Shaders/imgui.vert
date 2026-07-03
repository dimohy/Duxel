#version 450
#extension GL_EXT_buffer_reference : require

// Unified GPU-driven vertex shader: pulls either UiVertex data (triangle mode)
// or packed primitive instances (primitive mode) from buffer-device-address
// storage. Both records are 5 dwords (20 bytes).
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

const float Tau = 6.28318530717958647692;
const uint PrimitiveRectPayloadFlag = 0x80000000u;
const vec2 RectCorners[6] = vec2[](
    vec2(0.0, 0.0),
    vec2(1.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 1.0)
);

void main()
{
    vec2 pos;
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
        uint base = uint(gl_InstanceIndex) * 5u;
        vec3 data = vec3(
            uintBitsToFloat(pc.primitives.data[base]),
            uintBitsToFloat(pc.primitives.data[base + 1u]),
            uintBitsToFloat(pc.primitives.data[base + 2u]));
        uint payload = pc.primitives.data[base + 3u];
        fragColor = unpackUnorm4x8(pc.primitives.data[base + 4u]);
        fragUV = vec2(0.5, 0.5);

        if ((payload & PrimitiveRectPayloadFlag) != 0u)
        {
            vec2 local = RectCorners[gl_VertexIndex];
            float height = uintBitsToFloat(payload & ~PrimitiveRectPayloadFlag);
            pos = data.xy + local * vec2(data.z, height);
        }
        else
        {
            uint segmentCount = payload;
            int triangle = gl_VertexIndex / 3;
            int corner = gl_VertexIndex - triangle * 3;

            vec2 local = vec2(0.0, 0.0);
            if (corner != 0)
            {
                int segment = triangle + corner - 1;
                float angle = Tau * float(segment) / float(segmentCount);
                local = vec2(cos(angle), sin(angle));
            }

            pos = data.xy + local * data.z;
        }
    }

    if (pc.opacity < 0.999999)
    {
        fragColor.a *= pc.opacity;
    }

    vec2 clip = pos * pc.scale + pc.translate;
    gl_Position = vec4(clip, 0.0, 1.0);
}
