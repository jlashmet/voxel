# CI operations — HouseShowcase

## Baseline exact-SHA discriminator
- Feature SHA: `4ceb27cd97c4688273231163c37d6dea7c914b2f`
- Transport SHA: `e0575bd9619dd6c4ac7f0bd1b0dc426696513953`
- Run: `33994794734`
- Job: `101383338670`
- Artifact: `9977873261`
- Result: success.
- Automatic Showcase module validation: success.
- Standalone HouseShowcase SceneIssue replay: success.
- Visual review: defect reproduced; cyan/teal and magenta encoded-normal output proved diagnostic `_DebugCoverage` was active instead of production material shading.

## Fixed exact-SHA acceptance
- Feature SHA: `ac8262bc48d4a0069856fb2afc41e06bf679b076`
- Transport SHA: `e14333a4005a4d959bd91b88ab5d0253c2b87ac2`
- Run: `33995946540`
- Job: `101386407391`
- Artifact: `9978224269`
- Result: success.
- Automatically derived Showcase EditMode and PlayMode validation: success.
- Requested `Game.Structures.Tests.GuildHouseFurnishingResolverTests`: success.
- Module-local standalone player validation: success.
- Kentridge game-integration player validation: success.
- Built `Assets/Scenes/HouseShowcase.unity` SceneIssue replay: success.
- Screenshot previews/artifact upload/final commit status: success.
- Visual review: the baseline and fixed runs have the same sparse captured geometry at corresponding frames, so geometry is not a regression from this issue. The fixed capture replaces cyan/magenta normal-debug output with production wood/stone/cloth shading, satisfying the reported colors/textures defect without widening scope.

No queued or running request was replaced.