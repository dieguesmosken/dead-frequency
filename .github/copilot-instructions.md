# Copilot instructions for Dead Frequency

This repository is a Godot 4.7.2 .NET game project with a Cloudflare Worker backend and a static website. The working conventions below reflect the project’s actual tooling and architecture.

## Build, test, and lint commands

### Godot / C# gameplay
- Open `project.godot` in Godot 4.7 .NET and use the editor Build button, then Run (F5).
- CLI compile:
  - `dotnet build DeadFrequency.csproj -c Release`
  - `dotnet build DeadFrequency.csproj -c ExportRelease`
- CI builds use the same pattern as `.github/workflows/release.yml`:
  - download Godot .NET + export templates
  - run `dotnet build DeadFrequency.csproj -c ExportRelease || dotnet build DeadFrequency.csproj -c Release`
  - export Windows/Linux with Godot headless
- There is no dedicated C# unit test suite for gameplay in this repo; compile-time validation and editor runs are the main safety checks.

### Server / multiplayer
- Install dependencies:
  - `cd server && npm install`
- Local run:
  - `cd server && npm run dev`
- Smoke test (single targeted verification):
  - `cd server && node test/smoke.mjs`
  - or against a running worker: `cd server && node test/smoke.mjs http://localhost:8787`
- Type-check:
  - `cd server && npm run typecheck`
- Full server validation:
  - `cd server && npm test`

### Site generation
- Regenerate site data from the current game version:
  - `python3 site/build.py`

## High-level architecture

### Game project
- Core gameplay logic lives under `Scripts/` with subareas such as:
  - `Scripts/Core`: shared utilities, interfaces, UI styling, versioning, engine-specific helpers
  - `Scripts/Player`: player movement, interaction, inventory, needs, state
  - `Scripts/Cargo`: cargo data, pickup, damage, delivery logic
  - `Scripts/Vehicle`: van, cars, driving, seats, vehicle systems
  - `Scripts/UI`: menus, CRT HUD, debug screens, overlays
- World content is split between `Scenes/`, `Data/`, `Shaders/`, `Assets/`, and generated procedural city logic under `Scripts/World` and related gameplay systems.
- Design data is generally stored as Godot `Resource` files (for example `.tres`) and kept in `Data/` or adjacent scene/resource folders.
- Save and manifest metadata use `.tzn` JSON files written via `System.Text.Json` + `FileAccess`; the project does not rely on `ResourceSaver` for custom extensions.

### Multiplayer server
- The multiplayer backend lives in `server/` and runs on Cloudflare Workers + Durable Objects.
- `server/src/index.ts` defines the worker, room logic, and server-authoritative rules.
- The server mirrors gameplay rules from the Godot client, especially:
  - room code generation and limits
  - turn and delivery logic
  - cargo order/catalog parity with `DeliveryManager.CargoCatalog`
  - vehicle ownership and possession rules
- Server deployment and secrets are documented in `server/README.md` and use Wrangler; do not commit `server/.dev.vars`.

### Static website and release data
- `site/` is a GitHub Pages site generated from project version metadata and screenshots.
- `site/build.py` regenerates `site/data/site.json` based on `Scripts/Core/GameVersion.cs` and version folders in `site/assets/shots/`.
- Release notes are generated through `site/release_notes.py`.

## Key repository conventions

### Godot/C# code style
- Use `public partial class X : NodeType` for scripts.
- Prefer `[Export]` for values that need to appear in the Inspector and `[GlobalClass]` when a class is meant to be exposed as a resource-typed type.
- Resolve nodes in `_Ready()` with `GetNode<T>()` and store references in private fields; avoid deep parent traversal.
- Physics code belongs in `_PhysicsProcess(double delta)` and normally uses `float dt = (float)delta;`.
- Prefer interface-based interaction checks like `collider is IInteractable`; do not use string-based group checks.
- Keep the signal pattern consistent: use `[Signal]` or `event Action<T>` to communicate upward in the scene tree.
- Treat `Vector3`, `Transform3D`, `Basis`, and `Color` as value types: copy, mutate, and reassign rather than mutating shared state in place.

### Project-specific rules from the repo
- Collision layers are defined as:
  - 1 World
  - 2 Player
  - 3 Interactables
  - 4 Cargo
  - 5 Vehicle
- The gameplay/UI system intentionally avoids a traditional HUD; most information is carried by the van dashboard, CRT monitor, and diegetic UI elements.
- Shop, item, and cargo rules are mirrored between the Godot client and Cloudflare server; keep catalog ordering consistent across both sides.
- Versioning is tied to `Scripts/Core/GameVersion.cs` and `GameVersion.Changelog`; release artifacts are expected to be published with the matching tag format `vX.Y.Z`.
- Ignore or do not version the generated/secret files listed in project docs: `server/.dev.vars`, `server/node_modules`, `.godot/`, and `_to_delete/`.

### Repo-specific guidance
- The project’s current design notes are summarized in `CLAUDE.md`; it is a useful source for repo-specific architecture and implementation rules that are not obvious from a single file.
- `README.md` remains the best source for user-facing gameplay and release instructions.
- `server/README.md` covers the worker rules and local multiplayer test flow.
- Story mode, new levels (Bananal, Gruta, Serra), IK and level optimization are documented in `docs/README_PARA_IAS.md` (Portuguese). Read it before touching those areas. The game is a cozy everyday-life sim: never add horror, monsters or supernatural elements.
- `docs/` is documentation only: code templates live in `docs/templates/*.cs.txt` and `DeadFrequency.csproj` excludes `docs/**`. Never add `.cs` files under `docs/`.

## Working expectations

- Prefer surgical edits in the relevant subsystem instead of broad refactors.
- Keep Godot and Cloudflare logic in sync when touching shared rules such as cargo catalog ordering, player state, or networking contracts.
- When changing release metadata or version numbers, update `Scripts/Core/GameVersion.cs` and the corresponding changelog/release notes in the expected project flow.
