vec2 conicPoint(vec2 a, vec2 b, vec2 c, vec3 weights, float t) {
    precise vec3 h0 = vec3(a * weights.x, weights.x);
    precise vec3 h1 = vec3(b * weights.y, weights.y);
    precise vec3 h2 = vec3(c * weights.z, weights.z);
    precise vec3 first = h0 * (1.0 - t) + h1 * t;
    precise vec3 last = h1 * (1.0 - t) + h2 * t;
    precise vec3 value = first * (1.0 - t) + last * t;
    return value.xy / value.z;
}
void conicDerivative(vec2 a, vec2 b, vec2 c, vec3 weights,
    out vec2 quadratic, out vec2 linear, out vec2 constant) {
    vec2 n0 = a * weights.x;
    vec2 n1 = 2.0 * (b * weights.y - n0);
    vec2 n2 = n0 - 2.0 * b * weights.y + c * weights.z;
    float d1 = 2.0 * (weights.y - weights.x);
    float d2 = weights.x - 2.0 * weights.y + weights.z;
    constant = n1 * weights.x - n0 * d1;
    linear = 2.0 * (n2 * weights.x - n0 * d2);
    quadratic = n2 * d1 - n1 * d2;
}
float conicRequired(vec2 a, vec2 b, vec2 c, float weight, float inverseTolerance) {
    vec2 center = (min(min(a, b), c) + max(max(a, b), c)) * .5;
    a -= center; b -= center; c -= center;
    float magnitude = sqrt(max(max(dot(a, a), dot(b, b)), dot(c, c)));
    vec2 difference = a - 2.0 * weight * b + c;
    float numerator = length(difference) * inverseTolerance + max(0.0, magnitude * inverseTolerance - 1.0) * abs(2.0 - 2.0 * weight);
    return sqrt(numerator / (4.0 * min(weight, 1.0)));
}
float conicSecondDerivativeBound(vec2 a, vec2 b, vec2 c, vec3 weights) {
    vec2 center = (min(min(a, b), c) + max(max(a, b), c)) * .5;
    a -= center; b -= center; c -= center;
    float minimum = min(weights.x, min(weights.y, weights.z));
    float maximum = max(weights.x, max(weights.y, weights.z));
    float magnitude = max(length(a), max(length(b), length(c))) * maximum;
    vec2 n0 = a * weights.x, n1 = b * weights.y, n2 = c * weights.z;
    float first = 2.0 * max(length(n1 - n0), length(n2 - n1));
    float second = 2.0 * length(n0 - 2.0 * n1 + n2);
    float d1 = 2.0 * max(abs(weights.y - weights.x), abs(weights.z - weights.y));
    float d2 = 2.0 * abs(weights.x - 2.0 * weights.y + weights.z);
    return second / minimum + (magnitude * d2 + 2.0 * first * d1) / (minimum * minimum)
        + 2.0 * magnitude * d1 * d1 / (minimum * minimum * minimum);
}
