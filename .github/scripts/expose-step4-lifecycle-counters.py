from pathlib import Path

TEST = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")

test = TEST.read_text()
marker = 'lifecycle:{Step4FalseEmptyDiagnostics.Current}'
if marker in test:
    print("step4 lifecycle counter output already wired")
    raise SystemExit(0)

old = '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"'''
new = '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"lifecycle:{Step4FalseEmptyDiagnostics.Current} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"'''
count = test.count(old)
if count != 1:
    raise SystemExit(f"LOD lifecycle message: expected one match, found {count}")

TEST.write_text(test.replace(old, new, 1))
print("wired Step4FalseEmptyDiagnostics.Current into LodRendering failure output")
