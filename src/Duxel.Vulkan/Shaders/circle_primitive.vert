#version 450
layout(location = 0) in vec2 inCenter;
layout(location = 1) in float inRadius;
layout(location = 2) in vec4 inColor;
layout(location = 3) in uint inSegmentCount;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
} pc;

layout(location = 0) out vec4 fragColor;

const float Tau = 6.28318530717958647692;

void main()
{
    fragColor = inColor;

    int triangle = gl_VertexIndex / 3;
    int corner = gl_VertexIndex - triangle * 3;

    vec2 local = vec2(0.0, 0.0);
    if (corner != 0)
    {
        int segment = triangle + corner - 1;
        float angle = Tau * float(segment) / float(inSegmentCount);
        local = vec2(cos(angle), sin(angle));
    }

    vec2 pos = inCenter + local * inRadius;
    vec2 clip = pos * pc.scale + pc.translate;
    gl_Position = vec4(clip, 0.0, 1.0);
}
