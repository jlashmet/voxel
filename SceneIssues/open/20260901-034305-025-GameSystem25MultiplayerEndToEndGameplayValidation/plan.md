# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity, join-in-progress, repeated reconnect, persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` item remains binding.

**Ownership / architecture:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/Persistence/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, alternate transport, or weakened freshness rule. `Game.WorldObjects.Runtime` remains headless with module-local unit coverage; Kentridge Playable owns separate-process runtime proof. Release coverage stays structurally under `Assets/Game/Composition/Kentridge/Playable/Validation/Release/` and is repository-discovered.

## Material results

Earlier exact runs prove generic multi-process roles, state isolation, exact executable/source identity, semantic waits, stale-milestone prevention, Application/provider/UTP admission, and durable identity preservation.

Run `34170219113` reached real authority/client processes and exposed a launch-order deadlock; `b317a81244c6f676982e67ee9c04aeda24b030a8` launches both clients before join waits. Exact source `56fb5de177b7b3eda9798588565ac898d26023fd`, request `c523e765adc06838e7eefb3f29131be269470f25`, run `34171740485`, digest `sha256:52f075f761755436e1dd9746726d928d47ab2abd6595ea3158ccf31f9c23624d`, then proved the same three-member topology and baseline digest across authority/A/B. Contention input reached authority but used client-local frame ticks; `b8c2d3e1b2f15df4ba3532f31f3939a554aaeba0` and `b09fc663b064a025adfe9b906dcd281663ce6d89` switched smoke/release input to the production authoritative `ServerTick`.

Subsequent exact compile gates exposed only narrow production-boundary drift. `607abf9fbe855e3f32860fc9a2c7987d9039842e` restored the missing `Game.Sessions.Api` import. Request `c11b769f1ae25c29ad43305a97d4dbfcbf315991` / run `34178744571` preserved one proven runner-shutdown retry, then automatically selected both multiplayer smoke and structural release targets, satisfying T25-051. Its product attempt exposed the obsolete `_session.State` guard; `2b0a6899869bb09f8a971ca8a65ad9f6c260b3f9` uses the existing `IGameSessionControl.Snapshot.Lifecycle` contract (`Uninitialized`/`Stopped`).

Exact source `29a1f35c2a152dbc89c27742beb0fbbc10f260b2`, request `fad4a1762b240966b77e9787f7f45e67addf2161`, run `34185364415`, artifact digest `sha256:d0277120de43196f14607e52482353a4f197d6bda84fdf0afdc86dad1fbf5879`, reached Unity compilation and exposed the next direct dependency defect: `KentridgeMultiplayerPersistence.cs` imports `Game.Composition.Campaign.Runtime`, but `Game.Composition.Kentridge.Playable` referenced only `Game.Composition.Campaign`. `b46525ad096f6477c7c37e77823e64c88ceefc34` adds only the required `Game.Composition.Campaign.Runtime` asmdef reference.

Exact source `567d3d506896cc9490b124cea9516a96fbab27b6`, request `e31215ae533713bae6f2f4cfbb037bc88ad903d0`, run `34188578469`, artifact digest `sha256:bfc61be47c1796e45ccbb3b62cd479aacb4125ef6f146174bd4683faa441e484`, passed repository planning/tooling and then failed Unity compilation before tests/player execution. `KentridgeMultiplayerCapacityJipRehostValidation.cs` used production `C_PlayerInput` in its release mutation and `NoInputSink` interface implementation but omitted `VoxelEngine.Net.Runtime.Protocol`; the smoke validator already imports that namespace. `11225482e55f987a0176f1256bec86b612160a9f` adds only that missing import.

T25-030–034 and structural release T25-040–043 are implemented but remain unaccepted until green exact built-player evidence.

## Current discriminator

Hypothesis A: the release protocol import now lets persistent tests compile and the authoritative-tick fixes carry smoke through contention/progression, interruption/reconnect, absent-period combat recovery, explicit leave, then release through capacity/JIP/repeated reconnect/rehost. Hypothesis B: the next exact execution exposes the first remaining production ordering/state defect after compilation. **Next experiment:** exact-SHA targeted CI from the current feature head; check runtime boxes only from matching built-player artifacts.

## Remaining gates

1. Green exact-head smoke proof; fix only demonstrated failures.
2. Green structural release proof for T25-040–043; T25-051 automatic selection is already exact-run proven.
3. Mark remaining T25-010–052 runtime evidence truthfully, close open -> closed with metadata, merge current master, revalidate if required, then PR + auto-merge + required `affected` gate; finish only after merged closure is visible on master.
