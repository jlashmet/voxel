# Experiment 010 — water discovery deadline

## Hypothesis
The remaining moving-frame tail comes from water discovery synchronously classifying 32 solid-discovery bricks after the GPU solid path has met its own budget.

## Action / source
Exact feature `4754860892cf3ddd81a7d2fc8130d02fc58355f3`; targeted request `0890971c69bbadebe6e43c18269b2126eef3f6cd`, run/job `33281099872` / `99176301088`, artifact `9723212603`.

## Result
- Strengthened shared-mirror liveness passed in 52.21 s.
- Migration preserved visible geometry, zero eligible fallback, and snapshotless GPU adoption, but failed moving p99 at 75.912 ms (limit 25 ms).
- The exact player converged from an incomplete 15.7 s image to `missingVisible=0`; settled telemetry reached roughly 200–500 FPS with solid admission ~0.5–3 ms and no arena failure.
- A startup water slice reached 29.256 ms while solid admission in that frame was 0.478 ms. All four exact-player images were inspected; geometry was nearly absent at 15.7 s and substantially complete by 25.7 s.

## Verdict / next step
Shared-mirror liveness and coverage are no longer the tail owner. Make discovery-only water classification deadline-aware with a four-brick progress floor; keep mutation invalidation immediate and rerun unchanged thresholds. If p99 remains red, isolate GPU upload-buffer reuse next.
