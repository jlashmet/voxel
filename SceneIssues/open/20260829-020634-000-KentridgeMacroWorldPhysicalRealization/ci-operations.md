# CI operations

- `33230924543` / request `4424c2eaa328e573eea12a971a2c493b970a0f93`: product-red compile failure. Evidence driver missing `Game.WorldBuilder.Api`; fixed at `339ca94f593653e84a02fe2d19712971bfd99e20`.
- `33231300309`: product-red compile failure from acceptance test's same missing import; fixed at `e40fb7220af56e096020e105959202eac2b2d70d`.
- `33232755172`: Bandit hard route intersected Rossdam lake without semantic resolution; authored dry-shore `GoAround` + corridor regression.
- `33255557296`: Orc hard route grazed Southern Ridge; authored ridge-shoulder `GoAround` + travel-margin regression.
- Master `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` integrated by merge `379439a571b3e941ee9fc818c402fc49331ebf28`.
- `33258816868`: stale agent-6 `StorySpecs.cs` removed current opening-story APIs; restored master bytes at `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b`.
- `33259572439` / request `f726b2f4bbf8e3e116bef1792573b043c307bcd1`: focused + built player green; metrics `regions=6 settlements=6 buildings=16 hardRoutes=20 routeTiles=833 constrainedRoutes=5 solveSteps=1108 maxRoadRiseVoxels=2 waterDepthVoxels=46`; artifact `9716890862` visually incomplete.
- `33260139560` / request `5ac9917a1b8fff3221c3ae528b128f15c7168810`: green but remote captures at `coverage=False`; artifact `9717050641` rejected.
- `33260866388` / request `8e1e496099b64167e6210d562112467ff4da12dc`: green but validation prewarm caused remote feature presentation loss and timeout; artifact `9717246958` rejected.
- `33261299347` / request `0a7a8ca5fe1cff7142cab71c7bf89772809e1123`, source `cd194a16d32adb442f9d2f699b1ffc1c00f661ee`: exact focused + built player green; real motor traversal and clean process, but settlement evidence camera occlusion remained. See experiment 007.
- `33279138597`: nested physical acceptance green then all-building exclusion regression exposed Orc Village building 3 / Southern Ridge conflict; product failure fixed by bounding modern ridge extent.
- `33283034449` / request `c1a21b76cdc548436a32bd0866f26a2448a67286`, source `0bbc9150f36281c0f951d9c75a60b318842fba46`: green persisted physical/storage acceptance; artifact still lacked readable Fairy/Orc shells, proving storage alone was insufficient.
- `33288421041` / request `4a00dc022631e62628f59a944c5410767dc9904d`, source `f13bd8cf0e9e2bfcc4dfda3077eda391e61aefa4`: green two-stage readiness regression, but broad residency readiness delayed opening ~40 s and produced no required macro captures.
- `33289185080`, source `81022f85d1aa2b29d231175e204d9682e6edbbdf`: green scoped readiness; Moordell shells became visible but framing/readiness still prevented complete target coverage in 60 s.
- `33290154012` / request `e5a015b6e9c11b9d1cb91c32ef3a3f45363142ed`, source `8cab72cc862f3c0ae381cb4f951613af20d047c3`: workflow-green focused + built player. Artifact `9725740286` rejected for closure: opening/pub occupies ~40 s, macro road CharacterMotor evidence succeeds, then only `moordell index=0` becomes content-ready before the 60.4 s harness ends. Process `elapsed=79s peak=5859MB systemFree=31944MB swapGrowth=0MB`; no harness assertions. See experiment 011.

No queued/running request was replaced. Feature remains open. The next request must be built directly on the refreshed final feature SHA after the validation-only scheduling correction; do not reuse the visually incomplete green run.