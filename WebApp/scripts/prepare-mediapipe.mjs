import { copyFile, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(scriptDirectory, "..");
const packageWasm = resolve(webRoot, "node_modules", "@mediapipe", "tasks-vision", "wasm");
const publicWasm = resolve(webRoot, "public", "mediapipe", "wasm");

const files = [
  "vision_wasm_internal.js",
  "vision_wasm_internal.wasm",
  "vision_wasm_module_internal.js",
  "vision_wasm_module_internal.wasm",
  "vision_wasm_nosimd_internal.js",
  "vision_wasm_nosimd_internal.wasm",
];

await mkdir(publicWasm, { recursive: true });
for (const file of files) {
  await copyFile(resolve(packageWasm, file), resolve(publicWasm, file));
}

console.log(`Copied ${files.length} MediaPipe runtime files for self-hosting.`);
