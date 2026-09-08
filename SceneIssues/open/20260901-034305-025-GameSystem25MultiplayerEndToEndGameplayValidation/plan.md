# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact production build with separate authority/client OS processes proving formation, durable identity + baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity, join-in-progress, repeated reconnect, persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` item remains binding.

**Ownership / architecture:** only production Application/Sessions/Net/Kentridge/GameplayReplication/Persistence/gameplay seams plus generic validation orchestration. No fake networking, parallel authority, privileged mutation command, alternate transport, or weakened freshness rule. `Game.WorldObjects.Runtime` is headless with module-local unit coverage; Kentridge Playable owns separate-process runtime proof. Structural release coverage remains repository-discovered under `Assets/Game/Composition/Kentridge/Playable/Validation/Release/`.

## Material results

Earlier exact runs prove generic process roles/state isolation, exact executable/source identity, semantic waits, stale-milestone prevention, Application/provider/UTP admission, and durable identity preservation. Run `34170219113` exposed a launch-order deadlock; `b317a81244c6f676982e67ee9c04aeda24b030a8` launches both clients before join waits. Source `56fb5de177b7b3eda9798588565ac898d26023fd`, request `c523e765adc06838e7eefb3f29131be269470f25`, run `34171740485`, digest `sha256:52f075f761755436e1dd9746726d928d47ab2abd6595ea3158ccf31f9c23624d`, then proved the same three-member topology/baseline across authority/A/B and exposed client-local tick input; `b8c2d3e1b2f15df4ba3532f31f3939a554aaeba0` + `b09fc663b064a025adfe9b906dcd281663ce6d89` use replicated authoritative `ServerTick`.

Subsequent exact gates exposed narrow compile-boundary drift only. `607abf9fbe855e3f32860fc9a2c7987d9039842e` restored `Game.Sessions.Api`. Request `c11b769f1ae25c29ad43305a97d4dbfcbf315991` / run `34178744571` automatically selected both multiplayer smoke and structural release, satisfying T25-051, then exposed obsolete `_session.State`; `2b0a6899869bb09f8a971ca8a65ad9f6c260b3f9` uses `Snapshot.Lifecycle`.

Source `29a1f35c2a152dbc89c27742beb0fbbc10f260b2`, request `fad4a1762b240966b77e9787f7f45e67addf2161`, run `34185364415`, digest `sha256:d0277120de43196f14607e52482353a4f197d6bda84fdf0afdc86dad1fbf5879`, exposed the missing `Game.Composition.Campaign.Runtime` asmdef dependency; `b46525ad096f6477c7c37e77823e64c88ceefc34` adds it. Source `567d3d506896cc9490b124cea9516a96fbab27b6`, request `e31215ae533713bae6f2f4cfbb037bc88ad903d0`, run `34188578469`, digest `sha256:bfc61be47c1796e45ccbb3b62cd479aacb4125ef6f146174bd4683faa441e484`, exposed missing `VoxelEngine.Net.Runtime.Protocol`; `11225482e55f987a0176f1256bec86b612160a9f` adds it.

Source `ec05793b82fa2104a34c6db420488e093d956870`, request `d73572f54e8b2ad05c25d3b6dee70d74e9c6b2a2`, run `34192950690`, artifact `10043889627`, digest `sha256:f83ae39503fc12a745a9182e4b955a7fec19b8e054766d56398bbeeee44e3728`, passed planning/tooling but failed Unity compilation before tests/players: release validation called production `WorldBuilderTownAuthoring` without `Game.WorldBuilder.Runtime`. `f191251fefa59b64df52dd3116fbf9bd07936c02` adds only that missing import.

## Current discriminator / remaining gates

Hypothesis A: compile now reaches persistent tests and the authoritative-tick fixes carry smoke/release through their intended milestones. Hypothesis B: exact execution exposes the first remaining production ordering/state defect. Next experiment: exact-SHA targeted CI from current feature head; check runtime boxes only from matching built-player artifacts.

1. Green exact-head smoke and structural release proof; fix only demonstrated failures. T25-051 is already proven.
2. Mark remaining T25-010–052 runtime evidence truthfully.
3. Close `open -> closed` with metadata, merge current master, revalidate if required, then PR + auto-merge + required `affected`/Kentridge full-app gate; finish only after merged closure is visible on master.
