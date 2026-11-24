vec2 snapPoint(vec2 point, vec2 scale, vec2 origin) {
    return (roundEven((point * scale + origin) * 256.0) / 256.0 - origin) / scale;
}
int edgeWinding(vec2 point, vec2 a, vec2 b) {
    if (point.y < min(a.y, b.y) || point.y >= max(a.y, b.y)) return 0;
    float x = a.x + (point.y - a.y) / (b.y - a.y) * (b.x - a.x);
    return x > point.x ? (b.y > a.y ? 1 : -1) : 0;
}
vec2 curvePoint(vec4 ab, vec4 cd, uint index, uint count, vec2 scale, vec2 origin, float weight) {
    if (index == 0u) return snapPoint(ab.xy, scale, origin);
    if (index == count) return snapPoint(cd.zw, scale, origin);
    float t = float(index) / float(count);
    if (weight > 0.0) return snapPoint(conicPoint(ab.xy, ab.zw, cd.zw, vec3(1.0, weight, 1.0), t), scale, origin);
    precise vec2 a = ab.xy * (1.0 - t) + ab.zw * t;
    precise vec2 b = ab.zw * (1.0 - t) + cd.xy * t;
    precise vec2 c = cd.xy * (1.0 - t) + cd.zw * t;
    precise vec2 d = a * (1.0 - t) + b * t;
    precise vec2 e = b * (1.0 - t) + c * t;
    precise vec2 p = d * (1.0 - t) + e * t;
    return snapPoint(p, scale, origin);
}
// Search the camera-dependent uniform polyline in monotone Y intervals. Retained
// controls stay immutable; zoom requires neither CPU flattening nor buffer uploads.
vec4 placeCurve(vec4 controls, vec4 placement) {
    precise vec4 placed = (controls + placement.xyxy) - placement.zwzw;
    return placed;
}
int curveWinding(vec2 point, vec2 devicePoint, uint offset, vec2 scale, vec2 origin, bool placed, vec4 placement) {
    vec4 ab = pc.scene.data[offset], cd = pc.scene.data[offset + 1u];
    if (placed) {
        ab = placeCurve(ab, placement);
        cd = placeCurve(cd, placement);
    }
    vec4 intervals = pc.scene.data[offset + 2u];
    if (intervals.x == 0.0) {
        // Keep snapped line crossings in device space so a sample on the boundary stays a tie.
        precise vec2 deviceA = ab.xy * scale + origin;
        precise vec2 deviceB = cd.zw * scale + origin;
        vec2 a = roundEven(deviceA * 256.0) / 256.0;
        vec2 b = roundEven(deviceB * 256.0) / 256.0;
        if (devicePoint.y < min(a.y, b.y) || devicePoint.y >= max(a.y, b.y)) return 0;
        precise float x = a.x + (devicePoint.y - a.y) * (b.x - a.x) / (b.y - a.y);
        return x > devicePoint.x ? (b.y > a.y ? 1 : -1) : 0;
    }
    // Polynomial and positive-weight rational curves stay inside their control hull.
    // Leave room for device snapping and floating-point interpolation near the hull.
    vec2 minimum = min(min(ab.xy, ab.zw), min(cd.xy, cd.zw));
    vec2 maximum = max(max(ab.xy, ab.zw), max(cd.xy, cd.zw));
    vec2 guard = 1.0 / scale + max(abs(minimum), abs(maximum)) * 0.000001;
    if (point.y < minimum.y - guard.y || point.y > maximum.y + guard.y || point.x > maximum.x + guard.x)
        return 0;
    vec2 differenceA = (ab.xy - 2.0 * ab.zw + cd.xy) * scale;
    vec2 differenceB = (ab.zw - 2.0 * cd.xy + cd.zw) * scale;
    float required = sqrt(3.0 * max(length(differenceA), length(differenceB)));
    float weight = intervals.x < 0.0 ? -intervals.x : 0.0;
    if (weight > 0.0) required = conicRequired(ab.xy * scale, ab.zw * scale, cd.zw * scale, weight, 4.0);
    uint count = uint(exp2(max(0.0, ceil(log2(max(required, 1.0))))));
    uint cuts[6];
    uint cutCount = 1u;
    cuts[0] = 0u;
    for (uint root = 0u; root < uint(intervals.w); ++root) {
        float index = intervals[1u + root] * float(count);
        uint lower = uint(floor(index)), upper = uint(ceil(index));
        if (lower > cuts[cutCount - 1u]) cuts[cutCount++] = lower;
        if (upper > cuts[cutCount - 1u]) cuts[cutCount++] = upper;
    }
    if (count > cuts[cutCount - 1u]) cuts[cutCount++] = count;
    int winding = 0;
    for (uint part = 1u; part < cutCount; ++part) {
        uint low = cuts[part - 1u], high = cuts[part];
        vec2 a = curvePoint(ab, cd, low, count, scale, origin, weight);
        vec2 b = curvePoint(ab, cd, high, count, scale, origin, weight);
        if (point.y < min(a.y, b.y) || point.y >= max(a.y, b.y)) continue;
        bool increasing = b.y > a.y;
        while (high - low > 1u) {
            uint middle = low + (high - low) / 2u;
            vec2 p = curvePoint(ab, cd, middle, count, scale, origin, weight);
            if (increasing ? p.y > point.y : p.y <= point.y) high = middle;
            else low = middle;
        }
        winding += edgeWinding(point, curvePoint(ab, cd, low, count, scale, origin, weight), curvePoint(ab, cd, high, count, scale, origin, weight));
    }
    return winding;
}
bool containsStroke(vec2 point, vec4 specification);
bool containsPoint(vec2 point, vec2 devicePoint, vec4 bounds, vec4 specification, vec2 deviceScale, vec2 deviceOrigin, vec4 placement) {
    if ((uint(specification.w) & 32u) != 0u) return containsStroke(point, specification);
    uint offset = uint(specification.x), count = uint(specification.y);
    uint flags = uint(specification.w);
    bool placed = (flags & PlacedCurvePath) != 0u;
    if ((flags & 2u) != 0u) {
        uint band = min(uint((point.y - bounds.y) / (bounds.w - bounds.y) * float(count)), count - 1u);
        vec4 entry = pc.scene.data[offset + band];
        offset = uint(entry.x);
        count = uint(entry.y);
    }
    int winding = 0;
    for (uint i = 0u; i < count; ++i) {
        if ((flags & 4u) != 0u) {
            winding += curveWinding(point, devicePoint, offset + i * 3u, deviceScale, deviceOrigin, placed, placement);
            continue;
        }
        vec4 edge = pc.scene.data[offset + i];
        if (placed) edge = placeCurve(edge, placement);
        edge.xy = (roundEven((edge.xy * deviceScale + deviceOrigin) * 256.0) / 256.0 - deviceOrigin) / deviceScale;
        edge.zw = (roundEven((edge.zw * deviceScale + deviceOrigin) * 256.0) / 256.0 - deviceOrigin) / deviceScale;
        if (point.y < min(edge.y, edge.w) || point.y >= max(edge.y, edge.w)) continue;
        float x = edge.x + (point.y - edge.y) / (edge.w - edge.y) * (edge.z - edge.x);
        if (x > point.x) winding += edge.w > edge.y ? 1 : -1;
    }
    return (flags & 1u) != 0u ? (winding & 1) != 0 : winding != 0;
}
