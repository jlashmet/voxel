# Experiment 001 — Medrare dialogue source

**Hypothesis:** `showLines:data:5000` names a dialogue payload that is unavailable in the pinned source, so exact Medrare text cannot be recovered.

**Action / source SHA:** Inspect `jlashmet/mounting-force@9491acd9efc3ad7413a13fd28f1686ed473b5672`: `Code/RPG Engine/RPGCutScene.m`, `Code/KentridgeMedrareJoin.m`, `MountingForce.xcodeproj/project.pbxproj`, and the mapped resource.

**Result:** `RPGCutScene.showLines` sets `currentStop = index + lines`; `5000` is a line-count stop, not a dialogue id. The Xcode resource map points `kentridge-medrare-join.txt` to `Art/kentridge-medrare-join.txt`, which exists and contains 17 Medrare/Weldon lines.

**Verdict:** Hypothesis falsified. The authoritative payload is recoverable and must be ported verbatim. The separate sighting/first-spell/church payloads remain unavailable and must not be invented.

**Next:** Assert the exact 17 lines/speakers through production cutscene content and require built-player Logan -> Awon -> Medrare evidence before promotion.
