export function validateBrowserViewport(conditions, width, height) {
    if (!Number.isFinite(conditions.dpr) || Math.abs(conditions.dpr - 1) > .00001)
        throw Error("BROWSER_REFERENCE_DPR_NOT_ONE");
    const rectangle = conditions.rectangle;
    const tolerance = 1 / 65536;
    if (!rectangle || [rectangle.x, rectangle.y, rectangle.width, rectangle.height].some(value => !Number.isFinite(value))
        || Math.abs(rectangle.x) > tolerance || Math.abs(rectangle.y) > tolerance
        || Math.abs(rectangle.width - width) > tolerance || Math.abs(rectangle.height - height) > tolerance)
        throw Error("BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED: " + JSON.stringify({ width, height, rectangle }));
}
