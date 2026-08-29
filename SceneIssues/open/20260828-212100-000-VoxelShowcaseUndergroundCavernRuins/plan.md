# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver a natural walkable mouth, prolonged organic descent, huge irregular cavern with varied geology, reachable aged ruin, exactly two grounded readable statues, sparse supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal movement/collision/streaming, direct AAA visual review, and bounded cost.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` are authoritative.

## Latest evidence and discriminated hypotheses
Corrected exact request `d0bc880da1575b27795dbbcab9cf6bb404ea32c2` / run `33280968525`, first-parented directly on merged feature source `7d6ea2a5255069c8c6ba5a14a8147e40fc163bce`, is functionally green. Focused PlayMode passes. The standalone built player uses the intended 95-second `-voxel-underground-cavern-traversal` profile, reaches all 38/38 semantic waypoints through production `CharacterMotor` movement/collision/streaming, logs final completion at 74.6s, captures eight frames, and reports no assertion/startup/runtime failure.

Direct review of all eight rendered frames still fails the visual gate. The long descent and destination remain dominated by bright repetitive rectangular/banded solid surfaces and corridor-like cross-sections. The final post-completion frame reaches the ruin but does not present a huge dark irregular natural cavern, and the intended two flanking humanoid statues are not both clearly readable.

This rejects three prior hypotheses as sufficient fixes: capture duration is not the cause because the final frame is post-completion; route semantics/collision are not the cause because traversal is fully green; changing cave `Rock` from `DarkStone` to `Stone` is not sufficient because final7 is visually worse/brighter while preserving the same corridor read.

Canonical material tracing explains the failed material assumption. `GameMaterialIds.Stone` is a player-buildable textured row using `StoneTexture`, not a special geological-only surface. `DarkStone` uses a separate dark-stone texture with comparable hardness; `Slate` is planar and materially weaker; `Bedrock` is dark/triplanar but indestructible. A second blind ID swap would either repeat the presentation mistake or change gameplay semantics. Explicit ruin/statue masonry remains independently authored and must keep its architectural identity.

The remaining source hypothesis is therefore structural plus presentation-owned: some host/naturalization/destination solid surfaces visible from the production route remain too planar/rectilinear, and the material/coating path amplifies that read. The next source pass must identify those exact authoring owners against final7 evidence before changing code.

Request `03f59048` / run `33278987598` remains diagnostic only: it supplied `scene_issue` plus `replay_seconds=60`, causing the workflow to select generic SceneIssue capture instead of the cavern test-filter profile. No shared workflow/capture-helper change is required.

## Selected repair strategy
Keep generic movement, renderer/light contracts, preload behavior, acceptance thresholds, the eight-light cap, and the 55,000,000-write supported-device ceiling unchanged unless runtime evidence proves an owning shared defect.

1. Trace the full route host, route core, naturalization lobes, destination host shell, cavern boundary finish, circulation reassertion, and any weathering/coating writes to identify which authored solids produce the rectangular walls visible in final7.
2. Trace the rendering definition of every candidate natural-rock material/coating before choosing presentation changes. Preserve cave gameplay hardness/destruction semantics; do not repurpose `Bedrock`, weaken to `Slate`/`Dirt`, or globally retune a shared material just to fix one scene without blast-radius evidence.
3. Repair geometry at the reusable cave-authoring owner: irregularize or remove visible planar host faces outside the protected walkable core, ensure the destination chamber exposes overlapping natural boundary lobes/recesses rather than a corridor shell, and reassert circulation only where needed for normal movement.
4. If the existing material vocabulary cannot produce a dark natural surface without changing gameplay semantics, prefer a narrowly owned game-material presentation addition only after proving its registration/render/simulation blast radius; otherwise reuse an existing semantically compatible row. Do not add shaders, render passes, scene-only renderer overrides, or camera tricks.
5. Preserve the focal architectural contrast: aged ruin and exactly two grounded humanoid statues must remain structurally/materally distinct from geology and become readable from the derived front-approach waypoint.
6. Strengthen focused production regression around the actual structural/material cause while retaining determinism, route completion, visual-finish ceilings, total-write ceiling, and eight-light ceiling.
7. Re-run the same canonical 95-second built-player evidence path. The moving reveal remains authoritative: daylight mouth -> prolonged varied descent -> huge dark irregular cavern/formations -> aged ruin with exactly two readable flanking statues.

## Blast radius and cost discipline
Final7 exact focused metrics on source `7d6ea2a5` establish the pre-repair baseline: `sections=68`, `routeVoxels=3248`, `routeDistance=1352`, `routeLateralVariation=120`, `routeLights=8`, `naturalizedSections=60`, `naturalizationWrites=3,338,101`, `ruinWrites=385,440`, `finishBoundaryLobes=15`, `finishFormationAnchors=6`, `finishRuinLayers=8`, `finishRuinSupports=5`, `finishWrites=3,589,591`, and `writes=33,688,157` against the 55,000,000-write ceiling. Lights are already at the eight-light cap. Any geometry repair must stay bounded and should spend existing write headroom on silhouette quality rather than increase lighting or renderer complexity.

Near-endpoint render telemetry in final7 is roughly 256k-291k vertices, 526k-591k indices, and 287-298 draws, with a transient streaming spike around 1.1M vertices / 2.28M indices / 582 draws. The repaired exact run must be compared against these values and supported-device limits; do not infer cost safety solely from authoring counters.

## Remaining gates
Keep the assignment `open`. After the structural/material repair and regression are final, reconcile current `origin/master` into `fixes/agent-3` if it advanced. Then create one canonical post-repair request on `ci-test/fixes/agent-3` whose first parent is the exact final feature SHA and whose only transport delta is `.github/test-request.json`; leave feature-branch `.github/test-request.json` untouched, do not create another transport mechanism, and do not replace queued CI. The request must use the cavern PlayMode test filter with empty `scene_issue` and `replay_seconds` so the proven 95-second production profile owns real-player evidence.

Require focused PlayMode plus built-player traversal/capture green. Directly inspect every useful final frame for mouth, descent, natural cavern scale/geology, formations, ruin, exactly two statues, localized lighting, circulation/intersections, and placeholder/blocky artifacts. Record exact authoring/render/light cost evidence and validate every acceptance criterion. Only after those gates are green move this assignment `open` -> `pending`, complete pending metadata, then `pending` -> `closed` with `status=fixed` and `resolvedUtc`; re-check/merge current `origin/master` and promote the exact validated feature head non-force, retrying only if master advanced.
