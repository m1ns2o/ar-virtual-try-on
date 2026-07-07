# AR Closet Unity Prototype

Unity version target: 6000.0.62f1

This project is a desktop-oriented Unity prototype path for 3D clothing fitting.

## Prototype Strategy

The MVP should not try to accept arbitrary unprepared clothing models. Use a standard humanoid avatar and require every garment asset to be prepared as one of:

- a prefab containing one or more `SkinnedMeshRenderer` objects bound to the same humanoid bone names as the avatar
- a static placeholder prefab for early UI/demo flow only

The runtime app then switches garments by loading a garment prefab and rebinding its bones to the active avatar.

## Demo Garment Set

Use these as the first standardized examples:

- T-shirt: upper-body garment
- Hoodie: upper-body garment with hood volume
- Dress: one-piece garment
- Pants: lower-body garment for the second milestone

The menu item `AR Closet/Create Demo Scene` creates a simple mannequin and placeholder prefabs for T-shirt, hoodie, and dress.

## Asset Sources To Evaluate

- VRoid Studio / VRM: useful for quick humanoid avatars and dressed models.
- Ready Player Me: useful for avatar loading and standardized humanoid content.
- Sketchfab / OpenGameArt / MakeHuman Community: useful for CC0 or permissive sample clothing, but each item must be checked and normalized.
- Blender pipeline: preferred for final research assets because rig, weights, bind pose, and scale can be controlled.

## Architecture

- `GarmentDefinition`: describes one garment.
- `GarmentFittingController`: equips a garment prefab and rebinds skinned garment bones to the avatar.
- `ARClosetDemoSceneBuilder`: editor utility that creates a simple demo scene and placeholder garments.

## Next Implementation Steps

1. Import one humanoid avatar with stable bone names.
2. Create three garment prefabs using the same skeleton: T-shirt, hoodie, dress.
3. Replace placeholder prefabs with skinned garment prefabs.
4. Add webcam pose tracking with MediaPipe or another pose provider.
5. Map pose landmarks to an upper-body driver.
6. Add Addressables after garment prefabs are stable, so garments can be loaded as catalog items.

## Unity CLI

The project manifest includes `com.youngwoocho02.unity-cli-connector`.

After Unity resolves packages:

1. Open this Unity project.
2. The connector starts automatically in the Editor.
3. Run `unity-cli status` from this Unity project folder.
4. Use `unity-cli menu "AR Closet/Create Demo Scene"` to generate the mannequin and placeholder garments.
5. Use `unity-cli exec "return UnityEngine.Application.dataPath;"` for quick Editor-side checks.
