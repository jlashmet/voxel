# Experiment 040 — render-isolation runtime object discovery

## Result from exact diagnostic run
Exact request `019f5562d8b9d2575de0024d71ccbdb55dca028f`, run `34006671692`, source `affc45d54e08362ed6c7515a537bfb386eca4590` completed with repository-derived module validation green and the standalone SceneIssue replay failed as intended. The run did **not** attribute the magenta draw owner: after `WAYPOINT_REPLAY capture '01-mountain-approach.png'`, the diagnostic logged `RENDER_ISOLATION missing production approach/replay; no attribution is possible.` No isolation frames or material inventory were emitted.

The ordinary capture remained visibly unacceptable, with the same large error-magenta masses. This run is therefore rejected visual evidence and cannot justify a renderer fix.

## Demonstrated diagnostic defect
The replay harness is a live `DontDestroyOnLoad` / `DontSave` component. The diagnostic used the default `FindFirstObjectByType`, which did not rediscover that evidence object in this standalone-player lifecycle even though the harness was actively traversing immediately before the lookup.

## Correction / next discriminator
Use `Resources.FindObjectsOfTypeAll<T>()` and accept only components whose `gameObject.scene` is valid and loaded. Log the three prerequisites (`approach`, `replay`, `showcase`) independently if discovery still fails. Rendering, world state, collision, budgets, route, and acceptance remain unchanged.

Rerun the identical-camera isolation experiment. Only its full-rendering frames describe production output; suppression frames are diagnostic only. Do not make another shader/geometry correction until the run identifies which draw class removes the magenta pixels.
