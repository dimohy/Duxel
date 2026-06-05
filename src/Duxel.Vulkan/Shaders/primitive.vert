#version 450
layout(location = 0) in vec3 inData;
layout(location = 1) in uint inPayload;
layout(location = 2) in vec4 inColor;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
    float opacity;
} pc;

layout(location = 0) out vec4 fragColor;

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
    fragColor = inColor;
    if (pc.opacity < 0.999999)
    {
        fragColor.a *= pc.opacity;
    }

    vec2 pos;
    if ((inPayload & PrimitiveRectPayloadFlag) != 0u)
    {
        vec2 local = RectCorners[gl_VertexIndex];
        float height = uintBitsToFloat(inPayload & ~PrimitiveRectPayloadFlag);
        pos = inData.xy + local * vec2(inData.z, height);
    }
    else
    {
        uint segmentCount = inPayload;
        int triangle = gl_VertexIndex / 3;
        int corner = gl_VertexIndex - triangle * 3;

        vec2 local = vec2(0.0, 0.0);
        if (corner != 0)
        {
            int segment = triangle + corner - 1;
            float angle = Tau * float(segment) / float(segmentCount);
            local = vec2(cos(angle), sin(angle));
        }

        pos = inData.xy + local * inData.z;
    }

    vec2 clip = pos * pc.scale + pc.translate;
    gl_Position = vec4(clip, 0.0, 1.0);
}
