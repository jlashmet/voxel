# Plan — Kentridge vegetation meadow density

## Scope
Implement only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Confirmed current architecture / evidence
- `KentridgeDefinition` exposes additive per-region ecology policy: allowed vegetation, density, deterministic seed, meadow radius, route clearance, slope controls, exclusion classes, and ambient-animal allowlist.
- `KentridgeRegionLife` realizes policy through production surface sampling; no scene-local grass GameObjects or hand-authored scatter coordinates.
- Packed procedural grass expands each semantic grass instance deterministically to 5–15 blades; renderer chunks at 36,000 blades.
- Shared wind is already time-varying through `_GrassTime` / `Time.time`; preserve it unless built-player evidence disproves visible motion.
- Runtime diagnostic from the prior built player measured 11,478 semantic grass instances / 114,580 blades total; the connected primary meadow alone measured 5,777 instances / 57,589 blades, 8 grass mesh chunks total, and zero excluded-surface leakage.
- Final CI request `33240721951` passed the focused PlayMode acceptance and built the/runs the real Kentridge player, but its visual replay armed and never pinned. Direct scene inspection proves the current scene camera is the root object `Kentridge Player Camera`; the issue metadata still targets obsolete `FirstPerson-AIO/FirstPersonCharacter/Capsule/PlayerCamera`. This is assignment-local capture metadata, not a production camera/harness defect.

## Remaining discriminator / gates
1. Correct only this assignment's `issue.json` replay hierarchy to `Kentridge Player Camera`; leave scene/gameplay/harness code unchanged.
2. Run the exact focused PlayMode acceptance plus built-player replay from the corrected feature SHA using only `ci-test/fixes/agent-5`.
3. Inspect the corrected built-player meadow view. Require dense procedural grass at player height, `>=3000` primary-meadow blades, zero invalid-surface leakage, and visible time-separated wind motion from a stationary camera.
4. Record any CPU/GPU/memory/build-time metrics exposed by the canonical harness; if no corresponding budget metric exists, document that limitation plus measured FPS, instance/blade/chunk topology, and lack of new per-frame CPU work/material churn/GameObjects.
5. Store concise durable verification evidence, complete every task/acceptance checkbox, and perform the repository bookkeeping/promotion sequence only after all mandatory visual gates are satisfied.
6. After green exact-SHA gates, complete pending/final metadata, close with `status=fixed`/`resolvedUtc`, merge any newly advanced master, and non-force publish the exact feature head to master.

## Blast radius / cost
WorldBuilder API changes are additive; non-Kentridge callers keep existing behavior. Runtime changes are confined to Kentridge realization plus one shared deterministic blade-count helper used by the existing renderer. No new shader fork, scene serialization, per-blade GameObjects, per-frame CPU blade animation, or per-frame material allocation is introduced. The replay correction is assignment-local metadata with zero production runtime cost.
