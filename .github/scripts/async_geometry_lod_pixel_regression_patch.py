from pathlib import Path

p = Path('Assets/Tests/PlayMode/LodRenderingTests.cs')
s = p.read_text()

old = '''                using var target = new RenderTexture(64, 36, 24);
                camera.targetTexture = target;

                var bands = new[]
                {
                    (Step: 1, Distance: 48f),
                    (Step: 2, Distance: 144f),
                    (Step: 4, Distance: 240f),
                    (Step: 8, Distance: 340f),
                };

                foreach (var band in bands)
                {
                    camera.transform.position = castleWorldCentre
                                              - Vector3.forward * band.Distance
                                              + Vector3.up * 28f;
                    camera.transform.LookAt(castleWorldCentre + Vector3.up * 8f);
                    camera.fieldOfView = 42f;

                    // Give the async ring worker time to discover, build, publish and upload the
                    // band it owns. This is deliberately not a synchronous build test.
                    for (int frame = 0; frame < 24; frame++) yield return null;
                    camera.Render();

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"LOD step {band.Step} rendered no voxel chunks at {band.Distance}m.");
                    Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                        $"LOD step {band.Step} left visible solid chunks without geometry.");
                    Assert.Greater(metrics.UploadedGeometryBytes, 0ul,
                        $"LOD step {band.Step} never uploaded generated voxel geometry.");
                }
'''
new = '''                using var target = new RenderTexture(160, 90, 24, RenderTextureFormat.ARGB32);
                var readback = new Texture2D(target.width, target.height,
                                             TextureFormat.RGB24, false, true);
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 70f;

                var bands = new[]
                {
                    (Step: 1, Distance: 48f),
                    (Step: 2, Distance: 144f),
                    (Step: 4, Distance: 240f),
                    (Step: 8, Distance: 340f),
                };

                CastleStructureSignature reference = default;
                try
                {
                    foreach (var band in bands)
                    {
                        // Orthographic framing intentionally keeps the castle at the same screen
                        // scale while camera distance alone selects the LOD ring. That makes loss
                        // of architectural openings/edges measurable instead of conflating it with
                        // perspective shrinkage.
                        camera.transform.position = castleWorldCentre
                                                  - Vector3.forward * band.Distance
                                                  + Vector3.up * 28f;
                        camera.transform.LookAt(castleWorldCentre + Vector3.up * 8f);

                        // Give the async ring worker time to discover, build, publish and upload the
                        // band it owns. This is deliberately not a synchronous build test.
                        for (int frame = 0; frame < 48; frame++) yield return null;
                        camera.Render();

                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Assert.Greater(metrics.VisibleSolidChunks, 0,
                            $"LOD step {band.Step} rendered no voxel chunks at {band.Distance}m.");
                        Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                            $"LOD step {band.Step} left visible solid chunks without geometry.");
                        Assert.Greater(metrics.UploadedGeometryBytes, 0ul,
                            $"LOD step {band.Step} never uploaded generated voxel geometry.");

                        CastleStructureSignature signature = CaptureCastleStructure(
                            target, readback);
                        Assert.Greater(signature.EdgeCount, 40,
                            $"LOD step {band.Step} produced too little castle structure to inspect.");

                        if (band.Step == 1)
                        {
                            reference = signature;
                            Assert.Greater(reference.EdgeCount, 120,
                                "Step-1 castle reference lacks enough internal edges for a useful LOD regression.");
                            continue;
                        }

                        float retainedEdges = signature.EdgeCount / (float)reference.EdgeCount;
                        float matchedReference = MatchedEdgeRecall(reference, signature, 2);
                        Assert.GreaterOrEqual(retainedEdges, 0.18f,
                            $"LOD step {band.Step} collapsed too much architectural edge structure "
                          + $"({retainedEdges:P0} of step-1). A filled grey mass must not pass.");
                        Assert.GreaterOrEqual(matchedReference, 0.18f,
                            $"LOD step {band.Step} no longer preserves the castle's step-1 edge layout "
                          + $"({matchedReference:P0} matched). Openings/silhouette were likely collapsed.");
                    }
                }
                finally
                {
                    Object.Destroy(readback);
                }
'''
if s.count(old) != 1:
    raise SystemExit(f'LOD band render block expected once, found {s.count(old)}')
s = s.replace(old, new, 1)

# Insert helpers before GetPrivateField.
marker = '''        private static T GetPrivateField<T>(object target, string fieldName) where T : class
'''
if marker not in s:
    raise SystemExit('GetPrivateField insertion marker missing')
helpers = r'''        private readonly struct CastleStructureSignature
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool[] Edges;
            public readonly int EdgeCount;

            public CastleStructureSignature(int width, int height, bool[] edges, int edgeCount)
            {
                Width = width;
                Height = height;
                Edges = edges;
                EdgeCount = edgeCount;
            }
        }

        private static CastleStructureSignature CaptureCastleStructure(
            RenderTexture target, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = readback.GetPixels32();
            var edges = new bool[target.width * target.height];
            int edgeCount = 0;

            // Central keep/inner-castle crop. Excluding most sky/terrain prevents a stable horizon
            // from making a blob look structurally similar to the reference.
            int minX = target.width / 4;
            int maxX = target.width * 3 / 4;
            int minY = target.height / 5;
            int maxY = target.height * 17 / 20;
            const float threshold = 0.045f;

            for (int y = minY; y < maxY - 1; y++)
            for (int x = minX; x < maxX - 1; x++)
            {
                int i = x + y * target.width;
                float l = Luminance(pixels[i]);
                float dx = Mathf.Abs(l - Luminance(pixels[i + 1]));
                float dy = Mathf.Abs(l - Luminance(pixels[i + target.width]));
                if (Mathf.Max(dx, dy) < threshold) continue;
                edges[i] = true;
                edgeCount++;
            }

            return new CastleStructureSignature(target.width, target.height, edges, edgeCount);
        }

        private static float MatchedEdgeRecall(in CastleStructureSignature reference,
                                               in CastleStructureSignature candidate,
                                               int tolerancePixels)
        {
            Assert.AreEqual(reference.Width, candidate.Width);
            Assert.AreEqual(reference.Height, candidate.Height);
            if (reference.EdgeCount == 0) return 0f;

            int matched = 0;
            for (int y = 0; y < reference.Height; y++)
            for (int x = 0; x < reference.Width; x++)
            {
                int i = x + y * reference.Width;
                if (!reference.Edges[i]) continue;

                bool found = false;
                int minY = Mathf.Max(0, y - tolerancePixels);
                int maxY = Mathf.Min(candidate.Height - 1, y + tolerancePixels);
                int minX = Mathf.Max(0, x - tolerancePixels);
                int maxX = Mathf.Min(candidate.Width - 1, x + tolerancePixels);
                for (int cy = minY; cy <= maxY && !found; cy++)
                for (int cx = minX; cx <= maxX; cx++)
                {
                    if (!candidate.Edges[cx + cy * candidate.Width]) continue;
                    found = true;
                    break;
                }
                if (found) matched++;
            }
            return matched / (float)reference.EdgeCount;
        }

        private static float Luminance(Color32 colour) =>
            (0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b) / 255f;

'''
s = s.replace(marker, helpers + marker, 1)
p.write_text(s)

# Static guards.
text = p.read_text()
assert 'camera.orthographic = true;' in text
assert 'MatchedEdgeRecall(reference, signature, 2)' in text
assert 'A filled grey mass must not pass.' in text
assert 'CaptureCastleStructure' in text
