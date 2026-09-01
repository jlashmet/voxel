# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Preserve the source-backed Mounting Force/Kentridge graph while physically realizing settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded slopes, semantic constrained routes, lake/ridge realization, feature-aware vertical residency/readiness, and production evidence sequencing.
- Shared spatial reservations are integrated without moving geography/route ownership out of `TopDownWorldPhysicalPlanner`; an independent non-Kentridge fixture proves road/building conflicts are rejected (`33441865025`).
- Exact source `b500683...` passed focused storage/module/player validation in `33464366092` attempt 3 after the Southern Ridge/Orc composition correction.
- Exact source `3e34713...` passed the 180-second replay in `33469175243`; all seven targets complete by ~80s, but Fairy/Orc/lake/ridge visual evidence remained below acceptance.
- Exact source `89c3128...` passed signed negative-Z storage streaming (`33474641146`). Exact source `cdc4869...` passed the playable three-catalogue combine discriminator (`33476499718`).
- Exact source `fb4bc6d...` passed focused storage plus 180-second built player in `33480730488`. Correct authored wall/roof probes read `material=0` at every Moordell/Rossdam/Fairy/Orc capture while editor storage still proves all 16 buildings. Full-resolution Fairy/Orc frames confirm the shells are absent. Camera/framing and post-combine hypotheses are therefore falsified.

## Current discriminator
The remaining owner is the playable macro-selection handoff. `TopDownWorldLayoutSelection` is one-shot; `KentridgeCombinedVoxelCatalogue` appends macro content only when `TryConsume(seed, ...)` succeeds. `KentridgePlayableSlice.OnEnable` builds the production catalogue immediately and does not itself select the macro layout.

Feature source beginning at `4976490bf61dc897bf841675bd588a3254a7cc83` adds a behavioral repro only: clear the handoff, prove Fairy/Orc definitions are absent from the production Kentridge catalogue, then select the existing source-backed layout and prove both appear. Experiment 028 records the decision rule. No production behavior has changed yet.

## Next validation
1. Run the focused one-shot selection discriminator on the sole `ci-test/fixes/agent-6` transport.
2. If green, make the smallest composition fix: select the existing semantic macro layout before the playable's existing Kentridge catalogue build. Do not change the shared one-shot contract, physical planner, streaming/device budgets, or evidence framing.
3. Re-run exact built-player evidence; require readable grounded Fairy/Orc/Moordell/Rossdam settlements, lake + constrained route, ridge/pass, network, and CharacterMotor traversal.
4. Extract final route/water/feature/streaming/render/FPS/memory telemetry and vertical-residency blast radius.
5. Merge current master before the final exact-SHA gate. Close only after every acceptance/checklist item is green, then move open->closed, set `status=fixed` / `resolvedUtc`, and non-force promote the exact feature head.