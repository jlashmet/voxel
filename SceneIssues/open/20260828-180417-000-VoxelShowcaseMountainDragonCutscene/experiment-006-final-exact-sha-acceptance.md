# Experiment 006 — final exact-SHA acceptance and checked-in payload gate

## Exact source / CI provenance
- Feature source SHA: `2100df40287a9ec9d66407e54ab92b9cc2e58b25`.
- CI transport commit: `0b1cac9068bbed415d4ad5054b82e268f51707ac`, whose sole parent is the exact feature source SHA.
- Final targeted run: `33310677691`, job `99255036891`.
- Requested test: `VoxelEngine.Tests.PlayMode.MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay`.
- Result: green. The fresh source-matched bake, the post-bake Unity-reopen wait, the exact PlayMode test, built-player capture, artifact upload, and final commit status all completed successfully.

## Bake cost / shutdown result
- The fresh bake completed under the unchanged `UNITY_MAX_MINUTES=4` / `UNITY_MAX_RSS_MB=14336` watchdog contract; neither the 240-second time guard nor 14 GiB RSS ceiling fired.
- This is the first exact run proving the POSIX native `_exit(0)` successful-bake path makes the native Unity PID disappear soon enough for the next guarded Unity invocation to reopen normally.
- `showcase-bake.log` records: `200 regions, 18.2 MiB, seed 0x5EED1234, castle voxels 41,129,996, content signature 0x7C8A5152`.
- No runtime streaming, gameplay movement/collision, mountain geometry, structure semantics, or workflow budget was weakened to obtain this result. The native exit remains behind the existing exact GitHub-Actions batch bake-success predicate.

## Accepted generated startup image
- Artifact payload size: `19,122,896` bytes.
- SHA-256: `cf51ac9fd9da8a753bf625cc14430ee4941e1ea2f7404be43fd3a2f8323c5da4`.
- Sidecar manifest:
  - `version=1`
  - `contentSignature=7C8A5152`
  - `payloadSha256=cf51ac9fd9da8a753bf625cc14430ee4941e1ea2f7404be43fd3a2f8323c5da4`
- Startup-bake acceptance evidence records mountain origin `(-1712,219,-400)`, radius `500`, height `280`, path width `30`, six switchbacks, and placeholder size `60`.

## Focused acceptance / built-player evidence
- NUnit result: `1/1` passed; exact case duration `17.047203 s` (`17.0781654 s` test-run duration).
- Production replay used the normal `AutoWalk -> CharacterMotor.Step` path and reached all `17/17` waypoints in `57.5 s`; summit-proximity waypoint `17/17` reports `grounded=True`.
- Final exact-run captures:
  - `01-mountain-approach.png`
  - `02-path-base.png`
  - `03-switchback-mid.png`
  - `04-switchback-upper.png`
  - `05-summit-dragon-supported.png`
  - `06-dragon-dialogue.png`
- Human review of those exact frames confirms a substantial grounded landmark, readable accessible base/path, supported continuous switchbacks, the large red summit placeholder visibly supported by authored summit geometry, and the presented dialogue `Hello, I'm Mr. Dragon.`

## Remaining closure discriminator
The generated/validated artifact is accepted, but the feature branch still checks in an older `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` (`11,074,525` bytes, Git blob `53b8673358e6166445162d64be4f4af89c132a50`) and does not check in `ShowcaseWorld.manifest.txt`. `ShowcaseStartupBakeContract.Validate` requires the sidecar and exact payload SHA; therefore a clean checkout of the feature branch does not yet ship the accepted source-matched startup image.

The reopened issue explicitly requires the checked-in startup bake used by the scene to contain the accepted mountain. Do not move this assignment to pending/closed until the accepted `19,122,896`-byte payload and its manifest are committed through a repository-sanctioned binary refresh path, without creating a second CI transport or weakening provenance validation. The currently connected GitHub text/Git-data write surface cannot stream the retrieved 19 MiB binary artifact into a blob, so this is a repository-state gate rather than another generation/test defect.
