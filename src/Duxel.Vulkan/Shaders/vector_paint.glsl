vec4 paint(uint command, vec2 point) {
    vec4 specification = pc.scene.data[command + 1u];
    vec4 field = pc.scene.data[command + 2u];
    if (specification.z == 0.0) return field;
    vec4 parameters = pc.scene.data[command + 3u];
    vec4 inverse = pc.scene.data[command + 4u];
    vec4 translation = pc.scene.data[command + 5u];
    point = vec2(inverse.x * point.x + inverse.z * point.y, inverse.y * point.x + inverse.w * point.y) + translation.xy;
    if (specification.z == 3.0) {
        Dependency dependency = pc.dependencies.data[uint(parameters.x)];
        vec2 size = vec2(dependency.width, dependency.height);
        vec2 coordinate = fract((point - field.xy) / field.zw) * size - .5;
        ivec2 first = ivec2(floor(coordinate));
        vec2 weight = fract(coordinate);
        vec4 colors[4];
        for (uint sampleIndex = 0u; sampleIndex < 4u; ++sampleIndex) {
            ivec2 location = first + ivec2(int(sampleIndex & 1u), int(sampleIndex >> 1u));
            if (location.x < 0) location.x += int(dependency.width);
            if (location.y < 0) location.y += int(dependency.height);
            if (location.x >= int(dependency.width)) location.x -= int(dependency.width);
            if (location.y >= int(dependency.height)) location.y -= int(dependency.height);
            colors[sampleIndex] = unpackUnorm4x8(dependency.pixels.data[uint(location.y) * dependency.width + uint(location.x)]);
        }
        vec4 result = mix(mix(colors[0], colors[1], weight.x), mix(colors[2], colors[3], weight.x), weight.y);
        result.rgb /= max(result.a, 1e-8);
        return result;
    }
    if (specification.z == 5.0) {
        Dependency dependency = pc.dependencies.data[uint(parameters.x)];
        vec2 coordinate = (point - dependency.bounds.xy) / dependency.bounds.zw * vec2(dependency.width, dependency.height) - .5;
        ivec2 first = ivec2(floor(coordinate));
        vec2 weight = fract(coordinate);
        vec4 colors[4];
        for (uint sampleIndex = 0u; sampleIndex < 4u; ++sampleIndex) {
            ivec2 location = clamp(first + ivec2(int(sampleIndex & 1u), int(sampleIndex >> 1u)), ivec2(0), ivec2(dependency.width, dependency.height) - 1);
            colors[sampleIndex] = unpackUnorm4x8(dependency.pixels.data[uint(location.y) * dependency.width + uint(location.x)]);
        }
        vec4 result = mix(mix(colors[0], colors[1], weight.x), mix(colors[2], colors[3], weight.x), weight.y);
        result.rgb /= max(result.a, 1e-8);
        return result;
    }
    if (specification.z == 4.0) {
        vec2 dimensions = field.zw;
        if (parameters.w == 0.0) dimensions = parameters.xy * min(field.z / parameters.x, field.w / parameters.y);
        vec2 coordinate = (point - field.xy - (field.zw - dimensions) * .5) / dimensions * parameters.xy;
        if (any(lessThan(coordinate, vec2(0.0))) || any(greaterThanEqual(coordinate, parameters.xy))) return vec4(0.0);
        coordinate -= .5;
        ivec2 first = ivec2(floor(coordinate));
        vec2 weight = fract(coordinate);
        vec4 colors[4];
        for (uint sampleIndex = 0u; sampleIndex < 4u; ++sampleIndex) {
            ivec2 location = clamp(first + ivec2(int(sampleIndex & 1u), int(sampleIndex >> 1u)), ivec2(0), ivec2(parameters.xy) - 1);
            colors[sampleIndex] = pc.scene.data[uint(parameters.z) + uint(location.y) * uint(parameters.x) + uint(location.x)];
        }
        vec4 result = mix(mix(colors[0], colors[1], weight.x), mix(colors[2], colors[3], weight.x), weight.y);
        result.rgb /= max(result.a, 1e-8);
        return result;
    }
    float position;
    if (specification.z == 1.0) {
        vec2 direction = field.zw - field.xy;
        float lengthSquared = dot(direction, direction);
        // Linear stop selection follows the same row-stable layout as the scalar paint.
        const float LinearPositionBias = .00001;
        position = lengthSquared == 0.0 ? 1.0 : dot(point - field.xy, direction) / lengthSquared + LinearPositionBias;
    } else {
        vec2 ray = point - field.xy;
        vec2 focus = field.xy - field.zw;
        float a = dot(ray, ray);
        float b = 2.0 * dot(focus, ray);
        float c = dot(focus, focus) - parameters.x * parameters.x;
        float root = a == 0.0 ? 1e30 : (-b + sqrt(max(0.0, b * b - 4.0 * a * c))) / (2.0 * a);
        position = root <= 0.0 ? 1.0 : 1.0 / root;
    }
    uint spread = uint(parameters.w) & 3u;
    position = spread == 1u ? fract(position) : spread == 2u ? 1.0 - abs(mod(position, 2.0) - 1.0) : clamp(position, 0.0, 1.0);
    uint start = uint(parameters.y), count = uint(parameters.z);
    if (count == 0u) return vec4(0.0);
    vec4 previous = pc.scene.data[start + 1u];
    float previousOffset = pc.scene.data[start].x;
    if (position < previousOffset) return previous;
    for (uint i = 1u; i < count; ++i) {
        float nextOffset = pc.scene.data[start + 2u * i].x;
        vec4 next = pc.scene.data[start + 2u * i + 1u];
        if (position < nextOffset) return mix(previous, next, (position - previousOffset) / (nextOffset - previousOffset));
        previous = next;
        previousOffset = nextOffset;
    }
    return previous;
}
