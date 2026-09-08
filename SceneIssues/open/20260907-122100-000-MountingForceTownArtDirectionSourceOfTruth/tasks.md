# Tasks

## Source inventory and roster
- [x] Pin original and secondary Mounting Force source commits and record all authoritative reference paths used.
- [x] Inventory every town/location represented by original scene/map/story references; distinguish towns from wilderness, dungeons, castles, junctions, and encounter-only locations.
- [x] Search connected reference material for Cambridge and any additional town names not obvious from source filenames.
- [x] Resolve competing source claims and record disconfirming evidence; do not silently merge conflicts.
- [x] Freeze and document the complete canonical town roster before town concept generation.

## Game-wide art direction
- [x] Define the shared Project North Star environment language: shape grammar, architectural abstraction, materials, palette/lighting principles, readable detail hierarchy, stylization, terrain integration, and interior/exterior continuity.
- [x] Define explicit anti-drift rules preventing generic-town convergence and preventing current voxel placeholders from becoming canon.
- [x] Create a game-wide town comparison/index covering identity, palette, materials, silhouette, culture/economy, terrain, infrastructure, motifs, and signature landmarks.

## Per-town pre-concept inventory and briefs
- [x] For every frozen-roster town, lock the exact named/specific buildings, landmarks, public spaces, infrastructure, and source provenance **before** generating that town's concepts.
- [x] For every town, clearly separate historical/source-backed facts from newly derived Project North Star art direction.
- [x] For every town, author the required brief: identity/theme, architecture language, materials, palette, culture/economy, terrain/environment integration, signature motifs/landmarks, infrastructure, and provenance.

## Concept-art deliverables
- [x] For every town, commit at least one finished overall/elevated town concept.
- [x] For every town, commit at least two finished exterior building concepts tied to its locked inventory/roles.
- [x] For every town, commit at least two finished interior building concepts tied to its locked inventory/roles.
- [x] For every town, commit at least one finished street/plaza/public-space concept showing appropriate infrastructure.
- [x] Verify every canonical concept is environment-focused, original, coherent with Project North Star, visually distinct from other towns, and usable as downstream implementation reference; rejected schematic/blockout studies are not canonical deliverables.
- [x] Add a concept catalog mapping each image role to town, required category, depicted named places/infrastructure, and brief requirements.

## Evidence and regression
- [x] Audit the final master→feature diff to prove no production runtime/gameplay/scene implementation changed; if that ceases to be true, add the required behavioral regression and built-scene evidence.
- [x] Record why built-scene evidence is not applicable **provided** the final diff remains docs/art-only: existing gallery visuals are explicitly non-authoritative and final in-engine implementation is out of scope.
- [x] Audit every `expected` acceptance clause in `issue.json` against a concrete committed artifact.

## Exact-SHA CI, closure, promotion
- [ ] Submit the intended feature head through `ci-test/fixes/agent-2` using the documented exact-SHA request workflow; do not replace a queued/running request.
- [ ] Verify exact source SHA, inspect the targeted-CI result/evidence, and record request/run/artifact details.
- [ ] Complete every task/checklist item before closure.
- [ ] Atomically move the SceneIssue from `open` to `closed`, set `status=fixed`, resolution, and `resolvedUtc`; never use `pending`.
- [ ] Open/update the PR from `fixes/agent-2` to `master`, enable auto-merge, and allow repository-required PR checks to promote it; do not push the exact branch head directly to `master`.
