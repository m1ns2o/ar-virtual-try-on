import { existsSync, readFileSync, statSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { MeshoptDecoder } from "meshoptimizer";

import { GARMENTS } from "../data/garments";

function readGlbJson(path: string) {
  const buffer = readFileSync(path);
  expect(buffer.toString("ascii", 0, 4)).toBe("glTF");
  expect(buffer.readUInt32LE(4)).toBe(2);
  const jsonLength = buffer.readUInt32LE(12);
  const jsonType = buffer.toString("ascii", 16, 20);
  expect(jsonType).toBe("JSON");
  return JSON.parse(buffer.toString("utf8", 20, 20 + jsonLength).trim()) as {
    extensionsUsed?: string[];
    materials?: Array<{ doubleSided?: boolean }>;
    meshes: Array<{ primitives: Array<{ attributes: Record<string, number> }> }>;
  };
}

describe("prepared web assets", () => {
  it("ships every garment as a Meshopt GLB with UVs and normals", () => {
    for (const garment of GARMENTS) {
      const path = resolve(process.cwd(), "public", garment.modelPath.replace(/^\//, "").replace(/^garments\//, "garments/"));
      expect(existsSync(path), garment.id).toBe(true);
      const gltf = readGlbJson(path);
      expect(gltf.extensionsUsed).toContain("EXT_meshopt_compression");
      expect(gltf.materials?.length ?? 0).toBeGreaterThan(0);
      expect(gltf.materials?.every((material) => material.doubleSided !== true)).toBe(true);
      const attributes = gltf.meshes.flatMap((mesh) => mesh.primitives.map((primitive) => primitive.attributes));
      expect(attributes.some((value) => "POSITION" in value)).toBe(true);
      expect(attributes.some((value) => "NORMAL" in value)).toBe(true);
      expect(attributes.some((value) => "TEXCOORD_0" in value)).toBe(true);
      if (garment.texturePath) {
        expect(existsSync(resolve(process.cwd(), "public", garment.texturePath.replace(/^\//, "")))).toBe(true);
      }
    }
  });

  it("decodes every compressed garment and exposes renderable geometry", async () => {
    await MeshoptDecoder.ready;
    const io = new NodeIO()
      .registerExtensions(ALL_EXTENSIONS)
      .registerDependencies({ "meshopt.decoder": MeshoptDecoder });
    for (const garment of GARMENTS) {
      const document = await io.read(resolve(process.cwd(), "public", garment.modelPath.replace(/^\//, "")));
      const primitives = document.getRoot().listMeshes().flatMap((mesh) => mesh.listPrimitives());
      expect(primitives.length).toBeGreaterThan(0);
      expect(primitives.every((primitive) => (primitive.getAttribute("POSITION")?.getCount() ?? 0) > 100)).toBe(true);
    }
  });

  it("self-hosts the MediaPipe model and WASM runtimes", () => {
    const model = resolve(process.cwd(), "public/models/pose_landmarker_lite.task");
    expect(statSync(model).size).toBeGreaterThan(4_000_000);
    for (const file of [
      "vision_wasm_internal.wasm",
      "vision_wasm_module_internal.wasm",
      "vision_wasm_nosimd_internal.wasm",
    ]) {
      expect(statSync(resolve(process.cwd(), "public/mediapipe/wasm", file)).size).toBeGreaterThan(1_000_000);
    }
  });
});
