# Repository Guidelines

## Project Structure & Module Organization

- `Assets/ARCloset/` contains the Unity 6 prototype: runtime fitting scripts in `Scripts/`, editor-only scene/setup helpers in `Editor/`, garment prefabs in `Prefabs/`, and catalog assets in `Catalog/`.
- `Assets/ARCloset/MakeHuman/` holds source garment assets and license records. Do not modify originals from web asset tooling.
- `ProjectSettings/` pins the Unity Editor version (`6000.0.62f1`).
- `WebApp/` is an independent React 19 + TypeScript + Vite camera fitting app. UI lives in `src/components/`, pose and fitting logic in `src/lib/`, definitions in `src/data/garments.ts`, and tests sit beside the code as `*.test.ts`.
- `WebApp/public/` contains deployable MediaPipe, GLB, and texture assets. Generated files should be refreshed through scripts, not hand-edited.

## Build, Test, and Development Commands

Run web commands from `WebApp/` with Node.js 22 or newer:

```sh
npm ci                 # install locked dependencies
npm run dev            # serve Vite on 127.0.0.1
npm test               # run Vitest unit and catalog-parity tests
npm run build          # type-check and produce dist/
npm run prepare:assets # regenerate optimized garment GLBs/textures
npm run prepare:mediapipe # copy MediaPipe WASM runtime assets
```

Open the repository root in Unity 6000.0.62f1 for the desktop prototype. Use `AR Closet/Create Demo Scene` to create the demo rather than manually recreating its setup.

## Coding Style & Naming Conventions

Use TypeScript with two-space indentation, semicolons, double-quoted imports/strings where existing code does, and typed public values. Use `PascalCase` for React components and C# types, `camelCase` for functions and variables, and `kebab-case` garment IDs such as `mh-polo-shirt`. Keep Unity `.meta` files paired with their assets. Keep user-facing web copy in `src/i18n/ko.ts`.

## Testing Guidelines

Add focused Vitest cases next to changed web logic (`fit.test.ts`, `poseFilter.test.ts`). Changes to garment definitions must keep `catalogParity.test.ts` passing and preserve matching IDs, slots, fitting offsets, source, and license information across Unity and web. Run `npm test` and `npm run build` before opening a pull request. Check camera behavior manually on HTTPS or localhost because browser permissions cannot be unit-tested reliably.

## Commit & Pull Request Guidelines

Use concise, imperative summaries consistent with history: `Fix Netlify WebApp deployment` or `Improve pose fitting accuracy`. Keep commits scoped to one concern. Pull requests should explain the user-visible fitting or asset change, list validation commands, link relevant issues, and include screenshots or a short recording for UI, camera, or 3D changes. Call out new asset licenses and generated asset updates explicitly.
