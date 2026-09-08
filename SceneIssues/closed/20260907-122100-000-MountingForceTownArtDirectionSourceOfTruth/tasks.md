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
- [x] Audit the master→feature diff to prove no production runtime/gameplay/scene implementation changed; if that ceases to be true, add the required behavioral regression and built-scene evidence.
- [x] Record why built-scene evidence is not applicable **provided** the final diff remains docs/art-only: existing gallery visuals are explicitly non-authoritative and final in-engine implementation is out of scope.
- [x] Audit every `expected` acceptance clause in `issue.json` against a concrete committed artifact.
- [x] During final PR review, detect the stale six-town sidecar roster contradiction and prevent PR #335 from merging.
- [x] Reconcile `art-direction/index.md` and `art-direction/town-briefs.md` to the canonical seven-settlement roster, explicitly excluding uncorroborated Wharfington and restoring Orc Village/Fairy Village.
- [x] Re-audit every roster-bearing document and the town-folder set for one consistent canonical roster.

## Exact-SHA CI and closure
- [x] Record prior exact source `4ee3148f36c916928a1600aba9bb5a5f65408554` and successful request `22fe801f55fc6ac5ae65248d9d4260ae77f52ad8` / run `34204143335`; this gate predates the final consistency correction.
- [x] Submit corrected exact source `9c92a736d52c818ac951c29b468f70093b9a43ff` through `ci-test/fixes/agent-2` as request `a267944ed2790b484d6885f8374c2d4fe8293874` without replacing queued/running work.
- [x] Verify corrected exact source SHA, inspect the targeted-CI result/evidence, and record run `34215812697`, job `102027158713`, commit status, and artifact details.
- [x] Complete every issue-work task/checklist item before closure.
- [x] Atomically move the reopened SceneIssue from `open` to `closed`, set `status=fixed`, resolution metadata, and `resolvedUtc`; never use `pending`.

## Post-closure promotion
Repository workflow continues after closure: reconcile current `origin/master` if needed, mark PR #335 ready, enable auto-merge, and monitor the required `affected` PR gate through actual merge. These are integration steps, not pre-closure acceptance checkboxes.
