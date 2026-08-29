# Plan

## Acceptance and latest evidence
`VoxelShowcase` must provide a natural walkable mouth, prolonged organic descent, a huge irregular dark cavern with varied geology, a reachable aged ruin, exactly two grounded readable statues, sparse supported lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through production movement/collision/streaming, direct rendered review, and bounded cost. `SceneIssues/feature-readme.md` is absent, so `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` apply.

Exact request `d0bc880d` / run `33280968525` on source `7d6ea2a5` is functionally green: focused PlayMode passed, the 95-second built player reached 38/38 waypoints, and eight frames were captured without runtime/assertion failure. Visual review still fails: the descent/destination read as bright repetitive rectangular/banded corridors, the final cavern does not read huge/natural/dark, and both statues are not clearly readable.

This rejects capture duration, traversal/collision, and the `DarkStone` -> `Stone` swap as sufficient fixes. `Stone` is a normal textured build material; `Slate` changes durability and `Bedrock` is indestructible, so no further material-ID guess is acceptable.

## Repair
1. Trace route host, naturalization, destination host/boundary finish, circulation reassertion, and coating/material writes against final7 frames to identify the planar visible solids.
2. Repair the owning reusable cave authoring so visible host faces are irregular/natural outside the protected walkable core and the destination exposes overlapping cavern lobes/recesses rather than a corridor shell.
3. Preserve ruin/statue architectural identity, normal gameplay movement, determinism, eight-light cap, and 55,000,000-write ceiling. Avoid renderer/camera hacks and global material retuning.
4. Strengthen focused regressions for the actual structural/material invariant.
5. Re-run the canonical 95-second built-player reveal: daylight mouth -> varied descent -> huge dark irregular cavern/formations -> aged ruin and exactly two readable flanking statues.

## Cost and final gates
Final7 baseline: 68 sections, 60 naturalized sections, 3,338,101 naturalization writes, 3,589,591 finish writes, 33,688,157 total writes, and 8 lights. Endpoint rendering was about 256k-291k vertices, 526k-591k indices, and 287-298 draws; transient streaming reached ~1.1M vertices / 2.28M indices / 582 draws. Compare repaired CI against these values.

Keep the assignment open. After repair/tests are final, merge current `origin/master` if advanced, then make one canonical `ci-test/fixes/agent-3` request directly from that exact feature SHA using the cavern PlayMode filter with empty `scene_issue`/`replay_seconds`; do not edit feature `.github/test-request.json`, add transports, or replace queued CI. Close only after exact-SHA focused CI, built-player traversal, every useful frame, cost, and every acceptance criterion are green.