# CI operations

- `33230924543` / request `4424c2eaa328e573eea12a971a2c493b970a0f93`: product-red compile failure. `KentridgeMacroWorldEvidenceDriver.cs` missed `using Game.WorldBuilder.Api`; fixed at `339ca94f593653e84a02fe2d19712971bfd99e20`. Artifact `9708464190` is diagnostic only.
- `33231300309` / repaired request on the same `ci-test/fixes/agent-6` transport: product-red compile failure. `KentridgeMacroWorldPhysicalProductionAcceptanceTests.cs` had the same missing namespace import; fixed at `e40fb7220af56e096020e105959202eac2b2d70d`.
- `33232755172` / request `849f93f0b838b77b07fa1d24529f9fd69fa44dd2` for source `c447467b897b430cdc335582a33f0fc6b1dca526`: compilation succeeded, then the focused regression found a real planning defect: verified route `fighting-area-1->bandit-hideout` intersects the modern `rossdam-lake` footprint without an authored semantic solution. The built `KentridgePlayableSlice` hit the same planner exception during `OnEnable`; `visible=0`, no `macro-*` frames, so the artifact is diagnostic only.
- Product repair after run `33232755172`: preserve the substantial lake and verified topology, explicitly author the Bandit Hideout spur as a modern dry-shore `GoAround` solution (`08abd146cbc05022895327158f63e50db56e7b51`) and add a production regression that its entire road corridor remains outside the lake (`e2c84ce57a9455a16ad1cb10e63c077f2d5fc945`).

No queued/running request was replaced. The feature remains open until a corrected exact-SHA focused test and built-player scene validation are green.
