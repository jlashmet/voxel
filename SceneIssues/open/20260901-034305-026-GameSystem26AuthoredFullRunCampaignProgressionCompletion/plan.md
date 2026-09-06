# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Acceptance / selected architecture

The semantic campaign extends the recovered Kentridge opening through Rorik/Moordell/Rossdam/Logan to one authored System15 terminal condition; optional content is non-gating. Opening/continuation remain plain content helpers, Story consumes semantic facts, System11 owns objective truth, System15 owns terminal outcome, and System16/14 own persistence/restore. No parallel chapter/phase authority was introduced.

System26 semantic implementation is already exact-SHA green: campaign/fresh-graph restore passed run `33941421358`; Story-owned validation passed `33942377377`; feature `86911d9dec109c588310754f28c7a5644aed687a`, request `57a0b1f00615d0901e2d25ee0d2216296b13f163`, run `33944532957` passed repository-derived automatic module validation. No System26 production/test change exists after that feature SHA; later commits are blocker bookkeeping only.

## Current prerequisites / remaining gates

`origin/master=ef475182b866eabfe8e1d1a39c82bf7810a03f49`; its latest change is Astra screenshot-review plumbing and does not land a System26 prerequisite.

**Multiplayer:** System25 is the binding prerequisite. Agent-7 implemented the first production-topology prerequisite at source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; current `fixes/agent-7=ce141db1517eed0fe0b08e9dc4b445f6a654310b` records that state. Joined clients observe the matching active party projection and start their local Application/session-orchestration graph once without issuing the leader-only Sessions Start command. Directly-parented request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`, run `34002524305`, remains queued and must be left untouched. T25-010A/010/011 and identity/convergence/contention/combat/shared-progression/reconnect/leave/release cases remain unchecked, so T26-043 stays blocked on landed System25 production multiplayer acceptance.

**Full-run physical realization:** macro-world remains blocked at docs head `596a10f11ebfb3a0b868fba11808849a0470494a`. Renderer fail-before source `da3f5be338c57f5fe99ad4324405422e78c3918e`, request `6ddc72724c6653538be5c5a9818ebee059726264`, run `33999899224`, validly proved the old bounding-box substitution. Agent-1's current repair feature is `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b`; directly-parented pass-after request `fc6c3320d9b986b8d2401fcae0a17de80d286691`, run `34003412217`, is now queued and must be left untouched. Even a green discriminator will not by itself satisfy renderer acceptance: production-quality CPU evidence, GPU restoration/parity, final visual/performance proof, cleanup, closure and master landing still remain before macro-world can resume.

System24 is related, not binding: agent-2 source `14551b3475a0c96d340f647b48d8692022a5749f` has directly-parented request `5b86ed6c56f5df214d6aab2555c21f943f2d35a5`, run `34001710667`, in progress; leave it untouched.

No independent System26 task is currently available. Keep open until System25 production multiplayer acceptance and macro-world production full-run realization land, then sync master, complete T26-021/022/043/044/045/046/053/054, run final exact-SHA validation, close, and promote by PR + auto-merge.
