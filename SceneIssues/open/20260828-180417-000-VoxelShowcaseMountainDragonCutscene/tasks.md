# Tasks

## Source / regression gate
- [x] Supersede midpoint-only turn proof with a realized landing-column contract: three separated interior columns per turn retain path floor/headroom, with occupied support under the centre column.
- [x] Make built-player waypoint arrival verify authored vertical elevation using production `CharacterMotor.Position` feet and `Grounded`, not X/Z alone.
- [x] Implement the traversal fix for the reproduced obstruction: scenic/support mass first, reusable `Carve` corridor second, authored walking floor restored last.
- [x] Add a reusable path occupancy/headroom contract using the production motor envelope (0.6 m footprint, 1.8 m body, 0.3 m max step); accepted vertical clearance is 2.4 m / 24 voxels.
- [x] Carve/clear the reusable mountain traversal corridor above authored ramps/landings using shared `PrimitiveMode.Carve`; no scene-local voxel write or new shared primitive is required.
- [x] Emit traversal clearance after every scenic/support fill so later fills cannot repopulate the production motor headroom envelope; preserve the authored walking floor by re-emitting it last.
- [x] Expand the load-bearing mountain feature footprint vertically so every headroom primitive is inside generation bounds (1200 x 306 x 1200 voxels, within the shared 1200-per-axis budget).
- [x] Add semantic occupied-below + clear-above regression across all switchback ramps, every turn landing, final ascent, and summit approach.
- [x] Add regression/evidence that grounded vertical waypoint predicates reject flat X/Z false arrival and airborne passage.
- [x] Add grounded route expectations derived from authored `PathRise`: +4.6 m per switchback tier, +27.6 m at the sixth high point, +28.0 m at summit within 0.75 m.
- [x] Check static blast radius/cost: 76 one-time landform primitives, below the feature envelope of 80 and shared budget of 512; no new update loop/polling/physics work.
- [x] Merge the current `origin/master` bookkeeping head into `fixes/agent-4` before the implementation pass without touching another assignment.

## Exact-SHA bake / built-player gate
- [ ] Use the one authorized final `ci-test/fixes/agent-4` request only once, against the exact source candidate; never replace it or create another CI transport.
- [ ] Have the VoxelShowcase CI pre-test bake generate a source-matched `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` plus `ShowcaseWorld.manifest.txt` for this exact source candidate.
- [ ] Prove the generated startup bake semantically contains mountain mass, every switchback/landing, supported walking columns, 24-voxel headroom, summit support, and dragon occupancy.
- [ ] Verify the exact focused filter is green, including asymmetric-landform, natural-support, headroom, grounded-Y predicate, encounter, and startup-bake acceptance regressions.
- [ ] Traverse the complete evidence route by normal production `AutoWalk -> CharacterMotor.Step` movement with grounded vertical waypoint proof; no jump, teleport, assisted flight, or X/Z-only credit.
- [ ] Save durable built-player captures for normal approach, path base, representative middle/upper switchbacks, supported summit dragon, and exact dialogue.
- [ ] Human-review the exact built-player captures for grounded mountain scale, readable continuous ascent, supported/non-clipped path, supported summit dragon, and `Hello, I'm Mr. Dragon.`
- [ ] Retrieve the accepted generated bake + provenance manifest from the green exact-SHA artifact and commit those generated outputs to `fixes/agent-4` without changing production source.
- [ ] Record measured bake/runtime evidence and confirm no unexpected primitive/build-cost or shared-system blast-radius regression.

## Closure gate
- [ ] After the green exact-SHA workflow gate, complete pending evidence/resolution metadata on `fixes/agent-4` and move only this assignment `open -> pending`.
- [ ] Move only `SceneIssues/pending/20260828-180417-000-VoxelShowcaseMountainDragonCutscene` to `SceneIssues/closed/20260828-180417-000-VoxelShowcaseMountainDragonCutscene`, set `status=fixed` and `resolvedUtc`, and leave every acceptance criterion evidenced.
- [ ] Merge latest `origin/master` into `fixes/agent-4` after closure metadata, then push that exact branch head to `origin/master` non-force; if master advanced, fetch, merge, and retry.
