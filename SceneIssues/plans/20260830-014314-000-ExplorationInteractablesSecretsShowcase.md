# Exploration Interactables and Secrets Showcase

- [x] Verify agent-2 assignment, branch, `AGENTS.md`, `SceneIssues/issue-readme.md`, common workflow, issue metadata, and capture inventory.
- [x] Inspect the supplied capture metadata/marked region. **Blocker:** `captures/Primary__Scenepng.png` is referenced by `issue.json` but absent from the issue directory, so pixel-level inspection is unavailable; continue from the marked-region metadata and runtime evidence.
- [x] Discriminate competing hypotheses: pressure/lever/destruction/signals/persistence/presentation already exist; the missing acceptance behavior is primarily showcase composition plus a reusable semantic proximity source. Prior trapdoor/reset work is shared infrastructure but is not one of this issue's four acceptance behaviors.
- [ ] Add the narrow reusable proximity-trigger source and a generic authored-scene registry entry point; keep showcase IDs/layout policy in composition.
- [ ] Compose the four marked-region mechanisms adjacent to the Primary hub: proximity sliding door, pressure-plate door, lever bridge, and breakable secret wall with revealed rubble/path.
- [ ] Add a PlayMode behavioral regression that directly drives the WorldObject interaction interface and proves open/close, press/release, extend/retract, destruction/reveal, and persistence.
- [ ] Validate `Assets/Scenes/Primary.unity`, deterministic descriptors/connections, and bounded blast radius/cost (small fixed scene; no terrain-wide regeneration or per-frame allocation loop).
- [ ] Stage the assigned issue pending with exact feature metadata, then use only `ci-test/fixes/agent-2` for the exact-SHA targeted-CI request without replacing queued/running CI.
- [ ] After green exact-SHA CI, complete pending metadata, move pending→closed with status `fixed`/`resolvedUtc`, merge latest `origin/master`, and push the exact feature head to `origin/master` non-force.

## Evidence notes

- `WorldObjectBehavior` already implements pressure plate enter/exit, lever toggles, breakable-wall destruction, and connection-driven actions.
- `WorldObjectSceneRegistry` already preserves per-parent state across unload/reload and snapshot/restore.
- `WorldObjectPresentationPlan`/Unity presentation already handle door, drawbridge, lever, pressure plate, and breakable-wall visual/collider state.
- The Unity presentation sink does not itself dispatch trigger-enter/exit events. This issue's required behavioral regression explicitly drives the authoritative interaction API directly, so expanding into a new player-controller collision router is outside this defect's demonstrated scope.
