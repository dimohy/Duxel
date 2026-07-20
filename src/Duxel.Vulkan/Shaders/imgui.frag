#version 450
#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;
layout(location = 2) in vec4 fragPrimitiveData;
layout(location = 3) in vec2 fragPrimitiveParams;
layout(location = 4) in vec4 fragBorderColor;
layout(location = 5) flat in uint fragPrimitiveKind;

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D uTextures[];

layout(push_constant) uniform PushConstants
{
    layout(offset = 40) uint texIndexAndMode;
} pc;

const uint SubpixelCoverageModeBit = 0x80000000u;
const uint TextureIndexMask = 0x7FFFFFFFu;

float RoundedRectDistance(vec2 point, vec2 halfSize, float radius)
{
    vec2 q = abs(point) - max(halfSize - vec2(radius), vec2(0.0));
    vec2 outside = max(q, vec2(0.0));
    return length(outside) + min(max(q.x, q.y), 0.0) - radius;
}

void main()
{
    // The push-constant index is dynamically uniform, so no nonuniformEXT is needed.
    vec4 texel = texture(uTextures[pc.texIndexAndMode & TextureIndexMask], fragUV);

    if (fragPrimitiveKind != 0u)
    {
        float distance = fragPrimitiveKind == 1u
            ? length(fragPrimitiveData.xy) - fragPrimitiveParams.x
            : RoundedRectDistance(fragPrimitiveData.xy, fragPrimitiveData.zw, fragPrimitiveParams.x);
        float aaWidth = max(fwidth(distance), 0.0001);
        float outerCoverage = clamp(0.5 - distance / aaWidth, 0.0, 1.0);
        float innerCoverage = outerCoverage;
        float borderWeight = 0.0;
        if (fragPrimitiveParams.y > 0.0)
        {
            innerCoverage = clamp(0.5 - (distance + fragPrimitiveParams.y) / aaWidth, 0.0, 1.0);
            borderWeight = max(outerCoverage - innerCoverage, 0.0);
        }

        vec4 fill = fragColor * texel;
        vec4 border = fragBorderColor * texel;
        float alpha = fill.a * innerCoverage + border.a * borderWeight;
        if (alpha <= 0.0)
        {
            discard;
        }

        vec3 premultiplied = fill.rgb * fill.a * innerCoverage
            + border.rgb * border.a * borderWeight;
        outColor = vec4(premultiplied, alpha);
        outBlendFactor = vec4(alpha);
    }
    else if ((pc.texIndexAndMode & SubpixelCoverageModeBit) != 0u)
    {
        // ClearType subpixel rendering with per-channel coverage.
        // Coverage values from DWrite ClearType represent per-subpixel opacity.
        vec3 coverage = texel.rgb * fragColor.a;
        float alphaCoverage = texel.a * fragColor.a;
        outColor = vec4(fragColor.rgb * coverage, alphaCoverage);
        outBlendFactor = vec4(coverage, alphaCoverage);
    }
    else
    {
        // Standard alpha blending expressed through dual-source factors:
        // premultiplied src.rgb with blendFactor = alpha reproduces
        // SrcAlpha/OneMinusSrcAlpha exactly under One/OneMinusSrc1Color.
        vec4 c = fragColor * texel;
        outColor = vec4(c.rgb * c.a, c.a);
        outBlendFactor = vec4(c.a);
    }
}
