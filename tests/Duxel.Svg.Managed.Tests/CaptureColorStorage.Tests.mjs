import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { captureColorStorageReference } from "./CaptureColorStorage.mjs";

const existingDirectory = fileURLToPath(new URL(".", import.meta.url));
const page = new Proxy({}, {
    get() {
        throw Error("A rejected capture must not access the browser");
    }
});

test("unsupported channels fail before browser or output operations", async () => {
    await assert.rejects(captureColorStorageReference(page, existingDirectory, "rgba"),
        /COLOR_STORAGE_CHANNEL_UNSUPPORTED/);
});

test("relative output paths fail before browser operations", async () => {
    await assert.rejects(captureColorStorageReference(page, "relative-result", "alpha"),
        /absolute fresh reference directory/);
});

test("an existing output is never overwritten or sent to the browser", async () => {
    assert.ok(path.isAbsolute(existingDirectory));
    await assert.rejects(captureColorStorageReference(page, existingDirectory), { code: "EEXIST" });
});
