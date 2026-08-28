# CI operations

- Request `c11d015e…`: focused PlayMode test passed and the 45-second built-player replay reached `missingVisible=0`, but direct screenshot inspection still showed the upper rectangular grass tongue. Rejected as visual failure.
- Request `423ee5d9…`, workflow `33176475634`, attempt 1: infrastructure failure because an interactive Unity editor was open; requested regression was skipped. Diagnostic player replay completed. Used the one allowed infrastructure retry on the same request.
- Same request, attempt 2: requested regression executed and failed because it assumed an inactive civic-west court placement. The built-player replay again reached full residency; marked-ground pixels were unchanged from the prior replay, falsifying the civic terrace/court hypothesis. Request is complete and diagnostic only.
- No queued/running request is being replaced. The next request, if issued, will target the new product source after the proven plot-pad fix.