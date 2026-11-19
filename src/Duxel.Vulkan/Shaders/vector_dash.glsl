// Camera-dependent dash metrics operate on retained authored curves, never raster pixels.
struct DashCurve { vec4 ab; vec4 cd; bool cubic; vec3 weights; };
DashCurve dashStack[22];
vec2 dashParameters[22];
int dashTop;
uint dashCurveIndex, dashCurveCount, dashPath;
float dashTolerance;
DashCurve dashRead(uint path, uint index) {
    uint at = path + 1u + index * 3u;
    vec4 meta = pc.scene.data[at + 2u];
    return DashCurve(pc.scene.data[at], pc.scene.data[at + 1u], meta.x == 1.0,
        meta.x == 2.0 ? meta.yzw : vec3(0.0));
}
void dashSplit(DashCurve curve, float t, out DashCurve left, out DashCurve right) {
    if (curve.weights.x > 0.0) {
        vec3 a = vec3(curve.ab.xy * curve.weights.x, curve.weights.x);
        vec3 b = vec3(curve.ab.zw * curve.weights.y, curve.weights.y);
        vec3 c = vec3(curve.cd.zw * curve.weights.z, curve.weights.z);
        vec3 ab = a * (1.0 - t) + b * t;
        vec3 bc = b * (1.0 - t) + c * t;
        vec3 middle = ab * (1.0 - t) + bc * t;
        vec2 point = middle.xy / middle.z;
        left = DashCurve(vec4(curve.ab.xy, ab.xy / ab.z), vec4(point, point), false, vec3(a.z, ab.z, middle.z));
        right = DashCurve(vec4(point, bc.xy / bc.z), vec4(curve.cd.zw, curve.cd.zw), false, vec3(middle.z, bc.z, c.z));
        return;
    }
    precise vec2 a = curve.ab.xy * (1.0 - t) + curve.ab.zw * t;
    precise vec2 b = curve.ab.zw * (1.0 - t) + curve.cd.xy * t;
    precise vec2 c = curve.cd.xy * (1.0 - t) + curve.cd.zw * t;
    precise vec2 d = a * (1.0 - t) + b * t;
    precise vec2 e = b * (1.0 - t) + c * t;
    precise vec2 p = d * (1.0 - t) + e * t;
    if (!curve.cubic) p = curve.ab.xy * (1.0 - t) + curve.cd.zw * t;
    left = curve.cubic ? DashCurve(vec4(curve.ab.xy, a), vec4(d, p), true, vec3(0.0))
        : DashCurve(vec4(curve.ab.xy, curve.ab.xy), vec4(p, p), false, vec3(0.0));
    right = curve.cubic ? DashCurve(vec4(p, e), vec4(c, curve.cd.zw), true, vec3(0.0))
        : DashCurve(vec4(p, p), vec4(curve.cd.zw, curve.cd.zw), false, vec3(0.0));
}
DashCurve dashSlice(DashCurve curve, float first, float last) {
    DashCurve left, right;
    if (first != 0.0) { dashSplit(curve, first, left, right); curve = right; }
    if (last != 1.0) { dashSplit(curve, (last - first) / (1.0 - first), left, right); curve = left; }
    return curve;
}
vec2 dashTangent(DashCurve curve, bool end) {
    if (curve.weights.x > 0.0) {
        vec2 tangent = end ? curve.cd.zw - curve.ab.zw : curve.ab.zw - curve.ab.xy;
        return unitVector(all(equal(tangent, vec2(0.0))) ? curve.cd.zw - curve.ab.xy : tangent);
    }
    if (!curve.cubic) return unitVector(curve.cd.zw - curve.ab.xy);
    vec2 tangent = end ? curve.cd.zw - curve.cd.xy : curve.ab.zw - curve.ab.xy;
    if (all(equal(tangent, vec2(0.0)))) tangent = end ? curve.cd.zw - curve.ab.zw : curve.cd.xy - curve.ab.xy;
    if (all(equal(tangent, vec2(0.0)))) tangent = curve.cd.zw - curve.ab.xy;
    return unitVector(tangent);
}
void dashBegin(uint path) {
    dashTop = 0; dashCurveIndex = 0u;
    dashCurveCount = uint(pc.scene.data[path].x); dashPath = path;
}
bool dashNext(out uint curveIndex, out vec2 parameters, out vec2 first, out vec2 last) {
    for (uint step = 0u; step < 1000000u; ++step) {
        if (dashTop == 0) {
            if (dashCurveIndex >= dashCurveCount) return false;
            dashStack[0] = dashRead(dashPath, dashCurveIndex++);
            dashParameters[0] = vec2(0.0, 1.0); dashTop = 1;
        }
        --dashTop;
        DashCurve curve = dashStack[dashTop];
        vec2 interval = dashParameters[dashTop];
        precise vec2 chordA = curve.ab.xy * (1.0 - 1.0 / 3.0) + curve.cd.zw / 3.0;
        precise vec2 chordB = curve.ab.xy * (1.0 - 2.0 / 3.0) + curve.cd.zw * (2.0 / 3.0);
        vec2 error = max(abs(curve.ab.zw - chordA), abs(curve.cd.xy - chordB));
        if (curve.weights.x > 0.0) error = vec2(conicSecondDerivativeBound(curve.ab.xy, curve.ab.zw, curve.cd.zw, curve.weights) / 8.0);
        if ((!curve.cubic && curve.weights.x == 0.0) || max(error.x, error.y) <= dashTolerance) {
            if (all(equal(curve.ab.xy, curve.cd.zw))) continue;
            curveIndex = dashCurveIndex - 1u; parameters = interval;
            first = curve.ab.xy; last = curve.cd.zw; return true;
        }
        if (dashTop >= 20 || interval.y - interval.x <= 1.0 / 1048576.0) { overflow = true; return false; }
        DashCurve left, right; dashSplit(curve, .5, left, right);
        float middle = (interval.x + interval.y) * .5;
        dashStack[dashTop] = right; dashParameters[dashTop++] = vec2(middle, interval.y);
        dashStack[dashTop] = left; dashParameters[dashTop++] = vec2(interval.x, middle);
    }
    overflow = true; return false;
}
float dashLength(uint path) {
    dashBegin(path);
    uint curve; vec2 parameters, first, last;
    float distance = 0.0;
    while (dashNext(curve, parameters, first, last)) distance += length(last - first);
    return distance;
}
float dashCross(vec2 a, vec2 b) { return a.x * b.y - a.y * b.x; }
uint dashRoots(float a, float b, float c, out vec2 roots) {
    roots = vec2(0.0);
    if (a == 0.0) { if (b == 0.0) return 0u; roots.x = -c / b; return 1u; }
    float d = b * b - 4.0 * a * c;
    if (d < 0.0) return 0u;
    float q = -.5 * (b + (b < 0.0 ? -1.0 : 1.0) * sqrt(d));
    if (q == 0.0) { roots.x = -b / (2.0 * a); return 1u; }
    roots = vec2(q / a, c / q); return 2u;
}
void dashPatch(DashCurve curve) {
    vec2 first = dashTangent(curve, false), last = dashTangent(curve, true);
    float rotation = atan(dashCross(first, last), dot(first, last));
    float turn = dashCross(curve.cd.xy - curve.ab.xy, curve.cd.zw - curve.ab.zw);
    if (turn > 0.0 && rotation < 0.0) rotation += 6.283185307179586;
    if (turn < 0.0 && rotation > 0.0) rotation -= 6.283185307179586;
    emitPrimitive(curve.ab, curve.cd, curve.weights.x > 0.0 ? 7u : curve.cubic ? 0u : 1u, rotation,
        curve.weights.x > 0.0 ? vec4(curve.weights, 0.0) : vec4(first, last));
}
void dashCurve(DashCurve curve) {
    if (!curve.cubic && curve.weights.x == 0.0) { dashPatch(curve); return; }
    vec2 c = curve.ab.zw - curve.ab.xy, middle = curve.cd.xy - curve.ab.zw;
    vec2 b = middle - c, a = curve.cd.zw - curve.ab.xy - 3.0 * middle;
    if (curve.weights.x > 0.0) {
        conicDerivative(curve.ab.xy, curve.ab.zw, curve.cd.zw, curve.weights, a, b, c);
        b *= .5;
    }
    float qa = dashCross(a, b), qb = dashCross(a, c), qc = dashCross(b, c);
    vec2 roots; uint count;
    if (qa == 0.0 && qb == 0.0 && qc == 0.0) {
        float x = max(abs(a.x), max(abs(b.x), abs(c.x)));
        float y = max(abs(a.y), max(abs(b.y), abs(c.y)));
        count = x >= y ? dashRoots(a.x, 2.0 * b.x, c.x, roots) : dashRoots(a.y, 2.0 * b.y, c.y, roots);
    } else if (qb * qb - 4.0 * qa * qc < 0.0) {
        roots = vec2(qb == 0.0 ? -1.0 : -2.0 * qc / qb); count = qb == 0.0 ? 0u : 1u;
    } else count = dashRoots(qa, qb, qc, roots);
    if (count == 2u && roots.x > roots.y) roots = roots.yx;
    float previous = 0.0; vec2 priorTangent = vec2(0.0);
    for (uint i = 0u; i < count; ++i) {
        float cut = roots[i];
        if (cut < 1.0 / 2048.0 || cut >= 1.0 - 1.0 / 2048.0 || cut <= previous) continue;
        DashCurve left, right; dashSplit(curve, (cut - previous) / (1.0 - previous), left, right);
        if (dot(priorTangent, dashTangent(left, false)) < -.999)
            emitPrimitive(vec4(left.ab.xy, 0.0, 0.0), vec4(0.0), 2u, 0.0, vec4(0.0));
        dashPatch(left); priorTangent = dashTangent(left, true); curve = right; previous = cut;
    }
    if (dot(priorTangent, dashTangent(curve, false)) < -.999)
        emitPrimitive(vec4(curve.ab.xy, 0.0, 0.0), vec4(0.0), 2u, 0.0, vec4(0.0));
    dashPatch(curve);
}
void dashPolygon(vec2 a, vec2 b, vec2 c, vec2 d, uint count) {
    float area = dashCross(b - a, c - a) + (count == 4u ? dashCross(c - a, d - a) : 0.0);
    if (area < 0.0) { vec2 swap = a; a = count == 4u ? d : c; if (count == 4u) d = swap; else c = swap;
        if (count == 4u) { swap = b; b = c; c = swap; } }
    emitPrimitive(vec4(a, b), vec4(c, d), count == 4u ? 5u : 4u, 0.0, vec4(0.0));
}
void dashCap(vec2 point, vec2 tangent, uint cap) {
    if (cap == 1u) emitPrimitive(vec4(point, 0.0, 0.0), vec4(0.0), 2u, 0.0, vec4(0.0));
    if (cap == 2u) { vec2 n = normalVector(tangent), end = point + tangent * radius;
        dashPolygon(point + n, end + n, end - n, point - n, 4u); }
}
void dashJoin(vec2 point, vec2 first, vec2 last, uint join, float miter) {
    float cross = dashCross(first, last), cosine = clamp(dot(first, last), -1.0, 1.0);
    if (join == 1u) {
        if (cross == 0.0) { if (cosine < 0.0) dashCap(point, first, 1u); }
        else { float sign = cross > 0.0 ? -1.0 : 1.0;
            vec2 a = vec2(-first.y, first.x) * sign, b = vec2(-last.y, last.x) * sign;
            emitPrimitive(vec4(point, a), vec4(b, 0.0, 0.0), 3u, (cross < 0.0 ? -1.0 : 1.0) * acos(cosine), vec4(0.0)); }
    } else if (abs(cross) > .00001) {
        float sign = cross > 0.0 ? -1.0 : 1.0;
        vec2 a = vec2(-first.y, first.x) * (radius * sign), b = vec2(-last.y, last.x) * (radius * sign);
        vec2 sum = a + b, intersection = sum * (radius * radius / dot(sum, a));
        if (join == 0u && length(intersection) <= radius * miter)
            dashPolygon(point + a, point + intersection, point + b, point, 4u);
        else dashPolygon(point + a, point, point + b, vec2(0.0), 3u);
    }
}
void dashInterval(uint path, float start, float end, bool omitStart, bool omitEnd, uvec2 style, float miter) {
    dashBegin(path);
    uint curve, firstCurve = 0u, lastCurve = 0u; vec2 parameters, a, b;
    float distance = 0.0, firstT = 0.0, lastT = 1.0;
    bool found = false;
    while (dashNext(curve, parameters, a, b)) {
        float next = distance + length(b - a), length = next - distance;
        if (start <= next && end >= distance) {
            float low = clamp(parameters.x + (parameters.y - parameters.x) * (start - distance) / length, parameters.x, parameters.y);
            float high = clamp(parameters.x + (parameters.y - parameters.x) * (end - distance) / length, parameters.x, parameters.y);
            if (!found) { found = true; firstCurve = curve; firstT = low; }
            lastCurve = curve; lastT = high;
        }
        if (next >= end) break;
        distance = next;
    }
    if (!found) { // A zero-length drawn contour still has cap semantics.
        vec2 point = pc.scene.data[path].zw;
        dashCap(point, vec2(1.0, 0.0), style.x);
        if (style.x == 2u) dashCap(point, vec2(-1.0, 0.0), style.x);
        return;
    }
    if (start == end) {
        DashCurve dot = dashRead(path, firstCurve);
        vec2 point, tangent;
        if (dot.cubic || dot.weights.x > 0.0) evaluate(dot.ab, dot.cd, firstT, dot.weights, point, tangent);
        else { point = mix(dot.ab.xy, dot.cd.zw, firstT); tangent = dashTangent(dot, false); }
        dashCap(point, tangent, style.x);
        if (style.x == 2u) dashCap(point, -tangent, style.x);
        return;
    }
    bool firstPiece = true; vec2 previous = vec2(0.0), finalPoint = vec2(0.0);
    for (uint i = firstCurve; i <= lastCurve; ++i) {
        DashCurve piece = dashSlice(dashRead(path, i), i == firstCurve ? firstT : 0.0, i == lastCurve ? lastT : 1.0);
        if (all(equal(piece.ab.xy, piece.cd.zw)) && ((!piece.cubic && piece.weights.x == 0.0) || all(equal(piece.ab.xy, piece.ab.zw)) && all(equal(piece.ab.xy, piece.cd.xy)))) continue;
        vec2 tangent = dashTangent(piece, false);
        if (!firstPiece) dashJoin(piece.ab.xy, previous, tangent, style.y, miter);
        else if (!omitStart) dashCap(piece.ab.xy, -tangent, style.x);
        firstPiece = false; dashCurve(piece);
        previous = dashTangent(piece, true); finalPoint = piece.cd.zw;
    }
    if (!firstPiece && !omitEnd) dashCap(finalPoint, previous, style.x);
}
void emitDashes(vec4 specification) {
    uint metadata = sourceOffset + uint(specification.x), pathIndex = uint(specification.y);
    vec4 style = pc.scene.data[metadata], options = pc.scene.data[metadata + 1u];
    uint patternCount = uint(style.x), directory = metadata + 2u + patternCount;
    uint path = sourceOffset + uint(pc.scene.data[directory + pathIndex].x);
    dashTolerance = .5 / deviceScale;
    float total = dashLength(path), calibration = 1.0;
    if (options.y > 0.0) {
        float sum = 0.0;
        for (uint i = 0u; i < uint(options.z); ++i) sum += dashLength(sourceOffset + uint(pc.scene.data[directory + i].x));
        if (sum > 0.0) calibration = sum / options.y;
    }
    float period = 0.0;
    for (uint i = 0u; i < patternCount; ++i) period += pc.scene.data[metadata + 2u + i].x * calibration;
    float skipped = mod(mod(options.x * calibration, period) + period, period);
    uint initial = 0u;
    while (skipped > pc.scene.data[metadata + 2u + initial].x * calibration) {
        skipped -= pc.scene.data[metadata + 2u + initial].x * calibration;
        initial = (initial + 1u) % patternCount;
    }
    float initialRemaining = pc.scene.data[metadata + 2u + initial].x * calibration - skipped;
    bool wraps = false;
    if (pc.scene.data[path].y != 0.0) {
        float position = 0.0, remaining = initialRemaining; uint index = initial;
        bool firstAtOrigin = false, sawDash = false;
        for (uint step = 0u; step < 1000000u; ++step) {
            float end = position + min(remaining, total - position);
            if ((index & 1u) == 0u && !sawDash) { firstAtOrigin = position == 0.0; sawDash = true; }
            if (end >= total) { wraps = (index & 1u) == 0u; break; }
            if (end == position && remaining > 0.0) { overflow = true; return; }
            position = end; index = (index + 1u) % patternCount;
            remaining = pc.scene.data[metadata + 2u + index].x * calibration;
        }
        wraps = wraps && firstAtOrigin;
    }
    float position = 0.0, remaining = initialRemaining; uint index = initial;
    for (uint step = 0u; step < 1000000u; ++step) {
        float end = position + min(remaining, total - position);
        if ((index & 1u) == 0u) dashInterval(path, position, end, wraps && position == 0.0,
            wraps && end == total, uvec2(style.yz), style.w);
        if (end >= total) {
            if (wraps && uint(pc.scene.data[path].x) > 0u) {
                DashCurve first = dashRead(path, 0u), last = dashRead(path, uint(pc.scene.data[path].x) - 1u);
                dashJoin(first.ab.xy, dashTangent(last, true), dashTangent(first, false), uint(style.z), style.w);
            }
            return;
        }
        if (end == position && remaining > 0.0) { overflow = true; return; }
        position = end; index = (index + 1u) % patternCount;
        remaining = pc.scene.data[metadata + 2u + index].x * calibration;
    }
    overflow = true;
}
