#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;
layout(location = 2) in vec4 fragPrimitiveData;
layout(location = 3) in vec2 fragPrimitiveParams;
layout(location = 4) in vec4 fragBorderColor;
layout(location = 5) flat in uint fragPrimitiveKind;
layout(location = 6) in vec2 fragLocalPosition;
layout(location = 7) flat in vec4 fragLocalClip;
layout(location = 8) in float fragGradientPosition;
layout(location = 9) flat in vec4 fragGradientMiddleColor;
layout(location = 10) flat in float fragGradientMiddleStop;
#ifdef DUXEL_CIRCLE_CUTOUT
#extension GL_EXT_buffer_reference : require
#extension GL_EXT_buffer_reference_uvec2 : require
layout(buffer_reference, std430, buffer_reference_align = 4) readonly buffer PaintClipData { uint data[]; };
layout(location = 11) flat in vec3 fragCircleCutout;
layout(location = 12) flat in uvec2 fragPaintClipAddress;
#endif

layout(location = 0, index = 0) out vec4 outColor;
layout(location = 0, index = 1) out vec4 outBlendFactor;

layout(set = 0, binding = 0) uniform sampler2D uTextures[];

layout(push_constant) uniform PushConstants
{
    layout(offset = 40) uint texIndexAndMode;
    layout(offset = 112) vec4 localClip;
} pc;
layout(constant_id = 0) const bool EnableLayer3D = false;
layout(constant_id = 1) const bool EnableCompiledLayers = false;
layout(constant_id = 4) const bool EnableOpaqueCoverage = false;

const uint SubpixelCoverageModeBit = 0x80000000u;
const uint PremultipliedImageModeBit = 0x40000000u;
const uint TextureIndexMask = 0x3FFFFFFFu;

float RoundedRectDistance(vec2 point, vec2 halfSize, float radius)
{
    vec2 q = abs(point) - max(halfSize - vec2(radius), vec2(0.0));
    vec2 outside = max(q, vec2(0.0));
    return length(outside) + min(max(q.x, q.y), 0.0) - radius;
}

void main()
{
    vec4 localClip = EnableCompiledLayers ? fragLocalClip : pc.localClip;
#ifdef DUXEL_BACKDROP_COMPOSITE
    // Both indices are dynamically uniform. Existing bindless capacity is 4096.
    uint blackIndex = pc.texIndexAndMode & 0xFFFFu;
    uint whiteIndex = pc.texIndexAndMode >> 16;
    vec4 black = texture(uTextures[blackIndex], fragUV);
    vec4 white = texture(uTextures[whiteIndex], fragUV);
    vec4 coverage = clamp(vec4(1.0) - white + black, vec4(0.0), vec4(1.0));
    if (max(max(coverage.r, coverage.g), max(coverage.b, coverage.a)) <= 0.0) discard;
    // Black already contains premultiplied RGB, including independent LCD coverage.
    outColor = vec4(black.rgb * fragColor.rgb, black.a) * fragColor.a;
    outBlendFactor = coverage * fragColor.a;
#else
    // The push-constant index is dynamically uniform, so no nonuniformEXT is needed.
    vec4 texel = texture(uTextures[pc.texIndexAndMode & TextureIndexMask], fragUV);
    // Color images interpolate premultiplied texels. Decode after filtering for the shared tint/coverage path.
    if ((pc.texIndexAndMode & PremultipliedImageModeBit) != 0u)
        texel.rgb /= max(texel.a, 0.00000001);

    if (fragPrimitiveKind != 0u)
    {
        float distance = fragPrimitiveKind == 1u
            ? length(fragPrimitiveData.xy) - fragPrimitiveParams.x
            : RoundedRectDistance(fragPrimitiveData.xy, fragPrimitiveData.zw, fragPrimitiveParams.x);
        // Use local derivatives for consistent analytic edge coverage across drivers.
        float aaWidth = max(fwidthFine(distance), 0.0001);
        float outerCoverage = clamp(0.5 - distance / aaWidth, 0.0, 1.0);
        if (fragPrimitiveKind == 4u && fragPrimitiveParams.x == 0.0) outerCoverage = 1.0;
        if (fragPrimitiveKind == 5u || fragPrimitiveKind == 6u)
        {
            vec2 edge = min(fragUV, vec2(1.0) - fragUV);
            vec2 uvWidth = max(fwidth(fragUV), vec2(0.000001));
            outerCoverage = min(outerCoverage, clamp(0.5 + min(edge.x / uvWidth.x, edge.y / uvWidth.y), 0.0, 1.0));
        }
        float innerCoverage = outerCoverage;
        float borderWeight = 0.0;
        if (fragPrimitiveParams.y > 0.0)
        {
            innerCoverage = clamp(0.5 - (distance + fragPrimitiveParams.y) / aaWidth, 0.0, 1.0);
            borderWeight = max(outerCoverage - innerCoverage, 0.0);
        }

        vec4 fillColor = fragColor;
        if (fragPrimitiveKind == 4u)
        {
            float position = clamp(fragGradientPosition, 0.0, 1.0);
            fillColor = position <= fragGradientMiddleStop
                ? mix(fragColor, fragGradientMiddleColor, position / fragGradientMiddleStop)
                : mix(fragGradientMiddleColor, fragBorderColor, (position - fragGradientMiddleStop) / (1.0 - fragGradientMiddleStop));
        }
        vec4 fill = fillColor * texel;
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
        // Projected/scaled text cannot preserve a physical RGB subpixel grid.
        if (EnableLayer3D)
        {
            // Native ClearType masks may carry coverage only in RGB.
            alphaCoverage = dot(coverage, vec3(1.0 / 3.0));
            coverage = vec3(alphaCoverage);
        }
        if (max(max(coverage.r, coverage.g), max(coverage.b, alphaCoverage)) <= 0.0)
        {
            discard;
        }
        outColor = vec4(fragColor.rgb * coverage, alphaCoverage);
        outBlendFactor = vec4(coverage, alphaCoverage);
    }
    else
    {
        // Standard alpha blending expressed through dual-source factors:
        // premultiplied src.rgb with blendFactor = alpha reproduces
        // SrcAlpha/OneMinusSrcAlpha exactly under One/OneMinusSrc1Color.
        vec4 c = fragColor * texel;
        if (c.a <= 0.0)
        {
            discard;
        }
        outColor = vec4(c.rgb * c.a, c.a);
        outBlendFactor = vec4(c.a);
    }

#endif

#ifdef DUXEL_CIRCLE_CUTOUT
    if (any(notEqual(fragPaintClipAddress, uvec2(0u))))
    {
        PaintClipData mask = PaintClipData(fragPaintClipAddress);
        float coverage = 1.0;
        for (;;)
        {
            vec4 bounds = uintBitsToFloat(uvec4(mask.data[0], mask.data[1], mask.data[2], mask.data[3]));
            float kind = uintBitsToFloat(mask.data[5]);
            vec2 point = fragLocalPosition;
            if (kind <= -3.0)
            {
                vec4 basis = uintBitsToFloat(uvec4(mask.data[8], mask.data[9], mask.data[10], mask.data[11]));
                vec2 offset = uintBitsToFloat(uvec2(mask.data[12], mask.data[13]));
                point = vec2(point.x * basis.x + point.y * basis.z, point.x * basis.y + point.y * basis.w) + offset;
            }
            if (kind == -3.0)
            {
                if (any(lessThan(point, bounds.xy)) || any(greaterThanEqual(point, bounds.xy + bounds.zw)))
                {
                    coverage = 0.0;
                    break;
                }
            }
            else
            {
                bool inside = kind == -1.0 || kind == -4.0;
                float distance = inside
                    ? RoundedRectDistance(point - bounds.xy - bounds.zw * 0.5, bounds.zw * 0.5, uintBitsToFloat(mask.data[4]))
                    : length(point - bounds.xy) - bounds.z;
                float width = max(fwidth(distance), 0.0001);
                coverage = min(coverage, clamp(0.5 + (inside ? -distance : distance) / width, 0.0, 1.0));
            }
            int parent = kind < 0.0 ? int(mask.data[6]) : 0;
            if (parent == 0) break;
            // Parent links are strictly backward relative record indices, checked by the CPU.
            int delta = parent * 32;
            uvec2 address = uvec2(mask);
            uint low = address.x + uint(delta);
            uint high = address.y + 0xffffffffu + (low < address.x ? 1u : 0u);
            mask = PaintClipData(uvec2(low, high));
        }
        if (coverage <= 0.0) discard;
        outColor *= coverage;
        outBlendFactor *= coverage;
    }
    if (fragCircleCutout.z > 0.0)
    {
        float distance = length(fragLocalPosition - fragCircleCutout.xy) - fragCircleCutout.z;
        float coverage = clamp(0.5 + distance / max(fwidth(distance), 0.0001), 0.0, 1.0);
        if (coverage <= 0.0) discard;
        outColor *= coverage;
        outBlendFactor *= coverage;
    }

#endif
    // Keep clipped fragments available to the derivative group until coverage is evaluated.
    if (EnableLayer3D && (any(lessThan(fragLocalPosition, localClip.xy))
        || any(greaterThanEqual(fragLocalPosition, localClip.xy + localClip.zw))))
    {
        discard;
    }
    if (EnableOpaqueCoverage && outColor.a < 1.0)
    {
        // Location 0/index 0 alpha drives the hardware sample mask. Covered
        // samples write opaque RGB and depth; uncovered samples retain the back
        // plane. Keep alpha blending to preserve opaque framebuffer alpha.
        // Fully covered opaque samples already have straight color and factors
        // of one. Avoid their identity division; fractional edges still unpremultiply.
        outColor.rgb /= outColor.a;
        outBlendFactor.rgb = vec3(1.0);
    }
}
