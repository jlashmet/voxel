# Plan

## Acceptance and latest evidence
`VoxelShowcase` must provide a natural walkable mouth, prolonged organic descent, a huge irregular dark cavern with varied geology, a reachable aged ruin, exactly two grounded readable statues, sparse supported lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through production movement/collision/streaming, direct rendered review, and bounded cost. `SceneIssues/feature-readme.md` is absent, so `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` apply.

Exact request `d0bc880d` / run `33280968525` on source `7d6ea2a5` is functionally green: focused PlayMode passed, the 95-second built player reached 38/38 waypoints, and eight frames were captured without runtime/assertion failure. Visual review still fails: the descent/destination read as bright repetitive rectangular/banded corridors, the final cavern does not read huge/natural/dark, and both statues are not clearly readable.

Request/run `33282354956` from source `285ebe7d327e8ef964c2d3af04d0985f3db300fb` was also functionally green and reached 38/38 with eight frames, but it is diagnostic only: direct review still showed rectilinear destination channels, source inspection found `UndergroundCavernRuinAuthoring.AuthorProtectedDestinationRoute` remained an axis-aligned box carve, and current master had advanced after the branch's previous merge.

This rejects capture duration, traversal/collision, and material-only changes as sufficient fixes. `Stone` is a normal textured build material; `Slate` changes durability and `Bedrock` is indestructible, so the production cave remains on `DarkStone` and no further material-ID guess is acceptable.

## Repair
1. Replace the remaining private destination box carve with the same reusable `UndergroundCavernCirculationProtection` rounded sweep used after visual finish. Both safety passes now use overlapping cylindrical empty-space nodes derived from cavern/ruin bounds and facing, without showcase coordinates or long planar side faces.
2. Make the canonical production acceptance test execute the rounded-plan invariant itself before normal CharacterMotor traversal: overlapping node geometry, rear overlap, ruin-throat reach, deterministic plan resolution, existing write ceiling, and eight-light ceiling remain enforced in the one targeted test.
3. Preserve ruin/statue architectural identity, normal gameplay movement, determinism, eight-light cap, and 55,000,000-write ceiling. Avoid renderer/camera hacks and global material retuning.
4. Current master `2b100aa47ee3c9c349355d8f3bdd41ab3016582d` was merged into the repaired feature as real two-parent merge `6b59cb81b50176c9d8c344c6aad83b5d8fc8e148`; re-check master immediately before the canonical request.
5. Re-run the canonical 95-second built-player reveal: daylight mouth -> varied descent -> huge dark irregular cavern/formations -> aged ruin and exactly two readable flanking statues.

## Cost and final gates
Final7 baseline: 68 sections, 60 naturalized sections, 3,338,101 naturalization writes, 3,589,591 finish writes, 33,688,157 total writes, and 8 lights. Endpoint rendering was about 256k-291k vertices, 526k-591k indices, and 287-298 draws; transient streaming reached ~1.1M vertices / 2.28M indices / 582 draws. Diagnostic run `33282354956` reported 33,711,157 total writes, 3,338,101 naturalization writes across 223 nodes, 3,580,222 visual-finish writes, 20 preloaded regions, and 8 lights. Compare the repaired exact-SHA CI against these values and inspect its actual render/chunk statistics rather than carrying diagnostic numbers forward.

Keep the assignment open. After repair/tests are final, re-check and merge current `origin/master` if it advanced, then make one canonical `ci-test/fixes/agent-3` request directly from that exact feature SHA using the cavern PlayMode test filter with empty `scene_issue`/`replay_seconds`; do not edit feature `.github/test-request.json`, add transports, or replace queued CI. Close only after exact-SHA focused CI, built-player traversal, every useful frame, cost, and every acceptance criterion are green.
