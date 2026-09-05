# Gameplay residency / simulation streaming — implementation plan

**Ownership:** `Game.Residency.Api` / `Runtime` coordinates semantic fidelity only. Characters, CharacterAI, WorldObjects, Encounters, Persistence, WorldBuilder, GameplayReplication and VoxelEngine Streaming retain authoritative state/lifetime ownership.

## Acceptance and result

One stable gameplay identity must survive `Dormant` / `Coarse` / `Detailed`; independent demand leases aggregate deterministically; Detailed waits for physical readiness and quiesces before physical release; client interest/presentation remain separate; persistence restores owner state rather than residency state; generated content scales without global loaded-town switches.

Two hypotheses were tested. Existing `IRegionStreaming` alone was insufficient because engine eviction could bypass load-now/evict-later ownership. The selected design adds a Streaming-owned ref-counted physical residency lease and a game-level semantic coordinator over existing owner adapters.

## Implemented boundary

`semantic target + independent demands` → `GameplayResidencyCoordinator` → owner adapters. Highest fidelity wins deterministically. Detailed spatial promotion acquires `IRegionResidencyLease`, waits for readiness, then realizes. Demotion—including coordinator teardown—quiesces Detailed consumers before releasing the lease. Failed/pending transitions remain safe and quiescent. CharacterAI Coarse fidelity advances semantic life state without detailed perception/navigation. WorldObject and Encounter state remain owner-owned. Semantic proximity hysteresis composes with explicit encounter/control pins.

Independent proofs cover Character/AI, WorldObject, Encounter, real Streaming pins, a 64-NPC WorldBuilder fixture (48 Dormant / 12 Coarse / 4 Detailed), late-client current-state GameplayReplication, and a production `SessionPersistenceService` fresh-graph restore. Repository 30 Hz simulation and ≤0.5 ms Streaming main-thread budgets were not weakened.

## Final validation

Earlier failures isolated and fixed invalid short module scenarios, a Kentridge legacy-input blocker, the Input-System dependency on the wrong asmdef, and unsafe Detailed teardown ordering. The first teardown fixture naming collision was test-only and corrected.

Final exact request `951785fa43f947c214b681634f19e37ae75f825e` is parented directly to feature SHA `e20eeb2cc4796d64c0360bd971298a806e187dcf`; run `33937054327` succeeded. All 18 affected EditMode assemblies passed, Residency and Streaming standalone module validations passed, canonical Kentridge integration passed, and the mandatory SceneIssue replay completed with zero assertion failures and no legacy-input exception. The request-path-only replay metadata defect was corrected without changing the validated feature SHA.

All acceptance and checklist work is complete. Closure is direct `open/` → `closed/`; then merge current `origin/master`, open/update the PR, enable auto-merge, and monitor the required `affected` gate through merge.
