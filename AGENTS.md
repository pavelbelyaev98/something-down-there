# Repository Guidance: Something Down There

Docs-driven first-person excavation game built in Unity. Runtime code lives in `unity/`; design, architecture, and task state live in `docs/`.

## 1. Quick Start & Navigation

1. **Active Task & Status:** Check `docs/tasks.md` first. It is the single source of truth for the priority queue and completed history.
2. **Game Design Authority:** Read `docs/concept/00_README.md` and relevant concept chapters (`docs/concept/01`–`15`).
3. **Current Codebase:** Check `docs/architecture.md` for system ownership and `docs/baseline.md` for what is currently built.
   - *Note:* Existing baseline mechanics and art are working prototype features, **not** signed-off or final. They are expected to be refactored or replaced to match `docs/concept/`.

## 2. Task Workflow (Just-In-Time Planning)

- **Execution:** Work on the next pending task in `docs/tasks.md`.
- **Outcome-Driven (Think, Don't Just Execute):** Tasks define the *experiential and mechanical goal*, not an inflexible script. You are expected to think independently about how to best achieve that gameplay feel in code, evaluate trade-offs, and suggest or ask about the best architectural path during the JIT spec phase rather than blindly hardcoding rigid assumptions.
- **Extend, Don't Duplicate:** Inspect `docs/baseline.md` and `docs/architecture.md` first. Always build upon or refactor existing classes instead of creating parallel duplicate systems. If an architectural approach is ambiguous, ask the user.
- **No Pre-Release Backward Compatibility:** Until the game releases, support only the current implementation and data formats. Do not retain old-save readers, migrations, content aliases, obsolete APIs/assets, compatibility shims or tests solely for legacy behavior. Remove superseded code and references when replacing a system. Breaking older saves or development data is acceptable and does not require separate compatibility approval; use New Game or disposable test profiles instead of preserving old formats. Keep current-format validation, corruption recovery and gameplay correctness. Define a release compatibility policy before shipping.
- **Just-In-Time Spec:** When starting an active task, create a thorough spec at `docs/tasks/<id>-<slug>.md`. Thoroughly define: Objective, Concept Reference, live codebase analysis, exact architecture/class changes, edge cases, and concrete Acceptance Criteria.
- **Plan-First Mode:** If instructed to plan first or discuss, create the spec at `docs/tasks/<id>-<slug>.md`, summarize the technical approach and trade-offs in chat, and halt for user confirmation before modifying code.
- **Pragmatic Tests & Benchmarks:** Write tests or benchmarks **only when useful on core systems** (e.g. voxel meshing algorithms, save serialization, progression math, or performance-critical loops). Do not write tests for trivial UI layout, cosmetic props, or simple visual tweaks.
- **Completion Protocol:** A task is complete only when:
  1. Code compiles warning-free and passes relevant tests (including any new high-value tests).
  2. Playable gameplay changes are verified and built to `builds/windows/SomethingDownThere.exe`.
  3. The completed spec is moved from `docs/tasks/<id>-<slug>.md` to `docs/tasks/completed/<id>-<slug>.md` to preserve architectural decisions.
  4. If a baseline system was refactored or replaced, update `docs/baseline.md` so it remains an accurate snapshot of working code.
  5. The task is marked `[x]` in `docs/tasks.md`, and a 1–2 sentence technical summary is appended under `## Completed`.

## 3. Minimal Documentation & Data Rules

- **Strict Scannability:** Keep documentation minimal and concise. No session narratives, chat transcripts, or command logs. Target under 60 lines for roadmap/status files; task specs may be as thorough as needed.
- **Data-Driven Architecture:** **Never store item prices, coordinates, or tool stats in Markdown files.**
  - Discovery properties (prices, depths, exposure, counts) live in `catalog.json` / `DiscoveryCatalog.asset`.
  - Tool upgrade parameters and the shared tier price ladder live in `EquipmentProgression.cs`; every track pays the same for a tier, and none of it is serialized into the scene.
- **Decentralized Asset Tracking:** Do **not** maintain a centralized asset ledger. Document assets minimally in their local folder: `art/<name>/README.md` (5–8 line card stating: Item, Purpose, Source/License, Unity Path, Status).
- **Asset Approvals:** New external visual/audio content and paid assets/utilities require approval of the specific product and license before purchase/import. The user is open to buying useful assets and tools; proactively request them when they materially improve the game or development workflow. Visuals/audio must be licensed for commercial use or created via Blender MCP. Free code dependencies follow the library policy below.
- **Purchased Asset Workflow:** Pure Nature 2: Mountains (`unity/Assets/BK/`) is approved. Preserve vendor paths and `.meta` GUIDs; use project-owned materials/prefab variants under `Assets/Content` for tuning. Record publisher patches in the local `art/pure-nature-mountains/README.md` card. Keep the full import and metadata in private version control, with vendor binaries in Git LFS; see `unity/readme.md`. Reuse assets through existing gameplay systems; demo scenes and lighting must not replace MainGame ownership.
- **Recovery Scene:** Never delete, overwrite, or commit the user recovery scene `unity/Assets/_Recovery/0.unity`. Pre-release saves are disposable; reset obsolete profiles through the normal game/test workflows when needed, without adding compatibility code.

## 4. Post-Playtest Design Iteration Protocol

When the user playtests a build and modifies or redesigns a feature:
1. **Update `docs/concept/` In Place:** Update the relevant section in `docs/concept/` to record the new design intent (the concept docs are the living source of truth).
2. **Apply Code & Data Changes:** Adjust C# logic and balance numbers in `catalog.json` or `EquipmentProgression.cs`.
3. **Keep `docs/baseline.md` Accurate:** Update the working system snapshot in `docs/baseline.md` so subsequent tasks never rely on obsolete assumptions.
4. **Log the Iteration in `docs/tasks.md`:** Update the completed entry summary or append an iteration note under `## Completed`.
5. **Minimal Output:** Do not output chat narratives, changelogs, or walls of text. Apply the edits directly and confirm completion in under 3 lines.

## 5. Unity & Developer Tooling

- **Request Useful Tools:** Do not hesitate to recommend paid utilities, art, audio, VFX, shaders or animation packs for current or upcoming work. Explain the concrete benefit, when it is needed, current price/license, Unity/URP compatibility and integration cost; link the product. Prefer maintained tools with usable C# APIs and automation support. Paid tools are not inherently safer: evaluate them against existing systems and free alternatives before requesting a purchase.
- **Install Needed Libraries:** Install useful free libraries and Unity packages autonomously when the active task justifies them; no extra confirmation is needed for ordinary project-local dependency additions. Use official registries or trusted upstream sources, verify commercial-use licensing and compatibility, pin versions in the appropriate manifests/lockfiles, and run relevant checks. Document their purpose in the owning developer/asset documentation. Avoid speculative dependencies and duplicate frameworks; paid licenses or services still require product-specific approval.
- **Unity Environment:** Unity `6000.6.0f1` with URP `17.6.0` and Unity Input System.
- **Approved Odin Tooling:** Inspector/Serializer and Validator `4.0.2.4` are approved and installed. Use Inspector for useful authoring/tuning interfaces and Validator for asset/reference checks; keep custom validators in `Assets/Editor`. The MainGame validation profile runs before builds. Keep Odin in Editor Only mode while runtime serialization is unused; existing save ownership stays intact. See `unity/readme.md` and `art/odin/README.md`. These tools supplement tests and playtesting; they do not establish gameplay correctness.
- **Hand-off:** every task that changes runtime behaviour ends with a fresh `builds/windows/SomethingDownThere.exe` ready to playtest — never hand back a stale build, and never hand-place files into `builds/windows`: the build wipes its folder first so the artifact contains the game only.
- **Test scaffolding:** never delete `Assets/InitTestScene*.unity` while the Editor is open — the test runner owns those and a modal "Scene(s) Have Been Modified" dialog will block the Editor. Let the runner clean them up.
- **CLI & Pipeline:** Use the official Unity CLI directly (`unity status`, `unity command`) with `com.unity.pipeline` for live editor inspection. See `unity/readme.md`.
- **Agent Skill:** Load the `unity-cli` skill before Unity CLI work; it is installed for Codex and opencode via `unity skill install codex` (`~/.agents/skills/unity-cli`) — refresh with `--yes` after CLI updates.
- **3D Modeling:** Use Blender MCP (`127.0.0.1:9876`) for generating and modifying 3D assets, storing recipes and `.blend` files under `art/`.
- **Windows Builds:**
  - **If Unity Editor is open:** `unity command menu --path 'Tools/Something Down There/Build Windows Player' --timeout 300 --project-path "$projectPath" --format json`
  - **If Unity Editor is closed:** `./tools/build-windows.ps1`
  - *Warning:* Never run `./tools/build-windows.ps1` while the Editor is open, as batchmode will fail on process locks.
