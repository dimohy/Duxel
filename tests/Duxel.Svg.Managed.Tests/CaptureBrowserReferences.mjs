// Run through the ego-browser skill runtime. The caller owns the page and task-space lifecycle.
import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { validateBrowserViewport } from "./BrowserReferenceContract.mjs";
import { captureSvgImage } from "./CaptureSvgImage.mjs";

const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex");

export async function captureBrowserReferences(page, fixtureDirectory, outputDirectory) {
    if (!path.isAbsolute(fixtureDirectory) || !path.isAbsolute(outputDirectory)) throw Error("Use absolute fixture/output paths.");
    const sourceManifest = JSON.parse(await fs.readFile(path.join(fixtureDirectory, "sources.json"), "utf8"));
    const sources = [];
    for (const entry of sourceManifest.sources) {
        const bytes = await fs.readFile(path.join(fixtureDirectory, entry.file));
        if (hash(bytes) !== entry.sourceSha256) throw Error("Original SVG bytes changed: " + entry.file);
        sources.push({ entry, bytes });
    }
    // A fresh output prevents an interrupted capture from becoming the reference authority.
    await fs.mkdir(outputDirectory);
    await page.goto("about:blank");
    await page.cdp("Emulation.setDeviceMetricsOverride", { width: 1024, height: 768, deviceScaleFactor: 1, mobile: false });
    const references = [];
    for (const { entry, bytes } of sources) {
        const input = { source: bytes.toString("base64"), width: entry.width, height: entry.height };
        const conditions = await page.evaluate(({ source, width, height }) => {
            const text = new TextDecoder().decode(Uint8Array.from(atob(source), c => c.charCodeAt(0)));
            const original = new DOMParser().parseFromString(text, "image/svg+xml").documentElement;
            if (original.localName !== "svg") throw Error("Invalid SVG source");
            const intrinsicWidth = original.width.baseVal.value;
            const intrinsicHeight = original.height.baseVal.value;
            const ns = "http://www.w3.org/2000/svg";
            const outer = document.createElementNS(ns, "svg");
            outer.setAttribute("width", width);
            outer.setAttribute("height", height);
            outer.setAttribute("viewBox", `0 0 ${intrinsicWidth} ${intrinsicHeight}`);
            outer.setAttribute("preserveAspectRatio", "xMidYMid meet");
            outer.style.display = "block";
            const identifiers = new Set([...original.querySelectorAll("[id]")].map(node => node.id));
            let clipId = "__referenceViewport";
            while (identifiers.has(clipId)) clipId += "_";
            const defs = document.createElementNS(ns, "defs");
            const clip = document.createElementNS(ns, "clipPath");
            clip.id = clipId;
            const rectangle = document.createElementNS(ns, "rect");
            rectangle.setAttribute("width", intrinsicWidth);
            rectangle.setAttribute("height", intrinsicHeight);
            clip.append(rectangle); defs.append(clip); outer.append(defs);
            const group = document.createElementNS(ns, "g");
            group.setAttribute("clip-path", `url(#${clipId})`);
            group.append(document.importNode(original, true)); outer.append(group);
            document.body.style.cssText = "margin:0;background:#fff";
            document.body.replaceChildren(outer);
            // Normalize only the capture container; preserve every original SVG attribute and path.
            // Synchronous DOMRect reads force layout without background-tab animation-frame waits.
            const normalization = [];
            let scaleX = 1, scaleY = 1;
            for (let attempt = 0; attempt < 3; attempt++) {
                const measured = outer.getBoundingClientRect();
                normalization.push({ scaleX, scaleY, rectangle: measured.toJSON() });
                if (Math.abs(measured.width - width) <= 1 / 65536 && Math.abs(measured.height - height) <= 1 / 65536) break;
                if (!(measured.width > 0 && measured.height > 0)) throw Error("Invalid capture container size");
                scaleX *= width / measured.width;
                scaleY *= height / measured.height;
                outer.style.transformOrigin = "0 0";
                outer.style.transform = `scale(${scaleX},${scaleY})`;
            }
            return { intrinsicWidth, intrinsicHeight, dpr: devicePixelRatio, userAgent: navigator.userAgent,
                viewport: { width: innerWidth, height: innerHeight }, normalization, rectangle: outer.getBoundingClientRect().toJSON() };
        }, input);
        validateBrowserViewport(conditions, entry.width, entry.height);
        const screenshot = await page.cdp("Page.captureScreenshot", { format: "png", fromSurface: true,
            captureBeyondViewport: true, clip: { x: 0, y: 0, width: entry.width, height: entry.height, scale: 1 } });
        const directPng = Buffer.from(screenshot.data, "base64");
        const direct = await page.evaluate(async png => {
            const image = new Image(); image.src = "data:image/png;base64," + png; await image.decode();
            const canvas = document.createElement("canvas"); canvas.width = image.width; canvas.height = image.height;
            const context = canvas.getContext("2d", { willReadFrequently: false, colorSpace: "srgb" });
            context.drawImage(image, 0, 0);
            const data = context.getImageData(0, 0, image.width, image.height).data;
            let binary = "";
            for (let i = 0; i < data.length; i += 16384) binary += String.fromCharCode(...data.subarray(i, i + 16384));
            return { rgba: btoa(binary), width: image.width, height: image.height };
        }, screenshot.data);
        if (direct.width !== entry.width || direct.height !== entry.height) throw Error("Screenshot dimensions changed");
        const image = await captureSvgImage(page, { ...input, intrinsicWidth: conditions.intrinsicWidth, intrinsicHeight: conditions.intrinsicHeight });
        for (const [mode, png, data] of [["direct-svg", directPng, direct.rgba], ["image-canvas", Buffer.from(image.png, "base64"), image.rgba]]) {
            const id = path.parse(entry.file).name + "." + mode;
            const rgba = Buffer.from(data, "base64");
            await fs.writeFile(path.join(outputDirectory, id + ".browser.png"), png);
            await fs.writeFile(path.join(outputDirectory, id + ".browser.rgba"), rgba);
            const reference = { ...entry, id, mode, rgbaSha256: hash(rgba), conditions, background: "#ffffff",
                colorSpace: "srgb", capturedUtc: new Date().toISOString() };
            if (mode === "image-canvas") {
                const transparent = Buffer.from(image.transparentRgba, "base64");
                await fs.writeFile(path.join(outputDirectory, id + ".transparent.browser.rgba"), transparent);
                await fs.writeFile(path.join(outputDirectory, id + ".transparent.browser.png"), Buffer.from(image.transparentPng, "base64"));
                reference.transparentRgbaSha256 = hash(transparent);
            }
            references.push(reference);
        }
        console.log({ file: entry.file, modes: 2, sourceSha256: entry.sourceSha256 });
    }
    await fs.writeFile(path.join(outputDirectory, "references.json"), JSON.stringify({
        renderer: "Chromium native SVG; direct vector screenshot and independent SVG-image Canvas2D readback",
        contract: "Original root unchanged; xMidYMid meet into a fixed viewport; capture container normalized to measured pixel dimensions; intrinsic viewport clipped; DPR1/sRGB; white and transparent image outputs",
        references
    }, null, 2));
}
