# Experiment 042 — Showcase residency seed gate and split player signal

## Exact run
Transport `80bef5c7b31c4cd2a24c62b3a0be82eecbb89f35` validated source `f58f49aaee7c5857e9fb31ff5693c30cb94759b9` in run `33875344287`.

## Module-validation failure
The persistent editor reached the newly owned `Game.Composition.Showcase.Tests` assembly and failed only `ShowcaseFeatureResidencyTests.OrdinaryStreamingPublishesAuthoredUpperLayerWithoutHorizontalRadiusWidening`. The failure happened before the residency discriminator itself: the arbitrary fixture seed `0x46524553` caused production `KentridgeTownPlanner` to exhaust its bounded candidate set for `MagicShop` while `ShowcaseCatalogue.Build` authored Kentridge.

Classification: validation-fixture defect. The fixture is supposed to exercise production Showcase vertical residency, not test unsupported arbitrary town seeds. Both the EditMode discriminator and paired player validation now use the production Kentridge playable seed `0x4B454E54`, which is already exercised by the shipped Kentridge slice.

## Standalone SceneIssue signal
The same exact source built and replayed the real Kentridge player for 180 seconds with `HARNESS done ... assertion failures 0`. CharacterMotor traversal executed and the runtime macro catalogue contained the expected Moordell, Rossdam, Fairy, Orc, road, ridge, and water placements.

However strict evidence still progressed only through `MACROEVIDENCE content-ready target=moordell`. It did not advance to Rossdam/Fairy/Orc/ridge/network. Late renderer diagnostics remained unhealthy: `drawn=0`, `coverage=False`, eight in-flight GPU requests, a growing/large mirror admission backlog, and no useful GPU count/write/copy completions. Therefore the successful process replay is not acceptance proof and the macro liveness gate remains open.

## Next discriminator
Re-run exact-SHA CI after the production-seed fixture correction. The requested `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantRelocationExecutesProductionGpuLivenessRegression` must execute before any additional renderer fix is selected. If it reproduces sustained saturated admission or stalled GPU completion, use its slot/residency diagnostics to make the smallest production liveness correction.
