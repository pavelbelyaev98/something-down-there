# 093 — Find Mesh Detail Levels

**Status:** complete. Every find mesh gets index-only Mesh LOD levels on import, generated after its exposure samples so prefabs are unchanged. The view-distance setting sets the global Mesh LOD threshold (`GraphicsQuality.MeshLodThreshold`); High looks identical to full detail and cuts a dug pit's GPU time by about an eighth.

## Objective
Cut the rendering cost of exposed finds, which dominate the dig view once a pit opens, without a visible change at the accepted High look. Full detail stays up close and simpler meshes take over a few metres away. Finds imported later follow the same rule, and the graphics settings reach them without per-asset setup.

## Concept reference
- `09`: stylized low-poly with strong silhouettes. Backdrop geometry detail may drop for frame rate, but contact cues at the worksite stay.
- `08` §8: view distance is the Graphics row for how far detail reaches, and presets set it. High is the accepted look.
- `14`: stable frame pacing while digging.
- User request: settings must not ignore content added later.

## Live code analysis
- Find visuals: common rocks are 12,500 triangles each (5,390 in the population, 640 in the shallow layer), the four metal ores ≈ 3,900, coal 1,164, gems ≈ 400 and the computer unique 524. Nothing has LODs.
- `DiscoveryContentSetup.ImportAppearance` (rocks and minerals) and `RetroComputerSetup` (the three computer uniques share one mesh) build `.asset` meshes, then call `UpdatePrefab`. It draws the 256 exposure samples from `mesh.triangles` in index order.
- `BuriedFind.VertexRadius` reads vertices only. Collision uses separate hulls.
- Unity 6000.6 Mesh LOD: `MeshLodUtility.GenerateMeshLods(mesh, limit)` appends index-only levels that share the vertex buffer. Each renderer picks a level from screen size, `Mesh.lodSelectionCurve` and the global `QualitySettings.meshLodThreshold` (1 in every quality level). `Renderer.forceMeshLod` pins a level.
- `UnityGameSettingsPlatform.ApplyViewDistance` already drives global terrain overrides from the view-distance row, and the benchmark shares it.

## Architecture
- **Generation:** `DiscoveryContentSetup.GenerateDetailLevels(mesh)` runs `GenerateMeshLods(mesh, -1)` (down to ≈ 64 indices). `ImportAppearance` calls it after `UpdatePrefab`. `RetroComputerSetup` calls it once, after all three uniques have sampled the shared mesh. Generation reorders the full-detail triangles, so sampling first keeps every prefab's exposure samples byte-identical. Re-syncs copy the fresh import over the asset, which clears the old levels before sampling.
- **Settings:** `GraphicsQuality.MeshLodThreshold(viewDistance)`: High 2.5, Medium 3.2, Low 4. `ApplyViewDistance` sets it, and `UnityGameSettingsPlatform.Dispose` restores the original. Unity's default of 1 barely engages LOD at dig distances (below). 2.5 was indistinguishable from full detail, and savings level off near 4.
- **Benchmark:** the `finds_full_detail` environment variant forces full detail (level 0) on every find to measure the saving in the same run.
- **Rejected:** Blender-made LOD meshes plus `LODGroup` per find. That means extra assets and components on 12k objects, crossfade or pop tuning, and a manual step for every new find. Also rejected: a separate Graphics row. View distance already means "how far detail reaches".

## Edge cases
- Exposure, collection, physics and saves are unaffected: samples, hulls and vertices are unchanged, and levels are index-only.
- The rocks' held and displayed poses are close-up, so they render full detail.
- Development content and other props are outside the importers. The readme documents the rule for new dense props.

## Acceptance criteria
- Every catalog find mesh has generated levels; prefabs, the scene and exposure samples are unchanged by the sync.
- At High, the dig view is visually identical to forced full detail; Medium and Low differ only slightly.
- The pit benchmark shows the find triangle and GPU saving. View distance sets the threshold, and Dispose restores it.
- Relevant tests pass and a fresh Windows player is built.

## Verification notes
- Re-sync: all 14 find prefabs (3 rocks, 8 minerals, 3 computer uniques) and MainGame are byte-identical to before. A first attempt that generated levels before sampling changed every prefab's exposure samples.
- Levels (triangles): rock 12,500 → 6.3–6.7k → 3.4–3.7k → … → 64 (10 levels). Metal ores 3.9k → 2.1k → … (7–8 levels), coal 6 levels, gems 4 levels.
- Editor pit with ≈ 300 exposed rocks at 1920×1080: High versus forced full detail differs in 798 of 2.07 M pixels by more than 8/255 (invisible). Low differs in 47k pixels, mostly faint shading inside distant rocks.
- Native pit benchmark (RX 9060 XT, 2560×1440, High defaults, 639 rendered finds), GPU median and triangles per frame (shadows included), threshold only:

  | Threshold | GPU ms | Triangles |
  |---|---|---|
  | forced full detail | 5.25 | 15.2 M |
  | 1 (Unity default) | 5.26 | 15.1 M |
  | 1.6 | 5.00 | 12.2 M |
  | 2.5 (High) | 4.60 | 7.7 M |
  | 4 (Low) | 4.31 | 4.6 M |

  Screenshot differences from full detail at every threshold stay within the grass and water animation noise (≈ 13–19k of 3.7 M pixels differ by more than 8/255, including threshold 1).
- Rejected: keeping Unity's default of 1 at High. It is almost identical to having no LOD (−0.3 % triangles in the pit).
- Tests: EditMode 301/301, Detector 2/2, Discovery 18/18, FindPhysics 38/38, FpsUiInput 24/24, Save 12/12, StartupMenu 8/8.
- Found on the way: the 090 checkpoint cache returned shared records from the public `BuriedFind.Capture()`, so edited copies aliased the cache. Autosave now uses internal `CaptureCheckpoint()` (see the 090 iteration note).
