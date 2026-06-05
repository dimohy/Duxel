#version 450
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inUV;
layout(location = 2) in vec4 inColor;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
    float opacity;
} pc;

layout(location = 0) out vec2 fragUV;
layout(location = 1) out vec4 fragColor;

void main()
{
    fragUV = inUV;
    fragColor = inColor;
    if (pc.opacity < 0.999999)
    {
        fragColor.a *= pc.opacity;
    }
    vec2 pos = inPosition * pc.scale + pc.translate;
    gl_Position = vec4(pos, 0.0, 1.0);
}
