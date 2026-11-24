vec4 PlaceGlyphControls(vec4 controls, vec4 placement)
{
    precise vec4 positioned = (controls + placement.xyxy) - placement.zwzw;
    return positioned;
}

int GlyphEdgeWinding(vec2 point, vec2 a, vec2 b)
{
    if (point.y < min(a.y, b.y) || point.y >= max(a.y, b.y)) return 0;
    float x = a.x + (point.y - a.y) * (b.x - a.x) / (b.y - a.y);
    return x > point.x ? (b.y > a.y ? 1 : -1) : 0;
}

vec2 GlyphCurvePoint(vec4 ab, vec4 cd, float t)
{
    precise vec2 a = mix(ab.xy, ab.zw, t);
    precise vec2 b = mix(ab.zw, cd.xy, t);
    precise vec2 c = mix(cd.xy, cd.zw, t);
    precise vec2 p = mix(mix(a, b, t), mix(b, c, t), t);
    return p;
}

// A ray in glyph space stays valid under perspective because each sample is
// first mapped back to the glyph plane. Split at the original curve's Y extrema.
int GlyphCurveWinding(vec2 point, vec4 ab, vec4 cd, vec4 roots)
{
    if (roots.x == 0.0) return GlyphEdgeWinding(point, ab.xy, cd.zw);
    vec2 lower = min(min(ab.xy, ab.zw), min(cd.xy, cd.zw));
    vec2 upper = max(max(ab.xy, ab.zw), max(cd.xy, cd.zw));
    if (point.y < lower.y || point.y >= upper.y || point.x >= upper.x) return 0;
    // A cubic stays within its control hull. Allow for evaluation rounding
    // before using the net crossing from the two endpoints.
    float controlMagnitude = max(max(abs(lower.x), abs(upper.x)), 1e-30);
    precise float safeLowerX = lower.x - controlMagnitude * 3.814697265625e-6;
    if (point.x < safeLowerX)
    {
        float startY = ab.y;
        float endY = cd.w;
        // Oppositely directed crossings inside the hull cancel at each Y extremum.
        if (point.y >= min(startY, endY) && point.y < max(startY, endY))
            return endY > startY ? 1 : -1;
        return 0;
    }
    int winding = 0;
    uint splitCount = uint(roots.w);
    float start = 0.0;
    vec2 a = ab.xy;
    for (uint part = 0u; part <= splitCount; ++part)
    {
        float end = part < splitCount ? roots[1u + part] : 1.0;
        vec2 b = part < splitCount ? GlyphCurvePoint(ab, cd, end) : cd.zw;
        if (point.y >= min(a.y, b.y) && point.y < max(a.y, b.y))
        {
            float lo = start;
            float hi = end;
            bool increasing = b.y > a.y;
            // Stop once the parameter bracket has converged at float precision.
            for (uint step = 0u; step < 24u; ++step)
            {
                float mid = (lo + hi) * .5;
                if (mid == lo || mid == hi) break;
                if ((GlyphCurvePoint(ab, cd, mid).y > point.y) == increasing) hi = mid;
                else lo = mid;
            }
            if (GlyphCurvePoint(ab, cd, (lo + hi) * .5).x > point.x)
                winding += increasing ? 1 : -1;
        }
        start = end;
        a = b;
    }
    return winding;
}
