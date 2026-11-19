import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { validateBrowserViewport } from "./BrowserReferenceContract.mjs";
import { captureBrowserReferences } from "./CaptureBrowserReferences.mjs";

validateBrowserViewport({ dpr: 1, rectangle: { x: 0, y: 0, width: 764, height: 372 } }, 764, 372);
assert.throws(() => validateBrowserViewport({ dpr: 2, rectangle: { x: 0, y: 0, width: 764, height: 372 } }, 764, 372),
    /BROWSER_REFERENCE_DPR_NOT_ONE/);
// Observed fresh browser capture: DPR is nominally one but CSS layout has a fractional zoom drift.
assert.throws(() => validateBrowserViewport({ dpr: .9999999779149107,
    rectangle: { x: 0, y: 0, width: 763.9939575195312, height: 371.997 } }, 764, 372),
    /BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED/);
assert.throws(() => validateBrowserViewport({ dpr: 1, rectangle: { x: .25, y: 0, width: 764, height: 372 } }, 764, 372),
    /BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED/);
const output = process.argv[2];
if (!output) throw Error("Specify a fresh capture rejection directory");
let screenshots = 0;
const page = {
    goto: async () => {},
    cdp: async method => { if (method === "Page.captureScreenshot") screenshots++; return {}; },
    evaluate: async () => ({ dpr: .9999999779149107,
        rectangle: { x: 0, y: 0, width: 763.9939575195312, height: 371.997 } })
};
await assert.rejects(captureBrowserReferences(page, fileURLToPath(new URL("./Fixtures/Browser", import.meta.url)), output),
    /BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED/);
assert.equal(screenshots, 0);
assert.deepEqual(await fs.readdir(output), []);
await fs.writeFile(path.join(output, "result.json"),
    JSON.stringify({ passed: 5, screenshotRequests: screenshots, actualConsumerRejectedBeforeCapture: true,
        browserPageMocked: true, viewportFromObservedBrowserCapture: true }, null, 2));
console.log("PASS browser viewport contract : 5 cases; actual capture consumer rejected before screenshot");
