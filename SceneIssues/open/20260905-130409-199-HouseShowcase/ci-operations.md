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
- Screenshot previews/artifact upload/final commit status: success.
- Visual review: defect reproduced; cyan/teal and magenta encoded-normal output proved diagnostic `_DebugCoverage` was active instead of production material shading.

No queued/running request was replaced. A new exact-SHA request is permitted only after the demonstrated production fix and regression are committed.
