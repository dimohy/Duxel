#version 450
layout(location = 0) in vec4 fragColor;
layout(location = 0) out vec4 outColor;
layout(set = 0, binding = 0) uniform sampler2D sTexture;

void main()
{
    vec4 sampled = texture(sTexture, vec2(0.5, 0.5));
    outColor = fragColor * sampled;
}
