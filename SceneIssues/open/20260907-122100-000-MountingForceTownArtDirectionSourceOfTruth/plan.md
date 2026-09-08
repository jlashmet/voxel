# Plan

## Goal
Create the durable Mounting Force town-art source of truth requested by this SceneIssue: a source-provenance-backed town roster, a locked named-place inventory and written visual brief for every town, and a coherent Project North Star concept-art pack with at least six production-quality environment concepts per town.

This assignment changes reference content only. It does not implement or retune production scenes, gameplay, characters, or UI.

## Source precedence and anti-drift rule
1. **Original Mounting Force repository evidence** establishes historical town identity, relationships, named buildings/landmarks, and original thematic cues. Pin every claim to repository+commit+path where available.
2. **Explicit coordinator requirements in this SceneIssue** are canonical requirements even when the historical source archive does not expose a matching textual filename (for example Cambridge is required and must read as a water town).
3. **New Project North Star art direction** supplies the production-quality architectural/material/lighting language needed to modernize the rudimentary 2D source. Derived choices must be labeled as derived, not historical fact.
4. **Existing voxel town imagery and current scene implementation are non-authoritative for visual truth.** They may not be used to justify the new town identity.

## Evidence baseline
- Original source repository: `jlashmet/mounting-force` pinned at `f37512e0175f46e14ad2d7aeb5ecea290af14a14`.
- Later reference repository: `jlashmet/MountingForce2` pinned at `5e111d5a72e441824311b4a3a176f72e9c86f653`; its tracked Assets tree contains no Cambridge/Kentridge/Hightown path match, so it is treated as a secondary corroboration source rather than a competing town canon.
- Current connected Project North Star style-guide imagery may define the game-wide presentation language, but not original town-specific facts.

## Competing hypotheses and discriminators
1. **H1 — Existing voxel town art is already the source of truth.** Disconfirmed by the SceneIssue itself: the assignment exists because current town art is not sufficiently consistent or authoritative.
2. **H2 — The original Mounting Force sources establish town identity and required places, but not a production-quality modern visual language.** Test by inventorying scene/story/map/art references and separating historical facts from derived visual decisions. Expected to be confirmed.
3. **H3 — `MountingForce2` supersedes or materially changes the original town roster.** Test its tracked scene/assets/source names for each original town. If no corresponding evidence exists, retain the original repository as primary town provenance.
4. **H4 — Cambridge can be fully reconstructed as an original-source town from the connected source archive.** Search both repositories and connected references. If provenance remains absent, record that gap explicitly and use only the coordinator's canonical water-town requirement as historical input; all additional Cambridge design choices remain derived.
5. **H5 — Named-place conflicts exist between sources.** Where encountered, record both claims and the discriminator used; never silently merge conflicting facts.

## Root cause / feature gap
Downstream worldbuilding has no single, repository-organized, provenance-backed specification for how each Mounting Force town should look. That allows placeholder imagery and generic medieval styling to drift into de-facto canon. The fix is a durable source pack that distinguishes historical requirements from newly authored art direction and makes each town visually unmistakable while sharing one Project North Star language.

## Implementation sequence
1. Inventory the complete historical town/location evidence from the original repository and connected reference material.
2. Freeze the canonical town roster and record confidence/provenance, including any coordinator-canonical source gaps.
3. Before any town concept generation, lock that town's exact named/specific buildings, landmarks, public spaces, infrastructure, and source references.
4. Author a game-wide Project North Star environment language: shape grammar, materials, color/lighting, detail density, readability, and anti-drift rules.
5. Author a per-town brief covering identity/theme, architecture, materials/palette, culture/economy, terrain/environment integration, signature motifs/landmarks, named-place inventory, and explicit historical-vs-derived labeling.
6. Generate and commit at least six original environment concepts per town: one overall/elevated view, two exterior building concepts, two interior building concepts, and one street/plaza/public-space concept. Concepts must be production-reference quality, environment-only, original, readable, and non-realistic/stylized.
7. Add a game-wide index comparing town palettes, materials, motifs, silhouette, economy, infrastructure, and anti-drift rules.
8. Audit every acceptance requirement and every concept file against the locked inventories.

## Regression and evidence strategy
Because this is repository reference-content work only, no production runtime code or serialized gameplay scene is intentionally modified. The behavioral-regression proof is the final diff: only this assigned SceneIssue's docs/art/evidence/closure metadata may change. Existing runtime behavior therefore has no changed executable path to exercise.

`WorldbuildingGalleryShowcase` is the issue's context scene, but existing in-engine town visuals are explicitly non-authoritative and final in-engine implementation is out of scope. A new or modified built scene would contradict that boundary, so built-scene evidence is **not applicable to acceptance of the authored reference pack** unless implementation work is unexpectedly required. If any runtime/scene file is touched, this assumption is invalid and the relevant built-player validation becomes mandatory before closure.

## Verification and closure
- Maintain `tasks.md`; do not close with unchecked work.
- Confirm the final feature diff remains inside this assigned SceneIssue directory.
- Use `ci-test/fixes/agent-2` for the repository-required exact-SHA targeted CI request, and verify the reported source SHA is the intended feature head.
- Inspect CI evidence; fix product failures, retry only documented infrastructure failures.
- Record exact request/run/artifact evidence in the issue package.
- Atomically move `SceneIssues/open/20260907-122100-000-MountingForceTownArtDirectionSourceOfTruth` to `SceneIssues/closed/...` with `status=fixed`, resolution, and `resolvedUtc`; never use `pending`.
- Promote `fixes/agent-2` through a PR to `master` with auto-merge; never push the exact branch head directly to `origin/master`.
