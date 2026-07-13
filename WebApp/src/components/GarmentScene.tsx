import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  Box3,
  BufferAttribute,
  CanvasTexture,
  Color,
  DoubleSide,
  Group,
  LinearFilter,
  MathUtils,
  Mesh,
  MeshStandardMaterial,
  NoColorSpace,
  Object3D,
  SRGBColorSpace,
  Texture,
  TextureLoader,
  Vector3,
} from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import { MeshoptDecoder } from "three/examples/jsm/libs/meshopt_decoder.module.js";

import { deformGarmentPositions, type GeometryBounds } from "../lib/deform";
import { buildGarmentModelFitFrame, type GarmentModelFitFrame } from "../lib/garmentFitFrame";
import type { GarmentAppearance, GarmentDefinition, GarmentFit } from "../types/pose";

interface Props {
  definition: GarmentDefinition;
  appearance: GarmentAppearance;
  fit: GarmentFit | null;
  onCanvas: (canvas: HTMLCanvasElement) => void;
  onLoadingChange: (loading: boolean) => void;
  onLoadError: () => void;
}

interface GeometryBinding {
  geometry: Mesh["geometry"];
  rest: Float32Array;
  output: Float32Array;
  bounds: GeometryBounds;
}

interface LoadedGarment {
  definition: GarmentDefinition;
  object: Object3D;
  size: Vector3;
  fitFrame: GarmentModelFitFrame;
  materials: MeshStandardMaterial[];
  bindings: GeometryBinding[];
  baseTexture: Texture | null;
  normalTexture: Texture | null;
  detailTexture: Texture | null;
}

const textureLoader = new TextureLoader();
const gltfLoader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);

async function optionalTexture(path: string | undefined, color = false): Promise<Texture | null> {
  if (!path) return null;
  try {
    const texture = await textureLoader.loadAsync(path);
    texture.flipY = false;
    texture.colorSpace = color ? SRGBColorSpace : NoColorSpace;
    texture.minFilter = LinearFilter;
    return texture;
  } catch {
    return null;
  }
}

function materialClone(source: Mesh["material"]): MeshStandardMaterial[] {
  const sources = Array.isArray(source) ? source : [source];
  return sources.map((material) => {
    if (material instanceof MeshStandardMaterial) {
      const clone = material.clone();
      clone.side = DoubleSide;
      clone.transparent = true;
      clone.opacity = 0.96;
      clone.metalness = 0.02;
      clone.roughness = 0.78;
      return clone;
    }
    return new MeshStandardMaterial({ side: DoubleSide, transparent: true, opacity: 0.96, roughness: 0.78 });
  });
}

async function loadGarment(definition: GarmentDefinition): Promise<LoadedGarment> {
  const gltf = await gltfLoader.loadAsync(definition.modelPath);
  const object = gltf.scene.clone(true);
  const materials: MeshStandardMaterial[] = [];
  const bindings: GeometryBinding[] = [];

  object.traverse((child) => {
    if (!(child instanceof Mesh)) return;
    child.geometry = child.geometry.clone();
    const clonedMaterials = materialClone(child.material);
    child.material = Array.isArray(child.material) ? clonedMaterials : clonedMaterials[0];
    materials.push(...clonedMaterials);
    child.castShadow = false;
    child.receiveShadow = false;
    child.frustumCulled = false;
    child.geometry.computeBoundingBox();
    if (!child.geometry.getAttribute("normal")) child.geometry.computeVertexNormals();
    const position = child.geometry.getAttribute("position") as BufferAttribute;
    const rest = new Float32Array(position.array as ArrayLike<number>);
    const box = child.geometry.boundingBox!;
    bindings.push({
      geometry: child.geometry,
      rest,
      output: new Float32Array(rest.length),
      bounds: {
        min: [box.min.x, box.min.y, box.min.z],
        max: [box.max.x, box.max.y, box.max.z],
      },
    });
  });

  object.updateMatrixWorld(true);
  const box = new Box3().setFromObject(object);
  const center = box.getCenter(new Vector3());
  const size = box.getSize(new Vector3());
  object.position.sub(center);
  object.updateMatrixWorld(true);
  const [baseTexture, normalTexture, detailTexture] = await Promise.all([
    optionalTexture(definition.texturePath, true),
    optionalTexture(definition.normalTexturePath),
    optionalTexture(definition.detailTexturePath),
  ]);
  const fitFrame = buildGarmentModelFitFrame(size, definition.slot);
  return { definition, object, size, fitFrame, materials, bindings, baseTexture, normalTexture, detailTexture };
}

function disposeLoaded(loaded: LoadedGarment) {
  loaded.bindings.forEach(({ geometry }) => geometry.dispose());
  loaded.materials.forEach((material) => material.dispose());
  loaded.baseTexture?.dispose();
  loaded.normalTexture?.dispose();
  loaded.detailTexture?.dispose();
}

function patternTexture(appearance: GarmentAppearance): CanvasTexture {
  const canvas = document.createElement("canvas");
  canvas.width = 256;
  canvas.height = 256;
  const context = canvas.getContext("2d")!;
  context.fillStyle = appearance.baseColor;
  context.fillRect(0, 0, 256, 256);
  const stripe = Math.max(4, appearance.stripeWidth);
  context.fillStyle = appearance.stripeColor;
  for (let y = 0; y < 256; y += stripe * 2) context.fillRect(0, y, 256, stripe);
  context.globalAlpha = 0.06;
  context.fillStyle = "#ffffff";
  for (let x = 0; x < 256; x += 4) context.fillRect(x, 0, 1, 256);
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  texture.flipY = false;
  texture.needsUpdate = true;
  return texture;
}

function useLoadedGarment(
  definition: GarmentDefinition,
  onLoadingChange: (loading: boolean) => void,
  onLoadError: () => void,
) {
  const [loaded, setLoaded] = useState<LoadedGarment | null>(null);
  const loadedRef = useRef<LoadedGarment | null>(null);

  useEffect(() => {
    let cancelled = false;
    onLoadingChange(true);
    loadGarment(definition)
      .then((next) => {
        if (cancelled) {
          disposeLoaded(next);
          return;
        }
        const previous = loadedRef.current;
        loadedRef.current = next;
        setLoaded(next);
        onLoadingChange(false);
        if (previous) requestAnimationFrame(() => disposeLoaded(previous));
      })
      .catch(() => {
        if (!cancelled) {
          onLoadingChange(false);
          onLoadError();
        }
      });
    return () => {
      cancelled = true;
    };
  }, [definition, onLoadError, onLoadingChange]);

  useEffect(
    () => () => {
      if (loadedRef.current) disposeLoaded(loadedRef.current);
      loadedRef.current = null;
    },
    [],
  );
  return loaded;
}

function GarmentActor({ loaded, appearance, fit }: { loaded: LoadedGarment; appearance: GarmentAppearance; fit: GarmentFit | null }) {
  const groupRef = useRef<Group>(null);
  const patternRef = useRef<CanvasTexture | null>(null);
  const { viewport } = useThree();
  const targetPosition = useMemo(() => new Vector3(), []);
  const targetScale = useMemo(() => new Vector3(), []);
  const anchorOffset = useMemo(() => new Vector3(), []);

  useEffect(() => {
    patternRef.current?.dispose();
    patternRef.current = appearance.stripeEnabled ? patternTexture(appearance) : null;
    loaded.materials.forEach((material) => {
      material.color.set(appearance.stripeEnabled ? "#ffffff" : appearance.baseColor);
      material.map = patternRef.current ?? loaded.baseTexture;
      material.normalMap = loaded.normalTexture;
      material.roughnessMap = loaded.detailTexture;
      material.needsUpdate = true;
    });
    return () => {
      patternRef.current?.dispose();
      patternRef.current = null;
    };
  }, [appearance, loaded]);

  useEffect(() => {
    if (!fit) return;
    for (const binding of loaded.bindings) {
      deformGarmentPositions(binding.rest, binding.bounds, fit.deformation, loaded.definition.slot, binding.output);
      const position = binding.geometry.getAttribute("position") as BufferAttribute;
      (position.array as Float32Array).set(binding.output);
      position.needsUpdate = true;
      binding.geometry.computeVertexNormals();
      binding.geometry.computeBoundingSphere();
    }
  }, [fit, loaded]);

  useFrame((_, delta) => {
    const group = groupRef.current;
    if (!group) return;
    group.visible = Boolean(fit);
    if (!fit) return;
    const targetWidth = fit.width * viewport.width * 0.5;
    const targetHeight = fit.height * viewport.height * 0.5;
    const widthScale = targetWidth / loaded.fitFrame.fitWidth;
    const heightScale = targetHeight / loaded.fitFrame.fitHeight;
    const scalar = MathUtils.lerp(widthScale, heightScale, fit.heightBlend);
    const alpha = 1 - Math.exp(-Math.min(delta, 0.12) * 12);
    const targetRotation = fit.rotation + loaded.definition.rotationOffset[2];
    group.rotation.x = loaded.definition.rotationOffset[0];
    group.rotation.y = loaded.definition.rotationOffset[1];
    group.rotation.z = MathUtils.lerp(group.rotation.z, targetRotation, alpha);
    targetScale.set(
      scalar * loaded.definition.scaleOffset[0],
      scalar * loaded.definition.scaleOffset[1],
      scalar * loaded.definition.scaleOffset[2],
    );
    anchorOffset
      .set(
        loaded.fitFrame.anchorLocal.x * targetScale.x,
        loaded.fitFrame.anchorLocal.y * targetScale.y,
        loaded.fitFrame.anchorLocal.z * targetScale.z,
      )
      .applyEuler(group.rotation);
    targetPosition.set(
      fit.anchor.x * viewport.width * 0.5 - anchorOffset.x,
      fit.anchor.y * viewport.height * 0.5 - anchorOffset.y,
      0,
    );
    group.position.lerp(targetPosition, alpha);
    group.scale.lerp(targetScale, alpha);
  });

  return (
    <group ref={groupRef} visible={false}>
      <primitive object={loaded.object} />
    </group>
  );
}

export function GarmentScene(props: Props) {
  const loaded = useLoadedGarment(props.definition, props.onLoadingChange, props.onLoadError);
  const background = useMemo(() => new Color("#000000"), []);
  return (
    <Canvas
      className="garment-canvas"
      orthographic
      camera={{ position: [0, 0, 10], zoom: 100, near: 0.01, far: 100 }}
      dpr={[1, 2]}
      gl={{ alpha: true, antialias: true, preserveDrawingBuffer: true }}
      onCreated={({ gl, scene }) => {
        gl.setClearColor(background, 0);
        props.onCanvas(gl.domElement);
        scene.background = null;
      }}
    >
      <ambientLight intensity={1.9} />
      <directionalLight position={[1.5, 2.8, 5]} intensity={3.2} />
      <directionalLight position={[-2, 0.5, 3]} intensity={1.1} color="#d9f4e6" />
      {loaded ? <GarmentActor loaded={loaded} appearance={props.appearance} fit={props.fit} /> : null}
    </Canvas>
  );
}
