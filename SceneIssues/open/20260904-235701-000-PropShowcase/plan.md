# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render the selected production realization, dispose prior state, and prove useful framing/materials/contact plus bounded switching with exact standalone-player evidence. Only production-quality visual evidence passes; all required checkboxes and CI gates remain mandatory.

Canonical set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants, not identities. Exclude buildings, terrain, characters, VFX-only records, raw materials, and duplicate composition/mechanism aliases.

Structures owns catalogue enumeration and production realization/presenters. `Assets/Game/Composition/Showcase/SceneRuntime` (`VoxelEngine.Showcase`) owns browser lifecycle, environment and framing; the parent Showcase assembly is a separate owner. Structures uses `Validation/PropShowcaseProductionValidation.*`. Add the PropShowcase production-consumer scene/scenario under SceneRuntime's own `Validation/`; its existing input-validation scene and the parent-owned PropShowcase scene do not isolate this material/framing regression. Top-level `Assets/Scenes/PropShowcase.unity` remains integration evidence.

## Current source and evidence
Resumed source `a67d64a8174104327a097f11183db772109d40e3`; latest observed master `ef475182b866eabfe8e1d1a39c82bf7810a03f49` still needs final integration. Shell Git fetch is blocked by DNS; remote refs and files were fetched through GitHub. Prior synchronization used master `cd77b927dbe463171f6cef86bb268a31ae8df4e4`.

Run `34000107687`, request `c72cb89cea7d8a25e10dc8e716eecb300e5702ab`, completed FAILURE: standalone SceneIssue replay passed, automatic module validation failed in Unity Test Runner `RestoreSceneManagerSetup` because `Assets/InitTestScene1822f0e7-8826-4064-9948-8d5a333630ed.unity` no longer existed. This is harness infrastructure evidence, not a passing test run. Artifact `9979710315` SHA256 `ea8b83b779855c344269b26859f3b4ad8488b6040b702fafe4179724139f8d94` was downloaded and inspected.

## Discriminator and selected fix
The world-space presentation root and semantic-front/three-quarter camera fixes already exist. Fresh frame 020 shows Merchant Sign; frame 010 shows the table from an elevated three-quarter view, but with diagnostic rainbow colours. Neither is final production-quality acceptance.

Hypotheses: incorrect material catalogue versus accidentally enabled normal-coverage shading. Production `VoxelRenderPass` sets `_DebugCoverage=1` whenever `SurfaceDebugTint != Color.white`; `SmoothSurface.shader` then returns encoded normals before material evaluation. PropShowcase supplies a non-white tint. This directly explains the capture without changing materials or geometry. Change that composition argument to `Color.white`; add a behavioural lifecycle regression observing the real renderer state and a correctly owned standalone validation consumer. No shader, simulation, renderer-budget or catalogue-identity changes are needed.

## Cost and remaining gates
The configuration fix adds no allocation or per-frame work. Measure real switching/resource behaviour against `device-matrix.md`; do not infer memory flatness from presenter counts. Request exact-source CI after the fix, inspect all mount/backend representatives with production materials, address demonstrated remaining defects, obtain every required module/integration gate, finish metadata/checklists, close open-to-closed, merge current master, then final PR + auto-merge and verify closure on master.
