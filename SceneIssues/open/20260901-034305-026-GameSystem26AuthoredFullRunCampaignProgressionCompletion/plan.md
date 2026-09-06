# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Acceptance / selected architecture

The semantic campaign extends the recovered Kentridge opening through Rorik/Moordell/Rossdam/Logan to one authored System15 terminal condition; optional content is non-gating. Opening/continuation remain plain content helpers, Story consumes semantic facts, System11 owns objective truth, System15 owns terminal outcome, and System16/14 own persistence/restore. No parallel chapter/phase authority was introduced.

System26 semantic implementation is already exact-SHA green: campaign/fresh-graph restore passed run `33941421358`; Story-owned validation passed `33942377377`; feature `86911d9dec109c588310754f28c7a5644aed687a`, request `57a0b1f00615d0901e2d25ee0d2216296b13f163`, run `33944532957` passed repository-derived automatic module validation. No System26 production/test change exists after that feature SHA; later commits are blocker bookkeeping only.

## Current prerequisites / remaining gates

`origin/master=ef475182b866eabfe8e1d1a39c82bf7810a03f49`; its latest change is Astra screenshot-review plumbing and does not land a System26 prerequisite.

**Multiplayer:** System25 is the binding prerequisite. `fixes/agent-7=7768e4bec1a92989fdb9e66d40e517b4c4be8fc7` owns production `ApplicationFlowCoordinator` + `ISessionFormationService` + provider/UTP authority/two-client topology. T25-010/011 and convergence/contention/combat/progression/reconnect/leave cases remain unchecked; `ci-test/fixes/agent-7` still targets green diagnostic request `4fdb3400ef72c2095adcba6353f09c70724dd81a`, so no production-topology exact request exists. T26-043 waits on landed System25 acceptance.

**Full-run physical realization:** macro-world remains blocked at docs head `ec4798cc014755f8b313eea6801c8f36ab315327`. Renderer baseline `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa` passed run `33996360570`, but VoxelShowcase evidence remained prototype/blockout quality with giant far-world masses and no GPU requests. Agent-1's fail-before source `da3f5be338c57f5fe99ad4324405422e78c3918e`, directly-parented request `6ddc72724c6653538be5c5a9818ebee059726264`, run `33999899224`, completed `failure` in automatic module validation while its standalone 45-second VoxelShowcase replay succeeded. The requested `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper` old-AABB discriminator is therefore classified as expected fail-before behavioral evidence, not infrastructure or acceptance success; artifact `9979637933` preserves the result. Agent-1 already committed the bounded frustum repair at `a164456a9eac5091ec3e5d6c2e03a9de7b675199` and current feature head `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b` records it, but `ci-test/fixes/agent-1` still points to the completed fail-before request, so no pass-after exact request exists yet. Renderer CPU/GPU visual acceptance and closure remain open; macro-world cannot resume yet.

System24 is related, not binding: run `34000242054` on `b0b1e75ba97696a83bc0d2a5658e3fe28e22022f` passed the persistent EditMode/PlayMode phase and reached canonical Kentridge built-player validation, then failed when `MoveToDestination` steered into pub geometry before exit. Agent-2 corrected the driver at `14551b3475a0c96d340f647b48d8692022a5749f`; directly-parented request `5b86ed6c56f5df214d6aab2555c21f943f2d35a5`, run `34001710667`, is queued and must be left untouched.

No independent System26 task is currently available. Keep open until System25 production multiplayer acceptance and macro-world production full-run realization land, then sync master, complete T26-021/022/043/044/045/046/053/054, run final exact-SHA validation, close, and promote by PR + auto-merge.
