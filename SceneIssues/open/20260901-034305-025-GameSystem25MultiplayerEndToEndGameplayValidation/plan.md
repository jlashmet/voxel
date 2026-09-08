# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity, join-in-progress, repeated reconnect, persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` item remains binding.

**Ownership / architecture:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/Persistence/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, alternate transport, or weakened freshness rule. `Game.WorldObjects.Runtime` remains headless and uses module-local unit coverage; Kentridge Playable owns separate-process runtime proof. Release coverage lives structurally under `Assets/Game/Composition/Kentridge/Playable/Validation/Release/` and is repository-discovered.

## Material results

Harness process roles, state isolation, exact executable/source identity, semantic waits, stale-milestone prevention, and diagnostic artifacts are proven by earlier exact runs `33937957149`, `33986100313`, `33987833161`, and `33995470352`. Application/provider/UTP admission and identity preservation are proven by `34005489004` and `34011275001`.

Compile/product-boundary drift exposed by runs `34153302952`, `34154154340`, `34162468889`, and `34165608877` was repaired narrowly: missing direct assembly references/usings, struct-safe party projection handling, public WorldBuilder authoring, explicit input-sink typing, and Unity `Application` qualification.

Run `34170219113` reached the real separate-process topology and exposed a scenario deadlock: client A's completed join was awaited before launching client B although authority requires all three members before Start. `b317a81244c6f676982e67ee9c04aeda24b030a8` launches both clients before join waits.

Exact source `56fb5de177b7b3eda9798588565ac898d26023fd`, request `c523e765adc06838e7eefb3f29131be269470f25`, run `34171740485`, artifact digest `sha256:52f075f761755436e1dd9746726d928d47ab2abd6595ea3158ccf31f9c23624d`, proved authority + clients A/B reach the same three-member topology and identical baseline digest `a556f1f1650dda17`. Both clients sent contention input, but authority timed out because validation populated `C_PlayerInput.tick` from each process's local frame count. `b8c2d3e1b2f15df4ba3532f31f3939a554aaeba0` instead samples the admitted production player's authoritative `ServerTick`; `b09fc663b064a025adfe9b906dcd281663ce6d89` applies the same demonstrated repair to the release mutation input.

Exact source `a4a023184c3084d5975b0d221d617592ce6eaa20`, request `922b92c25027bf9cf7db33907ccacba7660f6558`, run `34175096808`, artifact digest `sha256:d006056b25d9087c9998c73653a4f15f81129a10597c28ac3c8619a89bf34e8f`, failed before player execution at `ApplicationFlowCoordinator.Persistence.cs:70`: `PartySessionCommandResult` lacked its `Game.Sessions.Api` import. Commit `607abf9fbe855e3f32860fc9a2c7987d9039842e` adds only that demonstrated import.

T25-030–034 and structural release T25-040–043 are implemented but remain unaccepted until green exact built-player evidence.

## Current discriminator

Hypothesis A: current head compiles and the authoritative-tick repairs carry smoke through contention/progression, interruption/reconnect, absent-period combat recovery, explicit leave, then release through capacity/JIP/repeated reconnect/persisted rehost. Hypothesis B: the next exact execution exposes the first remaining production ordering/state defect after inputs reach authority. **Next experiment:** exact-SHA targeted CI from this plan commit; check task boxes only from matching green built-player artifacts.

## Remaining gates

1. Green exact-head smoke proof; fix only demonstrated failures.
2. Green structural release proof for T25-040–043 and automatic selection T25-051.
3. Mark T25-010–052 truthfully from exact evidence, reconcile current master, close open -> closed with metadata, run any post-reconciliation exact gate required by source changes, then PR + auto-merge + required `affected` gate; finish only after merged closure is visible on master.
