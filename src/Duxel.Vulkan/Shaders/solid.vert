#version 450
layout(location = 0) in vec2 inPosition;
layout(location = 2) in vec4 inColor;
layout(location = 3) in vec3 inPrimitiveData;
layout(location = 4) in uint inPrimitivePayload;
layout(location = 5) in vec4 inPrimitiveColor;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
    float opacity;
} pc;

layout(location = 0) out vec4 fragColor;

const float Tau = 6.28318530717958647692;
const uint SolidTrianglePayloadFlag = 0xFFFFFFFFu;
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
    vec4 color;
    if (inPrimitivePayload == SolidTrianglePayloadFlag)
    {
        pos = inPosition;
        color = inColor;
    }
    else if ((inPrimitivePayload & PrimitiveRectPayloadFlag) != 0u)
    {
        vec2 local = RectCorners[gl_VertexIndex];
        float height = uintBitsToFloat(inPrimitivePayload & ~PrimitiveRectPayloadFlag);
        pos = inPrimitiveData.xy + local * vec2(inPrimitiveData.z, height);
        color = inPrimitiveColor;
    }
    else
    {
        uint segmentCount = inPrimitivePayload;
        int triangle = gl_VertexIndex / 3;
        int corner = gl_VertexIndex - triangle * 3;

        vec2 local = vec2(0.0, 0.0);
        if (corner != 0)
        {
            int segment = triangle + corner - 1;
            float angle = Tau * float(segment) / float(segmentCount);
            local = vec2(cos(angle), sin(angle));
        }

        pos = inPrimitiveData.xy + local * inPrimitiveData.z;
        color = inPrimitiveColor;
    }

    if (pc.opacity < 0.999999)
    {
        color.a *= pc.opacity;
    }

    fragColor = color;
    vec2 clip = pos * pc.scale + pc.translate;
    gl_Position = vec4(clip, 0.0, 1.0);
}
