#version 450
layout(location = 0) in vec4 inRect;
layout(location = 1) in vec4 inColor;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
} pc;

layout(location = 0) out vec4 fragColor;

const vec2 corners[6] = vec2[](
    vec2(0.0, 0.0),
    vec2(1.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 1.0)
);

void main()
{
    vec2 local = corners[gl_VertexIndex];
    vec2 pos = inRect.xy + local * inRect.zw;
    vec2 clip = pos * pc.scale + pc.translate;
    fragColor = inColor;
    gl_Position = vec4(clip, 0.0, 1.0);
}
