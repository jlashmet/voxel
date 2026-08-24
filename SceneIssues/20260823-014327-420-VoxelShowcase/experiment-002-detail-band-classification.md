# Experiment 002 — detail-band classification

**Hypothesis** — The clean current pose-1 result is only caused by the marked structure landing in
a finer LOD ring than it did in the original capture.

**What was performed** — Replayed exact pose 1 again with a temporary scene-only detail-band scale
change from 0.6 to 0.8. This expands the step handoffs from 57.6/115.2/172.8 metres to
76.8/153.6/230.4 metres without changing authoritative world state. The production player ran at
1293×718 for 50 seconds through `tools/unity-run.sh`; the final frame is
`verification-detail-scale-08.png` and facts are in `verification-detail-scale-08.txt`.

**Result** — The hypothesis was disproven. The marked structural silhouette is materially unchanged
when finer rings extend much farther. The run converged at 832 visible surfaces with zero missing.

**What was learned** — The clean marked region is not an accidental ring-selection workaround.
Changing global detail distance is neither necessary nor an appropriate fix.

**Next** — Query the authoritative hit and visible source-step coverage at the exact marked ray.
