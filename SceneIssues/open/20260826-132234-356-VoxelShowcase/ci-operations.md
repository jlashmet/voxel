# CI operations

- Request `c11d015e…`: focused PlayMode test passed and the 45-second built-player replay reached `missingVisible=0`, but direct screenshot inspection still showed the upper rectangular grass tongue. Rejected as visual failure.
- Request `423ee5d9…`, workflow `33176475634`, attempt 1: infrastructure failure because an interactive Unity editor was open; requested regression was skipped. Diagnostic player replay completed. Used the one allowed infrastructure retry on the same request.
- Same request, attempt 2: requested regression executed and failed because it assumed an inactive civic-west court placement. The built-player replay again reached full residency; marked-ground pixels were unchanged from the prior replay, falsifying the civic terrace/court hypothesis.
- Request `c63244e4…`, workflow `33205371873`: failed before Unity because scene-issue requests require `platform=PlayMode`.
- Request `084f9401…`, workflow `33205557129`: request resolution succeeded, but the moved PlayMode regression failed compilation; repaired without changing production geometry.
- Request `d0dbaeeb…`, workflow `33206033751`, source `636f6120…`: bake/test/player succeeded but the workflow hit its hard timeout, so it was not a valid gate.
- Request `agent8-132234-final-57006268`, workflow `33214166946`, source `57006268…`: route-overlap regression failed; zero live route placements intersect the corrected upper envelope. Lower mark improved; upper tongue remained.
- Workflow `33215984995`, source `a09f2897…`: exact plot regression and built player were green, but the upper rectangle remained, rejecting the archetype-pad candidate visually.
- Request `agent8-132234-precedence-final-36eb66c5`, workflow `33225240544`, source `36eb66c5…`: forced bake/player capture succeeded; focused test failed because no route intersects the marked WideHouse-pad region. Screenshot remained visually unchanged. Product failure; precedence hypothesis rejected. Next correction derives organic generated-house grading from the exact resolved foundation rather than an archetype-wide pad.
