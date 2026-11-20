#extension GL_EXT_buffer_reference : require
#extension GL_EXT_buffer_reference_uvec2 : require

// Unified GPU-driven vertex shader: pulls either UiVertex data (triangle mode)
// or packed primitive instances (primitive mode) from buffer-device-address
// storage. Triangle records are 5 dwords (20 bytes); primitive records are
// 8 dwords (32 bytes).
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
#ifdef DUXEL_CIRCLE_CUTOUT
    layout(offset = 44) int circleCutoutOffset;
#endif
    layout(offset = 48) mat4 layerTransform;
} pc;
layout(constant_id = 0) const bool EnableLayer3D = false;
layout(constant_id = 1) const bool EnableCompiledLayers = false;
layout(constant_id = 2) const bool EnableSharedBasis = false;
layout(constant_id = 3) const bool EnableSharedCamera = false;

layout(location = 0) out vec2 fragUV;
layout(location = 1) out vec4 fragColor;
layout(location = 2) out vec4 fragPrimitiveData;
layout(location = 3) out vec2 fragPrimitiveParams;
layout(location = 4) out vec4 fragBorderColor;
layout(location = 5) flat out uint fragPrimitiveKind;
layout(location = 6) out vec2 fragLocalPosition;
layout(location = 7) flat out vec4 fragLocalClip;
layout(location = 8) out float fragGradientPosition;
layout(location = 9) flat out vec4 fragGradientMiddleColor;
layout(location = 10) flat out float fragGradientMiddleStop;
#ifdef DUXEL_EXPLICIT_DEPTH_MASK
layout(location = 11) flat out mat3 depthSampleToShape;
#endif
#ifdef DUXEL_CIRCLE_CUTOUT
layout(location = 11) flat out vec3 fragCircleCutout;
layout(location = 12) flat out uvec2 fragPaintClipAddress;
#endif

const vec2 RectCorners[6] = vec2[](
    vec2(0.0, 0.0),
    vec2(1.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 1.0)
);

// Eight non-overlapping cells of a 3x3 grid, excluding the center. Stroke-only
// rounded rectangles use these cells so the fragment shader never shades the
// large, guaranteed-empty interior of a border.
const ivec2 BorderCells[8] = ivec2[](
    ivec2(0, 0), ivec2(1, 0), ivec2(2, 0),
    ivec2(0, 1),                 ivec2(2, 1),
    ivec2(0, 2), ivec2(1, 2), ivec2(2, 2)
);

float BorderCut(int cutIndex, float halfExtent, float bandExtent)
{
    if (cutIndex == 0)
    {
        return -halfExtent;
    }
    if (cutIndex == 1)
    {
        return -halfExtent + bandExtent;
    }
    if (cutIndex == 2)
    {
        return halfExtent - bandExtent;
    }
    return halfExtent;
}

#ifdef DUXEL_VIEWPORT_CLIP
out float gl_ClipDistance[4];

void WriteViewportClipDistances()
{
    vec4 distances = vec4(1.0);
    vec4 bounds = pc.layerTransform[2];
    if (EnableLayer3D && bounds.z > 0.0)
    {
        vec2 origin = floor(bounds.xy);
        vec2 extent = ceil(bounds.xy + bounds.zw) - origin;
        vec2 minNdc = 2.0 * (bounds.xy - origin) / extent - 1.0;
        vec2 maxNdc = 2.0 * (bounds.xy + bounds.zw - origin) / extent - 1.0;
        distances = vec4(gl_Position.xy - minNdc * gl_Position.w,
            maxNdc * gl_Position.w - gl_Position.xy);
    }
    gl_ClipDistance[0] = distances.x;
    gl_ClipDistance[1] = distances.y;
    gl_ClipDistance[2] = distances.z;
    gl_ClipDistance[3] = distances.w;
}
#endif

void main()
{
    uint vertexIndex = uint(gl_VertexIndex);
    uint instanceIndex = uint(gl_InstanceIndex);
    uint drawMode = pc.drawMode;
    float opacity = pc.opacity;
    vec2 translation = pc.translate;
    mat4 transform = pc.layerTransform;
    vec3 planeModel = vec3(0.0);
    GeometryData vertices = pc.vertices;
    GeometryData primitives = pc.primitives;
    fragLocalClip = vec4(0.0);
#ifdef DUXEL_CIRCLE_CUTOUT
    int cutoutOffset = 0;
    if (!EnableCompiledLayers) cutoutOffset = pc.circleCutoutOffset;
#endif
    if (EnableCompiledLayers)
    {
        // High eight bits select a draw descriptor; low sixteen bits retain
        // indexed vertex reuse. Indices stay within Vulkan's guaranteed 24 bits.
        uint descriptor = (vertexIndex >> 16u) * 16u;
        vertexIndex &= 65535u;
        vertices = GeometryData(uvec2(pc.vertices.data[descriptor], pc.vertices.data[descriptor + 1u]));
        primitives = GeometryData(uvec2(pc.vertices.data[descriptor + 2u], pc.vertices.data[descriptor + 3u]));
        drawMode = pc.vertices.data[descriptor + 6u];
        uint matrix = pc.vertices.data[descriptor + 7u] * 16u;
#ifdef DUXEL_CIRCLE_CUTOUT
        cutoutOffset = int(pc.vertices.data[descriptor + 11u]);
#endif
        if (drawMode != 0u)
        {
            uint vertexCount = pc.vertices.data[descriptor + 5u];
            instanceIndex = pc.vertices.data[descriptor + 4u] + vertexIndex / vertexCount;
            vertexIndex %= vertexCount;
        }
        opacity = uintBitsToFloat(pc.vertices.data[descriptor + 8u]);
        translation = vec2(uintBitsToFloat(pc.vertices.data[descriptor + 9u]), uintBitsToFloat(pc.vertices.data[descriptor + 10u]));
        fragLocalClip = uintBitsToFloat(uvec4(pc.vertices.data[descriptor + 12u], pc.vertices.data[descriptor + 13u], pc.vertices.data[descriptor + 14u], pc.vertices.data[descriptor + 15u]));
        if (EnableSharedCamera)
        {
            planeModel = uintBitsToFloat(uvec3(pc.primitives.data[matrix], pc.primitives.data[matrix + 1u], pc.primitives.data[matrix + 2u]));
        }
        else if (EnableSharedBasis)
        {
            // The first three columns are shared push constants. Replacing the
            // fourth column preserves the original matrix multiplication order.
            transform[3] = uintBitsToFloat(uvec4(pc.primitives.data[matrix + 12u], pc.primitives.data[matrix + 13u], pc.primitives.data[matrix + 14u], pc.primitives.data[matrix + 15u]));
        }
        else transform = mat4(
            uintBitsToFloat(uvec4(pc.primitives.data[matrix], pc.primitives.data[matrix + 1u], pc.primitives.data[matrix + 2u], pc.primitives.data[matrix + 3u])),
            uintBitsToFloat(uvec4(pc.primitives.data[matrix + 4u], pc.primitives.data[matrix + 5u], pc.primitives.data[matrix + 6u], pc.primitives.data[matrix + 7u])),
            uintBitsToFloat(uvec4(pc.primitives.data[matrix + 8u], pc.primitives.data[matrix + 9u], pc.primitives.data[matrix + 10u], pc.primitives.data[matrix + 11u])),
            uintBitsToFloat(uvec4(pc.primitives.data[matrix + 12u], pc.primitives.data[matrix + 13u], pc.primitives.data[matrix + 14u], pc.primitives.data[matrix + 15u])));
    }
#ifdef DUXEL_CIRCLE_CUTOUT
    fragCircleCutout = vec3(0.0);
    fragPaintClipAddress = uvec2(0u);
    if (cutoutOffset != 0)
    {
        // Signed byte offset from the current primitive base, using existing BDA support without shaderInt64.
        int delta = cutoutOffset - 1;
        uvec2 address = uvec2(primitives);
        uint low = address.x + uint(delta);
        uint high = address.y + (delta < 0 ? 0xffffffffu : 0u) + (low < address.x ? 1u : 0u);
        GeometryData mask = GeometryData(uvec2(low, high));
        if (uintBitsToFloat(mask.data[5]) < 0.0) fragPaintClipAddress = uvec2(low, high);
        else fragCircleCutout = uintBitsToFloat(uvec3(mask.data[0], mask.data[1], mask.data[2]));
    }
#endif
    vec2 pos;
#ifdef DUXEL_EXPLICIT_DEPTH_MASK
    mat3 planeToShape = mat3(1.0);
#endif
    fragPrimitiveData = vec4(0.0);
    fragPrimitiveParams = vec2(0.0);
    fragBorderColor = vec4(0.0);
    fragPrimitiveKind = 0u;
    fragGradientPosition = 0.0;
    fragGradientMiddleColor = vec4(0.0);
    fragGradientMiddleStop = 0.5;

    if (drawMode == 0u)
    {
        // Triangle mode: gl_VertexIndex already includes the draw's vertexOffset.
        uint base = vertexIndex * 5u;
        pos = vec2(uintBitsToFloat(vertices.data[base]), uintBitsToFloat(vertices.data[base + 1u]));
        fragUV = vec2(uintBitsToFloat(vertices.data[base + 2u]), uintBitsToFloat(vertices.data[base + 3u]));
        fragColor = unpackUnorm4x8(vertices.data[base + 4u]);
    }
    else
    {
        // Primitive mode: gl_InstanceIndex already includes firstInstance.
        uint base = instanceIndex * (drawMode >= 2u ? 16u : 8u);
        vec4 rect = vec4(
            uintBitsToFloat(primitives.data[base]),
            uintBitsToFloat(primitives.data[base + 1u]),
            uintBitsToFloat(primitives.data[base + 2u]),
            uintBitsToFloat(primitives.data[base + 3u]));
        float encodedRadius = uintBitsToFloat(primitives.data[base + 4u]);
        float borderThickness = uintBitsToFloat(primitives.data[base + 5u]);
        uint fillPacked = primitives.data[base + 6u];
        fragColor = unpackUnorm4x8(fillPacked);
        fragBorderColor = unpackUnorm4x8(primitives.data[base + 7u]);
        fragUV = vec2(0.5, 0.5);

        vec2 halfSize = rect.zw * 0.5;
        vec2 localPosition;
        bool strokeOnly = fillPacked == 0u && borderThickness > 0.0;
        if (strokeOnly)
        {
            int patchIndex = int(vertexIndex) / 6;
            int patchVertex = int(vertexIndex) % 6;
            ivec2 cell = BorderCells[patchIndex];
            vec2 bandExtent = min(
                halfSize,
                vec2(max(abs(encodedRadius), borderThickness) + 1.0));
            vec2 minimum = vec2(
                BorderCut(cell.x, halfSize.x, bandExtent.x),
                BorderCut(cell.y, halfSize.y, bandExtent.y));
            vec2 maximum = vec2(
                BorderCut(cell.x + 1, halfSize.x, bandExtent.x),
                BorderCut(cell.y + 1, halfSize.y, bandExtent.y));
            localPosition = mix(minimum, maximum, RectCorners[patchVertex]);
        }
        else
        {
            vec2 corner = RectCorners[vertexIndex];
            localPosition = (corner - vec2(0.5)) * rect.zw;
        }

        pos = rect.xy + halfSize + localPosition;
#ifdef DUXEL_EXPLICIT_DEPTH_MASK
        planeToShape[2].xy = -(rect.xy + halfSize);
#endif
        fragPrimitiveData = vec4(localPosition, halfSize);
        fragPrimitiveParams = vec2(abs(encodedRadius), borderThickness);
        fragPrimitiveKind = strokeOnly
            ? 3u
            : (encodedRadius < 0.0
            ? 1u
            : ((encodedRadius > 0.0 || borderThickness > 0.0) ? 2u : 0u));
        if (drawMode == 2u)
        {
            vec2 origin = uintBitsToFloat(uvec2(primitives.data[base + 8u], primitives.data[base + 9u]));
            vec2 direction = uintBitsToFloat(uvec2(primitives.data[base + 10u], primitives.data[base + 11u]));
            fragGradientPosition = dot(pos - origin, direction) / dot(direction, direction);
            fragGradientMiddleStop = uintBitsToFloat(primitives.data[base + 12u]);
            fragGradientMiddleColor = unpackUnorm4x8(primitives.data[base + 14u]);
            fragPrimitiveKind = 4u;
        }
        else if (drawMode == 3u)
        {
            vec2 uvOrigin = uintBitsToFloat(uvec2(primitives.data[base + 8u], primitives.data[base + 9u]));
            vec2 uvAxisX = uintBitsToFloat(uvec2(primitives.data[base + 10u], primitives.data[base + 11u]));
            vec2 uvAxisY = uintBitsToFloat(uvec2(primitives.data[base + 12u], primitives.data[base + 13u]));
            fragUV = uvOrigin + RectCorners[vertexIndex].x * uvAxisX + RectCorners[vertexIndex].y * uvAxisY;
            fragPrimitiveKind = 5u;
            vec2 contentSize = uintBitsToFloat(uvec2(primitives.data[base + 14u], primitives.data[base + 15u]));
            if (all(greaterThan(contentSize, vec2(0.0))))
            {
                fragPrimitiveData = vec4((fragUV - vec2(0.5)) * contentSize, contentSize * 0.5);
                fragPrimitiveKind = 6u;
#ifdef DUXEL_EXPLICIT_DEPTH_MASK
                planeToShape[0].xy = uvAxisX * contentSize / rect.z;
                planeToShape[1].xy = uvAxisY * contentSize / rect.w;
                planeToShape[2].xy = (uvOrigin - vec2(0.5)) * contentSize
                    - planeToShape[0].xy * rect.x - planeToShape[1].xy * rect.y;
#endif
            }
        }
    }

    if (opacity < 0.999999)
    {
        fragColor.a *= opacity;
        fragBorderColor.a *= opacity;
        fragGradientMiddleColor.a *= opacity;
    }

    vec2 clip = pos * pc.scale + translation;
    if (EnableSharedCamera) clip += planeModel.xy * pc.scale;
#ifdef DUXEL_EXPLICIT_DEPTH_MASK
    vec2 beforeProjection = translation;
    if (EnableSharedCamera) beforeProjection += planeModel.xy * pc.scale;
    vec4 origin = transform * vec4(beforeProjection, 0.0, 1.0);
    vec4 axisX = transform[0] * pc.scale.x;
    vec4 axisY = transform[1] * pc.scale.y;
    mat3 planeToClip = mat3(vec3(axisX.xy, axisX.w),
        vec3(axisY.xy, axisY.w), vec3(origin.xy, origin.w));
    depthSampleToShape = planeToShape * inverse(planeToClip);
#endif
    fragLocalPosition = pos;
    gl_Position = EnableLayer3D
        ? transform * vec4(clip, 0.0, 1.0)
        : vec4(clip, 0.0, 1.0);
    if (EnableSharedCamera) gl_Position.z += planeModel.z * gl_Position.w;
#ifdef DUXEL_VIEWPORT_CLIP
    WriteViewportClipDistances();
#endif
}
