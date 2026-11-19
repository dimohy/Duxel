import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";

// Run in the browser skill runtime; the caller owns page/task-space lifetime.
export async function captureColorStorageReference(page, outputDirectory, channel = "rgb") {
    if (channel !== "rgb" && channel !== "alpha")
        throw Error("COLOR_STORAGE_CHANNEL_UNSUPPORTED");
    if (!path.isAbsolute(outputDirectory))
        throw Error("Use an absolute fresh reference directory");
    await fs.mkdir(outputDirectory);
    await page.goto("about:blank");
    await page.cdp("Emulation.setDeviceMetricsOverride", {
        width: 1024, height: 768, deviceScaleFactor: 1, mobile: false
    });
    const result = await page.evaluate(channel => {
        const width = 129, height = 255;
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        document.body.replaceChildren(canvas);
        const gl = canvas.getContext("webgl2", {
            antialias: false, alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true
        });
        if (!gl)
            throw Error("WebGL2 unavailable");
        const compile = (kind, source) => {
            const shader = gl.createShader(kind);
            gl.shaderSource(shader, source);
            gl.compileShader(shader);
            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS))
                throw Error(gl.getShaderInfoLog(shader));
            return shader;
        };
        const vertex = compile(gl.VERTEX_SHADER, `#version 300 es
void main() {
    vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}`);
        const output = channel === "alpha" ? "vec4(0.0,0.0,0.0,value)" : "vec4(value,value,value,1.0)";
        const fragment = compile(gl.FRAGMENT_SHADER, `#version 300 es
precision highp float;
uniform float value;
out vec4 color;
void main() { color = ${output}; }`);
        const program = gl.createProgram();
        gl.attachShader(program, vertex);
        gl.attachShader(program, fragment);
        gl.linkProgram(program);
        if (!gl.getProgramParameter(program, gl.LINK_STATUS))
            throw Error(gl.getProgramInfoLog(program));
        gl.useProgram(program);
        const uniform = gl.getUniformLocation(program, "value");
        gl.disable(gl.DITHER);
        gl.disable(gl.BLEND);
        const values = new Float32Array(width * height);
        for (let base = 0; base < height; base++) {
            for (let offset = 0; offset < width; offset++) {
                const value = Math.fround((base + .5 + (offset - 64) / 1024) / 255);
                values[base * width + offset] = value;
                gl.viewport(offset, base, 1, 1);
                gl.uniform1f(uniform, value);
                gl.drawArrays(gl.TRIANGLES, 0, 3);
            }
        }
        const rgba = new Uint8Array(width * height * 4);
        gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, rgba);
        const error = gl.getError();
        if (error !== gl.NO_ERROR)
            throw Error("WebGL readback : " + error);
        const encode = bytes => {
            let binary = "";
            for (let i = 0; i < bytes.length; i += 16384)
                binary += String.fromCharCode(...bytes.subarray(i, i + 16384));
            return btoa(binary);
        };
        const extension = gl.getExtension("WEBGL_debug_renderer_info");
        return {
            width, height, ua: navigator.userAgent, dpr: devicePixelRatio,
            renderer: extension ? gl.getParameter(extension.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER),
            bits: [gl.getParameter(gl.RED_BITS), gl.getParameter(gl.GREEN_BITS), gl.getParameter(gl.BLUE_BITS), gl.getParameter(gl.ALPHA_BITS)],
            input: encode(new Uint8Array(values.buffer)), rgba: encode(rgba), capturedUtc: new Date().toISOString()
        };
    }, channel);
    if (Math.abs(result.dpr - 1) > .00001 || result.width !== 129 || result.height !== 255 || result.bits.some(value => value !== 8))
        throw Error("COLOR_STORAGE_CAPTURE_CONTRACT_CHANGED");
    const input = Buffer.from(result.input, "base64");
    const rgba = Buffer.from(result.rgba, "base64");
    await fs.writeFile(path.join(outputDirectory, "unorm-input.f32"), input);
    await fs.writeFile(path.join(outputDirectory, "unorm-output.rgba"), rgba);
    const { input: encodedInput, rgba: encodedRgba, ...metadata } = result;
    metadata.inputSha256 = crypto.createHash("sha256").update(input).digest("hex");
    metadata.rgbaSha256 = crypto.createHash("sha256").update(rgba).digest("hex");
    metadata.channel = channel;
    metadata.scope = "Independent WebGL float32 " + channel + " uniform to RGBA8 UNORM; no SVG grammar/shape qualification";
    await fs.writeFile(path.join(outputDirectory, "unorm-contract.json"), JSON.stringify(metadata, null, 2));
    return metadata;
}
