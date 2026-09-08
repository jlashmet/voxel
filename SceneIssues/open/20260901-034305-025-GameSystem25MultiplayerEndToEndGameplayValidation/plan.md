# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact production build with separate authority/client OS processes proving formation, durable identity + baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity, join-in-progress, repeated reconnect, persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` item remains binding.

**Ownership / architecture:** use only production Application/Sessions/Net/Kentridge/GameplayReplication/Persistence/gameplay seams plus generic validation orchestration. No fake networking, parallel authority, privileged mutation command, alternate transport, or weakened freshness rule. `Game.WorldObjects.Runtime` remains headless with module-local unit coverage. Application owns the session lifecycle; Kentridge Playable owns separate-process runtime proof and structural release coverage under its module `Validation/Release/` surface.

## Material results

Earlier exact runs proved generic process isolation/build identity, provider/UTP admission, joined-party startup, durable roster identity, replication readiness, and repository-owned smoke/release selection. Exact source `6401622dab168f474aa2abc19eb8828e36cbaa07` narrowed the remaining Sessions failure to a test-only `IReadOnlyList.Count` assertion; `6a34fc66d728b90d83379cff81671065b0b44976` corrected that assertion without production behavior changes.

Exact source `ae08ee1c97bbfdc8106e525db58147e285a4c54e`, request `5bf5078c06f895d637431b7e9ca41fdfac0cc4d9`, run `34202599000`, job `101494323685`, artifact `10048699677`, digest `sha256:6738ef445f277eaa228d6106951dfc7f904f816827b67fa99f85560cb8a917b8` passed all 86 tooling tests, every selected persistent EditMode assembly, Application/Audio players, and the Kentridge encounter player. Its three-process topology then proved exact roster/baseline convergence, exactly-once contention (`quantity=1`, pickup disabled, two accepted inputs), shared progression `Completed`, and production Continuity `ConnectionInterrupted`. It timed out only waiting for authority `combat-vitality-converged`.

The discriminator is production `ApplicationFlowCoordinator.Update`: after `PromoteWhenReady()` changes Application to `InGame`, it no longer calls the still-Running `IGameSessionControl.Tick`, freezing deterministic gameplay update steps. That prevents the production forest Encounter/Combat/Vitality extension from observing proximity after interruption. Selected fix `b541b59e51675d656117f46beecf39801266ae4e` continues the running session tick in `InGame`; regression `a69ba7a2629f9e27d7a9f07e68479c85aef061be` asserts post-readiness updates still advance the session.

## Current discriminator / remaining gates

Hypothesis A: continued InGame session ticks allow the real forest encounter and authenticated combat input to produce/replicate the expected Vitality delta, after which reconnect/current-state/leave proof proceeds. Hypothesis B: execution reaches a narrower combat-input or replication defect. Next experiment: exact-SHA targeted CI from the current feature head; only matching built-player milestones may close runtime boxes.

After smoke succeeds, run/verify structural release capacity/JIP/reconnect/rehost evidence, complete every remaining checkbox, record final exact-SHA evidence, close `open -> closed`, reconcile current master, then PR + immediate auto-merge and monitor the required `affected`/standalone Kentridge gate until merged closure is visible on master.
