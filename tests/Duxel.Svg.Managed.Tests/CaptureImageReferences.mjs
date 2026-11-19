import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { captureSvgImage } from "./CaptureSvgImage.mjs";

const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex");

export async function captureImageReferences(page, fixtureDirectory, outputDirectory) {
    if (!path.isAbsolute(fixtureDirectory) || !path.isAbsolute(outputDirectory)) throw Error("Use absolute fixture/output paths");
    const manifest = JSON.parse(await fs.readFile(path.join(fixtureDirectory, "sources.json"), "utf8"));
    const inputs = [];
    for (const entry of manifest.sources) {
        const bytes = await fs.readFile(path.join(fixtureDirectory, entry.file));
        if (hash(bytes) !== entry.sourceSha256) throw Error("Original SVG bytes changed: " + entry.file);
        inputs.push({ entry, bytes });
    }
    await fs.mkdir(outputDirectory);
    await page.goto("about:blank");
    await page.cdp("Emulation.setDeviceMetricsOverride", { width: 1024, height: 768, deviceScaleFactor: 1, mobile: false });
    const references = [];
    for (const { entry, bytes } of inputs) {
        const source = bytes.toString("base64");
        const conditions = await page.evaluate(source => {
            const original = new DOMParser().parseFromString(new TextDecoder().decode(
                Uint8Array.from(atob(source), c => c.charCodeAt(0))), "image/svg+xml").documentElement;
            if (original.localName !== "svg") throw Error("Invalid SVG source");
            return { intrinsicWidth: original.width.baseVal.value, intrinsicHeight: original.height.baseVal.value,
                dpr: devicePixelRatio, userAgent: navigator.userAgent, surface: "Canvas2D backing pixels" };
        }, source);
        const image = await captureSvgImage(page, { source, width: entry.width, height: entry.height, ...conditions });
        const id = path.parse(entry.file).name + ".image-canvas";
        const rgba = Buffer.from(image.rgba, "base64"), transparent = Buffer.from(image.transparentRgba, "base64");
        if (rgba.length !== entry.width * entry.height * 4 || transparent.length !== rgba.length)
            throw Error("Browser reference byte count changed");
        for (const [suffix, data] of [[".browser.rgba", rgba], [".transparent.browser.rgba", transparent],
            [".browser.png", Buffer.from(image.png, "base64")], [".transparent.browser.png", Buffer.from(image.transparentPng, "base64")]])
            await fs.writeFile(path.join(outputDirectory, id + suffix), data);
        references.push({ ...entry, id, mode: "image-canvas", rgbaSha256: hash(rgba), transparentRgbaSha256: hash(transparent),
            conditions, background: "#ffffff", colorSpace: "srgb", capturedUtc: new Date().toISOString() });
    }
    await fs.writeFile(path.join(outputDirectory, "references.json"), JSON.stringify({
        renderer: "Chromium native SVG Image into GPU Canvas2D; independent readback",
        contract: "Original source hashes; fixed backing pixel extent; xMidYMid meet; DPR1/sRGB; white and transparent",
        references
    }, null, 2));
}
