vec4 over(vec4 foreground, vec4 background) {
    foreground = roundEven(clamp(foreground, 0.0, 1.0) * 255.0) / 255.0;
    return roundEven(clamp(foreground + background * (1.0 - foreground.a), 0.0, 1.0) * 255.0) / 255.0;
}
vec4 quantize(vec4 color) {
    return roundEven(clamp(color, 0.0, 1.0) * 255.0) / 255.0;
}
float shaderChannel(float color) {
    // Source precision and geometric coverage are combined before RGBA8 destination blending.
    float value = floor(color * 4096.0) / 4096.0;
    // One representable step encodes a downward half tie for the nearest-even destination.
    return value == .5 ? uintBitsToFloat(0x3effffffu) : value;
}
vec4 shaderColor(vec4 color) {
    return vec4(shaderChannel(color.r), shaderChannel(color.g), shaderChannel(color.b), shaderChannel(color.a));
}
vec4 layerSource(vec4 color, float opacity, float coverage) {
    return opacity == 1.0 ? color * coverage : shaderColor(color * opacity * coverage);
}
vec4 resolveSamples(vec4 a, vec4 b, vec4 c, vec4 d) {
    vec4 sum = round(a * 255.0) + round(b * 255.0) + round(c * 255.0) + round(d * 255.0);
    return floor((sum + 1.0) * .25) / 255.0;
}
