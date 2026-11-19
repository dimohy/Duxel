bool strokeOverflow = false;
bool containsStroke(vec2 point, vec4 specification) {
    StrokeTarget target = pc.strokes.data[uint(specification.y)];
    uint source = uint(specification.x) + 3u;
    int winding = 0;
    for (uint i = 0u; i < target.count; ++i) {
        vec4 bounds = pc.scene.data[source + i * 5u + 3u];
        if (any(lessThan(point, bounds.xy)) || any(greaterThan(point, bounds.zw))) continue;
        float length = target.edges.data[i].x;
        if (length < 0.0) { strokeOverflow = true; return false; }
        float height = bounds.w - bounds.y;
        uint band = height <= 0.0 ? 0u : uint(clamp(floor((point.y - bounds.y) / height * 64.0), 0.0, 63.0));
        vec4 range = target.edges.data[target.count + i * 64u + band];
        uint start = target.count * 65u + i * target.capacity;
        for (uint edgeIndex = uint(range.x); edgeIndex < uint(range.y); ++edgeIndex) {
            vec4 edge = target.edges.data[start + edgeIndex];
            winding += edgeWinding(point, edge.xy, edge.zw);
        }
    }
    return winding != 0;
}
