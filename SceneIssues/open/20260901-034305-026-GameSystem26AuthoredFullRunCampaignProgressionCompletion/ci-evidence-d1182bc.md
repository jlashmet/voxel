# Exact targeted-CI evidence — d1182bc

- Feature source: `31e7b47aefad0f71d4d7ab2b842f4f1dec898ebb`
- Exact request: `d1182bc248c54dced2ce5bb86de31056bd5d7b07`
- Workflow run: `34029171252`
- Job: `101475442215`
- Artifact: `9988560566` (`single-test-34029171252`)
- Artifact digest: `sha256:7784f88df4e9374e44241375b668ed50e5f8bc506b3c266122087509359ec3e2`
- Mechanical conclusion: **success**

## Product classification

The prior WB3002 product failure is fixed. Repository-derived module validation executed `Game.Composition.Kentridge.Tests.AuthoredFullRunPhysicalWorldPlanTests.FullRunGenerationResolvesAuthoredSitesAndNpcAssignmentsAgainstPhysicalHierarchy` and it finished `status=Passed`. The same assembly also passed `FullRunPlanConsumesCompiledHierarchyAndRecoveredPhysicalMacroWorld`.

The focused regression therefore proves the `ExistingRichGeneration` projection keeps one source-backed semantic candidate per authored rich-settlement role: the authored starting pub resolves through `/rich-generation/starting-pub` and retains a physical Kentridge macro anchor. Generic physical settlements still fail closed when their required generated blockouts are absent.

The repository-derived affected plan also ran `Game.Composition.Campaign.Tests`, `Game.Composition.Kentridge.Tests`, `Game.Story.Tests`, the other selected dependent assemblies, module-local player validations, and the standard `Assets/Scenes/KentridgePlayableSlice.unity` integration consumer. The run artifact reports all selected test assemblies as passed and all six discovered player validations completed.

## What this run does not prove

The explicit SceneIssue replay step was skipped, and the attached top-level Kentridge integration consumer remains the generic `Assets/Scenes/Validation/kentridge.player-scenario.json` layout/autowalk/survey scenario. Its log contains `KENTRIDGE_WORLD_LAYOUT`, but no Rorik/Moordell/Rossdam/Logan campaign milestones, no System15 terminal result, and no Systems14/23 aftermath assertion. `KentridgePlayableSlice` at this exact feature source still boots `KnownOpeningCampaignContent.Build(...)`.

Accordingly this exact success closes only the rich-generation physical site-projection defect. It is **not** T26-058 production full-campaign player proof and does not complete T26-021/022/044/045/046/053/054. T26-043 remains dependent on real System25 production multiplayer evidence.
