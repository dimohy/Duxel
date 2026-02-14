#version 450
layout(location = 0) in vec2 fragUV;
layout(location = 0) out vec4 outColor;
layout(set = 0, binding = 0) uniform sampler2D sTexture;

void main()
{
    vec2 texel = 1.0 / vec2(textureSize(sTexture, 0));

    vec3 rgbM  = texture(sTexture, fragUV).rgb;
    vec3 rgbNW = texture(sTexture, fragUV + vec2(-texel.x, -texel.y)).rgb;
    vec3 rgbNE = texture(sTexture, fragUV + vec2( texel.x, -texel.y)).rgb;
    vec3 rgbSW = texture(sTexture, fragUV + vec2(-texel.x,  texel.y)).rgb;
    vec3 rgbSE = texture(sTexture, fragUV + vec2( texel.x,  texel.y)).rgb;

    vec3 luma = vec3(0.299, 0.587, 0.114);
    float lumaM  = dot(rgbM,  luma);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    float range = lumaMax - lumaMin;

    if (range < max(0.0312, lumaMax * 0.125))
    {
        outColor = vec4(rgbM, 1.0);
        return;
    }

    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * (1.0 / 8.0)), 1.0 / 128.0);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = clamp(dir * rcpDirMin, vec2(-8.0), vec2(8.0)) * texel;

    vec3 rgbA = 0.5 * (
        texture(sTexture, fragUV + dir * (1.0 / 3.0 - 0.5)).rgb +
        texture(sTexture, fragUV + dir * (2.0 / 3.0 - 0.5)).rgb
    );

    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        texture(sTexture, fragUV + dir * -0.5).rgb +
        texture(sTexture, fragUV + dir *  0.5).rgb
    );

    float lumaB = dot(rgbB, luma);
    outColor = (lumaB < lumaMin || lumaB > lumaMax) ? vec4(rgbA, 1.0) : vec4(rgbB, 1.0);
}
