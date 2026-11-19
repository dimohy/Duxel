export async function captureSvgImage(page, input) {
    const result = await page.evaluate(async ({ source, width, height, intrinsicWidth, intrinsicHeight }) => {
        const image = new Image(); image.src = "data:image/svg+xml;base64," + source; await image.decode();
        const scale = Math.min(width / intrinsicWidth, height / intrinsicHeight);
        const x = (width - intrinsicWidth * scale) / 2;
        const y = (height - intrinsicHeight * scale) / 2;
        function draw(background) {
            const canvas = document.createElement("canvas"); canvas.width = width; canvas.height = height;
            const context = canvas.getContext("2d", { willReadFrequently: false, colorSpace: "srgb" });
            if (background) { context.fillStyle = "#fff"; context.fillRect(0, 0, width, height); }
            context.drawImage(image, x, y, intrinsicWidth * scale, intrinsicHeight * scale);
            const data = context.getImageData(0, 0, width, height).data;
            let binary = "";
            for (let i = 0; i < data.length; i += 16384) binary += String.fromCharCode(...data.subarray(i, i + 16384));
            return { canvas, rgba: btoa(binary), png: canvas.toDataURL("image/png").split(",")[1] };
        }
        const transparent = draw(false);
        const white = draw(true);
        document.body.replaceChildren(white.canvas);
        return { rgba: white.rgba, png: white.png, transparentRgba: transparent.rgba,
            transparentPng: transparent.png, dpr: devicePixelRatio, width: white.canvas.width, height: white.canvas.height };
    }, input);
    if (Math.abs(result.dpr - 1) > .00001 || result.width !== input.width || result.height !== input.height)
        throw Error("BROWSER_IMAGE_REFERENCE_PIXEL_GRID_CHANGED");
    return result;
}
