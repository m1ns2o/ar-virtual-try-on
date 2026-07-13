import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import obj2gltf from "obj2gltf";
import sharp from "sharp";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { dedup, meshopt, weld } from "@gltf-transform/functions";
import { MeshoptEncoder } from "meshoptimizer";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(scriptDirectory, "..");
const repositoryRoot = resolve(webRoot, "..");
const unityAssetRoot = resolve(repositoryRoot, "Assets", "ARCloset", "MakeHuman");
const garmentOutput = resolve(webRoot, "public", "garments");
const textureOutput = resolve(webRoot, "public", "textures");
await MeshoptEncoder.ready;
const io = new NodeIO()
  .registerExtensions(ALL_EXTENSIONS)
  .registerDependencies({ "meshopt.encoder": MeshoptEncoder });

function addSmoothNormals(document) {
  for (const mesh of document.getRoot().listMeshes()) {
    for (const primitive of mesh.listPrimitives()) {
      const positions = primitive.getAttribute("POSITION");
      if (!positions) continue;
      const indices = primitive.getIndices();
      const normals = new Float32Array(positions.getCount() * 3);
      const a = [0, 0, 0];
      const b = [0, 0, 0];
      const c = [0, 0, 0];
      const elementCount = indices ? indices.getCount() : positions.getCount();
      for (let element = 0; element < elementCount; element += 3) {
        const ia = indices ? indices.getScalar(element) : element;
        const ib = indices ? indices.getScalar(element + 1) : element + 1;
        const ic = indices ? indices.getScalar(element + 2) : element + 2;
        positions.getElement(ia, a);
        positions.getElement(ib, b);
        positions.getElement(ic, c);
        const abx = b[0] - a[0];
        const aby = b[1] - a[1];
        const abz = b[2] - a[2];
        const acx = c[0] - a[0];
        const acy = c[1] - a[1];
        const acz = c[2] - a[2];
        const nx = aby * acz - abz * acy;
        const ny = abz * acx - abx * acz;
        const nz = abx * acy - aby * acx;
        for (const index of [ia, ib, ic]) {
          normals[index * 3] += nx;
          normals[index * 3 + 1] += ny;
          normals[index * 3 + 2] += nz;
        }
      }
      for (let index = 0; index < positions.getCount(); index += 1) {
        const offset = index * 3;
        const length = Math.hypot(normals[offset], normals[offset + 1], normals[offset + 2]) || 1;
        normals[offset] /= length;
        normals[offset + 1] /= length;
        normals[offset + 2] /= length;
      }
      primitive.setAttribute(
        "NORMAL",
        document.createAccessor(`${mesh.getName()} normals`).setType("VEC3").setArray(normals),
      );
    }
  }
}

const garments = [
  {
    id: "mh-polo-shirt",
    obj: "PoloShirt/Polo_t-shirt.obj",
    textures: {
      base: "PoloShirt/Polo_Base_Color.png",
      normal: "PoloShirt/Polo_Normal_OpenGL.png",
      roughness: "PoloShirt/Polo_Roughness.png",
    },
  },
  {
    id: "mh-fisherman-sweater",
    obj: "FishermanSweater/sweater_fisherman.obj",
    textures: {
      base: "FishermanSweater/shirt-knit.png",
      normal: "FishermanSweater/shirt-knit-NORM.png",
    },
  },
  {
    id: "mh-wool-pants",
    obj: "WoolPants/pants_wool.obj",
    textures: { base: "WoolPants/Pants_wool.png" },
  },
  {
    id: "mh-flapper-dress",
    obj: "FlapperDress/flapper_dress_1.obj",
    textures: {},
  },
  {
    id: "mh-short-sleeve-qipao",
    obj: "ShortSleeveQipao/westernized_quipoa.obj",
    textures: {},
  },
];

await mkdir(garmentOutput, { recursive: true });
await mkdir(textureOutput, { recursive: true });

for (const garment of garments) {
  const source = resolve(unityAssetRoot, garment.obj);
  const optimizedOutput = resolve(garmentOutput, `${garment.id}.glb`);
  const glb = await obj2gltf(source, {
    binary: true,
    secure: true,
    doubleSidedMaterial: false,
    inputUpAxis: "Y",
    outputUpAxis: "Y",
  });

  const document = await io.readBinary(glb);
  addSmoothNormals(document);
  await document.transform(dedup(), weld(), meshopt({ encoder: MeshoptEncoder, level: "high" }));
  await writeFile(optimizedOutput, await io.writeBinary(document));

  for (const [kind, texture] of Object.entries(garment.textures)) {
    const destination = resolve(textureOutput, `${garment.id}-${kind}.webp`);
    await sharp(resolve(unityAssetRoot, texture))
      .resize({ width: 1024, height: 1024, fit: "inside", withoutEnlargement: true })
      .webp({ quality: kind === "base" ? 86 : 82, effort: 5 })
      .toFile(destination);
  }
}

console.log(`Prepared ${garments.length} Meshopt GLBs and their web textures.`);
