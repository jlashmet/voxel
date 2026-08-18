from pathlib import Path

path = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")
text = path.read_text()
marker = "step4Lifecycle={Step4FalseEmptyDiagnostics.Current}"
if marker in text:
    print("step4 lifecycle failure output already wired")
    raise SystemExit(0)
old = '''                      + $"profile:{metrics.ProfileBlockInvalidations} "\n                      + $"stale:{metrics.RejectedStaleSolidBuilds}.\");'''
new = '''                      + $"profile:{metrics.ProfileBlockInvalidations} "\n                      + $"stale:{metrics.RejectedStaleSolidBuilds} "\n                      + $"step4Lifecycle={Step4FalseEmptyDiagnostics.Current}.\");'''
if text.count(old) != 1:
    raise SystemExit(f"expected one LodRendering failure output anchor, found {text.count(old)}")
path.write_text(text.replace(old, new, 1))
print("wired Step4FalseEmptyDiagnostics.Current into LodRendering failure output")
