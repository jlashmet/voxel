# Plan — Kentridge vegetation meadow density

## Scope
Implement only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Never edit `.github/test-request.json` on the feature branch or alter scene serialization for this feature.

## Evidence / selected fix
- Kentridge already routes semantic vegetation through the reusable point-cloud placement and packed procedural-grass renderer.
- Renderer capacity is not the density bottleneck: each semantic grass seed expands deterministically to 5–15 blades and packed meshes split into bounded chunks.
- The feature adds reusable regional ecology policy for vegetation allowlists, density/variation, ambient-animal allowlists, and explicit building/path/cultivated/water/steep/invalid exclusions; Kentridge uses grass-only meadow policy.
- Kentridge diagnostics now measure semantic instances, renderer-equivalent blades, connected primary-meadow blades, mesh chunks, and post-policy excluded-surface leakage.
- Shared grass wind already uses `_GrassTime` updated from `Time.time`; do not add a Kentridge-only shader or second animation system.
- Final CI run `33236269717` consumed its one infrastructure retry. The retry passed the focused PlayMode acceptance and ran the built player for 60 seconds with zero assertions. Runtime reported 11,478 grass instances, 114,580 rendered blades, 5,777 primary-meadow instances, 57,589 primary-meadow blades, 8 grass mesh chunks, and zero excluded-surface grass.
- Its only remaining blocker was visual replay: `issue.json has no replayable camera snapshot`, leaving screenshots in the opening interior/cutscene.
- A known-good Kentridge capture proves `poseAnchor` may be null while the player camera uses `FirstPerson-AIO/FirstPersonCharacter/Capsule/PlayerCamera`. This ticket had the same player-camera capture intent but an empty `camera.hierarchyPath`.
- Selected replay fix: restore that hierarchy path in this assignment’s capture metadata only. Production runtime cost: zero; blast radius: this SceneIssue replay.

## Validation
1. Keep `tasks.md` current and assignment-only.
2. Refresh/merge current `origin/master` before final CI; stop on any conflict outside assigned work.
3. Use only `ci-test/fixes/agent-5` for the exact-SHA targeted request, with the request commit built directly on the final feature SHA.
4. Require focused regression + real-player harness green on that exact SHA.
5. Inspect real-player screenshots: they must show a dense player-height meadow, not the opening interior/cutscene, and time-separated stationary frames must visibly demonstrate wind motion.
6. Preserve runtime proof of >=3,000 connected meadow blades, zero excluded-surface grass, no startup/runtime exceptions, and acceptable cost.
7. Store concise durable verification evidence beside this feature; only then move `open -> pending`, complete metadata, and satisfy every task/acceptance checkbox.
8. After all green gates, move `pending -> closed`, set `status=fixed`/`resolvedUtc`, merge latest master if it advanced, and non-force publish the exact feature head to `origin/master`.
