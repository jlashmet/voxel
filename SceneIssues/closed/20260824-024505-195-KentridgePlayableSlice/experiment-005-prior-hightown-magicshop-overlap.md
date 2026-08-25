# Experiment 005 — prior Hightown overlap with the magic shop

## Hypothesis

The pre-fix Hightown pass placed Kentridge-only terrace and urban features through the magic-shop
envelope, explaining why the original shell was fragmented/floating and why the preceding
catalogue-ownership fix changed it into one continuous wall.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, reconstructed all fifteen formerly
mis-invoked Hightown stages, evaluated every primitive, and reported any primitive intersecting the
magic-shop envelope `(988,247,562)..(1112,367,686)`. Ran the temporary EditMode diagnostic
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests.DiagnosticPriorHightownStagesOverlappingMagicShop`
through `tools/unity-run.sh`.

## Result

The test passed 1/1 and found extensive overlap: the lower-middle and market-main district terraces
carved/filled/painted the envelope; Kentridge anonymous fabric 9/10 intersected its south edge at
precedence 86; and a hillside terrace dwelling intersected it at precedence 90. Exact definitions,
placements, modes, and bounds are in `verification-prior-hightown-overlap-results.xml`; Unity output
is in `verification-prior-hightown-overlap-unity.log`.

## What was learned

The hypothesis is confirmed. The preceding Hightown ownership fix causally removed multiple
unrelated authored volumes crossing the magic shop, which accounts for the original fragmented
floating silhouette. The surviving featureless west wall is the legitimate magic-shop shell viewed
from its interior; whether it reads correctly is now an independent facade-authoring question.

## Next

Verify the magic-shop form's intended side-window policy and whether the shared-house adapter
actually compiles that policy into the west/east facades before deciding whether further production
authoring is required for this issue.
