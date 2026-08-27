# CI operations

- `bad78b74d2f0c1966ca3a858a3fb8c2c1efe5336`: final request was malformed because it combined an EditMode test with `scene_issue`; the workflow rejected it during request resolution before Unity ran. This is not product evidence.
- Corrected focused request `cce2f2bf63252d62749fcf555ca11e352e1d03c6` was created directly from exact feature source `4f600c33edd9533ce9fc3c407497ebc114dbc673`; run `33080889659` passed.
- Final saved-pose request `e64272f9b8e8add992a1a06c4275bbe00fd43d62` was also created directly from exact source `4f600c33edd9533ce9fc3c407497ebc114dbc673`; run `33081103282` passed including real-player capture.
