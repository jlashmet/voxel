# CI operations

- `33230924543` / request `4424c2eaa328e573eea12a971a2c493b970a0f93`: product-red compile failure. Evidence driver missing `Game.WorldBuilder.Api`; fixed at `339ca94f593653e84a02fe2d19712971bfd99e20`.
- `33231300309`: product-red compile failure from acceptance test's same missing import; fixed at `e40fb7220af56e096020e105959202eac2b2d70d`.
- `33232755172`: Bandit hard route intersected Rossdam lake without semantic resolution; authored dry-shore `GoAround` + corridor regression.
- `33255557296`: Orc hard route grazed southern ridge; authored ridge-shoulder `GoAround` + travel-margin regression.
- Master `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` was integrated by merge `379439a571b3e941ee9fc818c402fc49331ebf28`.
- `33258816868`: stale agent-6 `StorySpecs.cs` removed current opening-story APIs; restored master bytes at `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b`.
- `33259572439` / request `f726b2f4bbf8e3e116bef1792573b043c307bcd1`: focused + built player green; metrics `regions=6 settlements=6 buildings=16 hardRoutes=20 routeTiles=833 constrainedRoutes=5 solveSteps=1108 maxRoadRiseVoxels=2 waterDepthVoxels=46`; artifact `9716890862` had valid Moordell but insufficient time.
- `33260139560` / request `5ac9917a1b8fff3221c3ae528b128f15c7168810`: green but remote captures at `coverage=False`; artifact `9717050641` rejected. See experiment 005.
- `33260866388` / request `8e1e496099b64167e6210d562112467ff4da12dc`: green but validation prewarm caused remote feature presentation loss and timeout; artifact `9717246958` rejected. See experiment 006.
- `33261299347` / request `0a7a8ca5fe1cff7142cab71c7bf89772809e1123`, source `cd194a16d32adb442f9d2f699b1ffc1c00f661ee`: exact focused + built player green. Test metrics unchanged and good; player restores `Time.timeScale=1`, traverses 6.84m locally and 8.24m macro road, finishes 60s with zero assertions; process status `elapsed=71s rss=519MB peak=5289MB systemFree=34268MB swapGrowth=0MB`. Artifact `9717362552`: prewarm removal restores Moordell building and lake target exists, but Rossdam/Fairy/Orc ground-level center-facing poses are terrain-occluded and ridge/overview miss timeout. See `experiment-007-evidence-camera-occlusion.md`.
- Repair after `33261299347`: evidence-only source `3767205da4df9e94114871722aa0de05834a788c` derives elevated generic-settlement surveys from actual four-plot geometry/terrain, raises lake/ridge/network surveys, changes validation-only opening timeline 4x->12x, trims validation traversal/dwell overhead, retains published-near-coverage gating, and restores normal time before CharacterMotor evidence. Production planner/catalogues/streamer/story remain unchanged. Ledger commits follow.

No queued/running request was replaced. Feature remains open until a fresh exact-SHA gate is green and every required target plus cost/runtime evidence is accepted.
