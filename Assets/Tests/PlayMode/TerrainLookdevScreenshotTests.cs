using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter TerrainLookdevScreenshotTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class TerrainLookdevScreenshotTests
    {
        private const int CaptureWidth = 512;
        private const int CaptureHeight = 768;
        private const int ReferenceWidth = 32;
        private const int ReferenceHeight = 48;
        private const float MinimumPatchSsim = 0.25f;

        // Exact 32x48 RGB24 block-average downsample of the original 1024x1536
        // Mounting Force terrain reference supplied for this lookdev. The byte-count assertion
        // below deliberately makes malformed or accidentally concatenated references fail loudly.
        private const string ReferenceRgbBase64 =
            "ipBJpJ5NrqlMt65TyLpuxbdjtK9sm55sjphii5VgjpZcoaZfsK1js6tsyLtyzMJzt7V3qq5/urmDvbt6t7drurturrJvo6lrg5JnkJRqrqpjvrRhv7Znw7hpvbJpx716Y3BIlJBWqqVerKVSsKhUw7VgxbhfwbRfv7R0s7BwmqNljZllmqBmsK5lwLljwLZkw7lju7Btv7d2trV0r69qrrJps7dpoqlkoalkkpxih5JjoaNhq6pfsatjvrRpvLJaiJFOi5BMjY1VmZZfp6FhnZ1YralSxLddwLNhxLdwu7Flta9rnaFqeoNmlpR3rqdqsaxhwLhnzL5zy716yLpuvLFuxr1zw7xxr7F0nqdxkJpshJBniY5mn59kkZJesKZpp6BPs6htuK9jmp1Qj5RUkpdZkJVSnZ1WubFfu7Vip6lgpaRcvLNpsaxqmp9loqNti49qmppqlZZqm5hjvLB1w7hnrKlaoqBetqxoxbputa9trqlqp6Vso6BgoJxjpqJjsqxNua1OtalRtKpKv7dWuLJZq6pdqq1eoadVp6xUuLdas7JfqKhfo6Rgq6topaVopahmpaZkoqJrnp5on55lr6hprKxkqa1msLRkublpsLBqmJlnn5tmuaxkxLZgv7JVgohDoJxGo5tRt6peu7JUurVTta9KvbRVwLhiq65mk51jmJxnq6hosa1gp6lkqKxjrbBksLBdvbhhtbBir6xno6J1ipJjnKFogI9mdoVfcn9gioxjrqhjvbNavLBbx7hi" +
            "m5ZalY1ejIhdn5dms6Zxu7FctLJXmJ1FrK5Mw7xYubJhpqVuk5dpoJ5mu7FutbNft7VdtbJhoZ9kqKJsop9wgoxsi5R3hZJsc4hmiJdkjZdkgoVnpqNir6pdta1WvrVcqaVJt6lbpp5foZprj4tckotjt6p0trFcrK9VnqVOu7hctLFdq6lok5hmhIllnZtomZtdqKdau7ViubRltbJmqqxmnKRklKFnlqFnh5Vrj5Zps61ssqxirKRcu65btq1agYg6r6hLu65Ys6VptKlsqqRcmpdno5xrqadjq6xbrq5asa5dqaVjr6hsnZ1njZNsjZNjpqRnpKVelptfpqheqKxir65on6Run6NujJFobnRjd3pehYRhoJlhqJ9dtqpjgodAf4Q8mpRMraFfv69lsqVgurJeq6lcrKRjtq9ppKlapalaqq1crrBgoKRatrNlsLJksbNfsLVer7Rfp6tfuLNut61wr6lvi5Fjh5FndYNjdH9hgINgf4BZi4pdm5dZiYNRnJRRk45Pj45OraROua5bw7dhxLhdvrVNv7ZUu7Fitq5praxer7Ffs7NhoqhiiJVgjZpdo6hgmp1je4JliYdoj45ZnJhip6Rir6hlrapeoKJgq6hnt7BinpxakY5Oq6ZNtadWsKdQn5xDg4Y/pJxam5pVmJhCtrBHubVHtLBEvrVWyL1bxLteuLJlp6RllpxohpFmi5Nni5NioKJjpKZbtLNZwbxcyL5ZwblcvrdrrqlvurNguLNWqKRUnJhP" +
            "oZxBvKxdtqhauqtQr6lDl5dAfINEdHhOoptVmJdHsahhsaZns6R1o59TqqdbwrVntKxltaxxnp1vj5FboaFam59WeIBMmJhOjpRFg4lRd3lcj4pfqqBVpp9Kr6VPwrVTo51HsqZHoplamplPpJ9DtalJr6hFnZ1CjY5UjIpXh4ZblItlrqNotKprsK1emZ1RrapWuLJev7hmsq9nlphZe4NWdn9YjpNPiZJPdoFVjIhkk49Xh4RUj4pRrqRXqqNLfX1BqZpQoZlJsKdaqqRXp55hq6Fas6hTp6VLkJJKjI1Oe35JlJBLpJpWqJ1Yr6NmrqpTqaZhi4tkpJ9onJ5dj5pWfohXeIVVjpRUp6dPqalRtK1jt61auK5Rua1IuK9MpqNJnJVJm5RMlYtQva5jvLJetaxSpKFNho5FkpJMoZ9Lt7BStqtJurBHp6FXtaxfm5xXnZ5dkpRUl5lSoKNYqK1WsK9VrKtUk5hPk5NSo51dkopZj4ZYq5lepJVKtKdSrKdLt61SqaFbhIFKpJxRwrJXvKxdta1Vtq5moaFbmJZao6NOmZ1Esa1Lx71irapMsrBRsrFUtrFTubZZjJdTdYBMmZtZi45cnpJrj4hUfHhRaG1HgH1LhYNNp6JKvLJMr6hJrqZFtqtMwLJVq6Falo9ZnpZPwK9ZwbFisqlZsKtmmppVlJlMjZFLoJ5Utq5mlZdSeYRPio1MiIxclpdZpKBjsKZhoJddpJZkj4tdn5tVn5pUrqxNq6hQmJBJsahL" +
            "mpFEwq5pu69TrqZFxbZeu7BNrKFTj4tSmpVEt61TxbVNuaxUvLFZsahXm5ddl5dipaBojpJYlJNYpJ5bl5NYnpVbtapZqZ1bppxcr6ZXp6JJvLRLw7pbqKJSnJJYvrBhkI5BqZtPtahaqqRJq5tXv6xmuaxcuq9NqKVJj41DiolEsqZQqqNOq6VRqp9UvLBWqaJgm5ZgmZJmjo1WkY1Sq6RQtqtOt6VWo5pPl5BMg35BjYhKlJRGm5RUpJxTmJJJqaZGtK5Nuq9MqZ9VjohJq5dds6RYrqJZtKlar6hPtK1MsKhPkY1WmJRQsahZnp1Mp6NRo5tZnphXhoNQbnRLYGVBbGtDlZBOiopKanBGYWRGeXdTf3lRjIJdiIpBiIo6gok9mZw8sK1PubJGt6xZrKBNtalSuq5Jt6dUvLBbq6VUrqhTrahbjY5NgIFHnZdQnJhUlJFJkpBPhoRHhYNKiIVMY2dGaW1JZm1EdHhGhYRDdm9UYWY+bW5HT1ozU2E0jY1PlZFHnplHnJs/t7JWwLFbt69OsKpOp6BYsalOs6tBuq9TtKpWpaFZopthmpRZk5JSi41XgYNRmJVTmJJanZdbmJZVm5RXn5haenxTXmZCY2lBfIBIeX5EnplburRdlJVImpdLrKFrsKRikItHr55nsqpTq6dJq6dHrKhCvLFJt6o7tKhIvbBqu7RTvbNXrKZTlZNPkI5YjIlZkYxPkZBHnZdOvrFWlZBgpp5esahbop5Oi4tKcXhDhYdMmJhR" +
            "jZA8lZhEj5FCkJBHnptJqaFPtqxWs6hXsapMp6hIlpdArqZasqpGta9Ks61MvLFYsKJSr6Bhs6hbr6dVopxJgIM+g4FQq6Rao51QnJNWn5lZdHlJXmdFXWNCdHY+oZ9UnZ5Bs61Hp6NKrKZIwbVVwrRPuaxYrKBUsqdLv7VTvLdRj5FJa3E2ZGwybG5ClIhWsKFpnZRTe3w/hIk7lJBGrKNNua1JoJpQeXs9dXM/fXpFraJfqaVTkJFDi4xBoJtJWWo4dX4zkpRFq6hHs6w/rKY+ubBJt6levbJao6RKnZRdpJplg4BYcndGYmU6ZWdBe3VHl5BTZm4+cHczXGE+h39Qj4pQeHVHlI1MoZhUs6RYpJhUp6JRdXo6Wls0W2MxRlo0Wmk3bnY3fYQ8mJ08iJBCq6tLm5VPjYdNrKFjn5hPrKNOqKFRrKBfl5VNhYdBgYVCaXNDZWpCfHxJfHxLlpBaqJ5Ug4BMaW5Ac3NIj4pVraZagYdHTlw4a3FJWl82XmtGb3pHeX1Fi41Wio9PbHdHoZ97n5drpJlkmpJanpVXiINEko1Kq6VQsapPw7NfvrJUoJ1HcnM6kolNeHRHdnFRnZFju6tit65Xr6ZPhYBIY2VCi4VNmpU/raVKsqpKe4BBgII/g4Q8iopHkY1KjopldXpUn5hywbZ1v7RVsKpLuq1XsqlPqKRLs6tPs6Ziw7RdxrhbtqxOrKRKk5REfYJDfH1BgYNJgoJLb3ZJZGlLVlpBWWA1YmU2ZGUydHY2" +
            "p5xWvqxqraJTrJ9dmJJJp55OjIxFk4pPd3ZAq6FUwrpXwbdZrKhOgoU+anM8lJJJq6JJoJlJsqRota1Ttq1RsKpMrKlJm51Hop5InpxOZ3FCT18+VlxAU1Y6UVw/dXhBaHA8hn1QvKlmnpdQoZk/xrNeyLdoxbRhwbNSs6tGrqlGsa5HvLRRu7FVt7BblZY8q6FLlo1YkpFMo6RHtLBQq6dMjI9GZmlFYGI6bnBIrJ1+wbCQo5draHA9eXhBq6BQlIhxw6+HiYg/YWgvbHMyoptMl5dLp6dVxrRd1LlmwK9QnJk/i4xYu7CEsKlatqxKrqFWtKlfrqhJk5dElZM4pqBCqJ9Ju51/bWtSZ2ZKZmZBh3tehXphgXtlY2M+cGw4aGZJpZJlfXpUgX9Vm5VsYmxHRlkwaXM5p5tJtKlWqZ1frp1YjIk+goFBqZ9B0bVqzLNtwrFisaNbXmg6SlkpXmc1en9PnJBki4dfWGQ6VWA1Wl1Ai31jrZp+sp+CZ2JHjY1JdXRJlIpmtqZ1nZJ7bHE8WGc0UFosdG9Kens/k45AraM/vKxYw7FjyLJpo5ZUuadWwrNhradUkZFDhIk8ZG42f4g/rKlRrqlUoJtMmpVEko9lhntncGhSfHFVTE86iIlHV2Q5Ymg8dnZAeYVocXlKlZM/salJrKRKva1WrJ0+va9Mt6dRp5ZMuaZWzbRnzbVhwbBVwbJSwLNKyLlMzbtKvrJJoqNHl5xMp6VQjo5DsJ1ysJ5+Z2NLZmNNQ0k2" +
            "Y2szVWE0UF8ze35AZ3E6mJQ8pZ88e381dnNJtqJor6NIoZE80LV8uadSzbhpzrRyybNoy7NZwK5Sr6ZJuKRarZxOwK1Xwq1YxLNIvq9LpZpYnJZjlIxYZWlBYmlETFs3b3g5fHw/fnpFk45FaG46U14qZ24wfX5TmZVasKhOq59Fu6tUwq1U0LdqxK9qyrF5taNcuKdSw7FUtahLhH5AlY1FsZ9Zwq1jwq9OqpxLwKiCxrRurKRIpJ4/qaFElJQ/jYw+q59IvahfqptVWGA4OEgpXFxAgXhMlYpKq6BAq6pKu7BJwK9P0LJ10LV+0LaIsqJlv6hs1rtyv61Zp5Zgval728SlzbaXpJRddHAyoZRCvaxTx7VQq6JCo5xIlZI9nZw+r6hBop5Ia3I+QlItTFdAVVo1aWk0gH82tahbuKpMtqpEwqxV2r12wrBnybFxvaNwx656xrBjfH06Xl40gHFVoo9wi35SlpJAkIlKlIlJoJk6ppxDsqpIsalFr6U/kZU6paI9qaU9goc8TFc5S1U1ZmkwnJFYjINbu6eAzbV0tKVAvqtAvatDp51KkYJfvKB+xaqF0riZv6mITForX1w8bGk9iIw5mpo9r6lOhHxIeXVBopRYop1Iops+mZY4cXkxfIQxlJU4lZNIqpd5vquMrqJim5Y9nZdAuql1uqk9wLFDuaxDpaI+n51ApZU2oopYgW1SpIppoIVhZW4vO0AkTl4pdYE1i5I9hYc3fXlDemxPs553qJ5Ds6ZDp589" +
            "QU8fWF8zlYVmuqV9xq6T1b2j2b6g0b2blpFDpJ5OkZEzg4Uye34vg4tHn55ZkY03jnpahHRWlYFkin5WWWgsLTwdQE0oaXIxY2gzdHQ9b3E4YmM2lo5EsaRBsKQ+rKM7KjkcPUA3XFdPgXRntp+Iz7aZvKGAoYRjfntFXmcqSlUrZWZFQ1MlZGs5hYU1lZI6npVJsZ9mnYdmaWhAP0wkOUMoTU48cGpSdGpNfnhCqptVuqdtpZZRnI9EoJU/op05JzQhNT4uUVBHWlVPbmNZooRloolrm4BlWVo3MEEfQFEvQFMrUGAueX43tKdFwK1DwrNIxrhSsqJalYpeVVozSlUuRlAxZmVAioBIno9Sd3BFrJlto5VTsp5cuKNgpppSLkAjMD8oRUY+TU1KZV5WmYJkkH9VbGNKLDkbNUkjQ1gmQ1gnUl0sgH1Xn5NhtadNs6ZAwbNMv6pizrORwq2Hc3k5aG44jos7n5Y+qaA/n5RQk4o+vKxLuqtPsKRJraI/O0srTFovM0AwOT85WVZSf25bjHZdXFU/LTwcMEMfNkwlQFQmUV8mX2MxcGtAe3czfX0xnpc/wqd8ya+Qv6WEtqZOqJpKkI03n5g7xa1uxK1tsqNJqaFBo5s6oJg5nZY2UGIvLD0oO0kuPUM2VFBKf21ZgGxYTU00KDgdJTgdLEMjM0cjSFUlUlokYGYoV18nWmQqiYUxlIs9pYxsoY5roplBtKVCopk9s55ZtqBitKRQv61Oqp5Es6FIr6E9qJs0";

        [UnityTest]
        public IEnumerator CaptureTerrainLookdev()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Artifacts", "Terrain");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("Terrain Lookdev Test Camera");
            root.tag = "MainCamera";
            TerrainLookdev lookdev = root.AddComponent<TerrainLookdev>();
            Camera camera = lookdev.SceneCamera;

            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _),
                "Terrain lookdev did not register a valid voxel world.");
            VoxelRenderBridge.ResetSurfacePassDiagnostics("terrain-capture-start");

            var convergenceTarget = new RenderTexture(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            convergenceTarget.Create();
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = convergenceTarget;

            int stableFrames = 0;
            for (int frame = 0; frame < 360 && stableFrames < 3; frame++)
            {
                camera.Render();
                yield return null;
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                bool converged = metrics.SolidKnownChunks > 0
                    && metrics.SolidDirtyChunks == 0
                    && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                stableFrames = converged ? stableFrames + 1 : 0;
            }

            camera.targetTexture = previousTarget;
            convergenceTarget.Release();
            UnityEngine.Object.DestroyImmediate(convergenceTarget);

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.Greater(VoxelRenderBridge.RenderFeatureEnqueueCount, 0,
                "VoxelRenderFeature never enqueued for the terrain camera.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "Voxel surface pass never recorded for the terrain camera.");
            Assert.GreaterOrEqual(stableFrames, 3,
                $"Terrain surface did not converge: known={finalMetrics.SolidKnownChunks}, " +
                $"resident={finalMetrics.SolidResidentChunks}, dirty={finalMetrics.SolidDirtyChunks}, " +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}, " +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}, " +
                $"state={VoxelRenderBridge.LastSurfacePassState}");

            string capturePath = Path.Combine(outputDirectory, "terrain.png");
            Texture2D captured = Capture(camera, capturePath, CaptureWidth, CaptureHeight);
            byte[] actualRgb = DownsampleTopLeftRgb(captured, ReferenceWidth, ReferenceHeight);
            byte[] referenceRgb = Convert.FromBase64String(ReferenceRgbBase64);
            Assert.AreEqual(ReferenceWidth * ReferenceHeight * 3, referenceRgb.Length,
                "Embedded terrain reference byte count is invalid.");

            float rgbMae = RgbMae(actualRgb, referenceRgb);
            float patchSsim = PatchLumaSsim(
                actualRgb, referenceRgb, ReferenceWidth, ReferenceHeight, 4);
            WriteDiff(Path.Combine(outputDirectory, "terrain-diff.png"),
                actualRgb, referenceRgb, ReferenceWidth, ReferenceHeight);
            WriteReference(Path.Combine(outputDirectory, "terrain-reference.png"),
                referenceRgb, ReferenceWidth, ReferenceHeight);

            File.WriteAllText(Path.Combine(outputDirectory, "terrain-similarity.txt"),
                $"patchSsim={patchSsim:F4}\n" +
                $"rgbMae={rgbMae:F4}\n" +
                $"minimumPatchSsim={MinimumPatchSsim:F4}\n" +
                $"capture={CaptureWidth}x{CaptureHeight}\n" +
                $"reference={ReferenceWidth}x{ReferenceHeight}\n");

            File.WriteAllText(Path.Combine(outputDirectory, "terrain.txt"),
                $"knownChunks={finalMetrics.SolidKnownChunks}\n" +
                $"residentChunks={finalMetrics.SolidResidentChunks}\n" +
                $"dirtyChunks={finalMetrics.SolidDirtyChunks}\n" +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}\n" +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}\n" +
                $"surfaceState={VoxelRenderBridge.LastSurfacePassState}\n" +
                $"patchSsim={patchSsim:F4}\n" +
                $"rgbMae={rgbMae:F4}\n");

            UnityEngine.Object.DestroyImmediate(captured);
            lookdev.Shutdown();
            UnityEngine.Object.Destroy(root);
            yield return null;

            Assert.GreaterOrEqual(patchSsim, MinimumPatchSsim,
                $"Terrain is not visually similar enough to the original reference. " +
                $"patch SSIM={patchSsim:F4} (required >= {MinimumPatchSsim:F4}), " +
                $"RGB MAE={rgbMae:F4}. Inspect terrain.png and terrain-diff.png.");
        }

        private static Texture2D Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            return texture;
        }

        private static byte[] DownsampleTopLeftRgb(Texture2D source, int width, int height)
        {
            Assert.AreEqual(0, source.width % width);
            Assert.AreEqual(0, source.height % height);
            int blockWidth = source.width / width;
            int blockHeight = source.height / height;
            Color32[] pixels = source.GetPixels32();
            var result = new byte[width * height * 3];
            for (int outY = 0; outY < height; outY++)
            {
                int topSourceY = outY * blockHeight;
                for (int outX = 0; outX < width; outX++)
                {
                    int sumR = 0, sumG = 0, sumB = 0;
                    for (int dy = 0; dy < blockHeight; dy++)
                    {
                        int unityY = source.height - 1 - (topSourceY + dy);
                        int row = unityY * source.width;
                        for (int dx = 0; dx < blockWidth; dx++)
                        {
                            Color32 p = pixels[row + outX * blockWidth + dx];
                            sumR += p.r;
                            sumG += p.g;
                            sumB += p.b;
                        }
                    }
                    int samples = blockWidth * blockHeight;
                    int index = (outY * width + outX) * 3;
                    result[index] = (byte)(sumR / samples);
                    result[index + 1] = (byte)(sumG / samples);
                    result[index + 2] = (byte)(sumB / samples);
                }
            }
            return result;
        }

        private static float RgbMae(byte[] actual, byte[] reference)
        {
            Assert.AreEqual(actual.Length, reference.Length);
            double sum = 0.0;
            for (int i = 0; i < actual.Length; i++)
                sum += Math.Abs(actual[i] - reference[i]) / 255.0;
            return (float)(sum / actual.Length);
        }

        private static float PatchLumaSsim(
            byte[] actual, byte[] reference, int width, int height, int patchSize)
        {
            const double c1 = 0.0001;
            const double c2 = 0.0009;
            double score = 0.0;
            int patches = 0;
            for (int y0 = 0; y0 < height; y0 += patchSize)
            for (int x0 = 0; x0 < width; x0 += patchSize)
            {
                int x1 = Math.Min(x0 + patchSize, width);
                int y1 = Math.Min(y0 + patchSize, height);
                int count = (x1 - x0) * (y1 - y0);
                double meanA = 0.0, meanB = 0.0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * width + x) * 3;
                    meanA += Luma(actual, i);
                    meanB += Luma(reference, i);
                }
                meanA /= count;
                meanB /= count;
                double varianceA = 0.0, varianceB = 0.0, covariance = 0.0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * width + x) * 3;
                    double da = Luma(actual, i) - meanA;
                    double db = Luma(reference, i) - meanB;
                    varianceA += da * da;
                    varianceB += db * db;
                    covariance += da * db;
                }
                varianceA /= count;
                varianceB /= count;
                covariance /= count;
                double numerator = (2.0 * meanA * meanB + c1) * (2.0 * covariance + c2);
                double denominator = (meanA * meanA + meanB * meanB + c1) *
                    (varianceA + varianceB + c2);
                score += denominator > 0.0 ? numerator / denominator : 1.0;
                patches++;
            }
            return (float)(score / patches);
        }

        private static double Luma(byte[] rgb, int index)
        {
            return (0.2126 * rgb[index] + 0.7152 * rgb[index + 1] +
                0.0722 * rgb[index + 2]) / 255.0;
        }

        private static void WriteDiff(
            string path, byte[] actual, byte[] reference, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (int topY = 0; topY < height; topY++)
            for (int x = 0; x < width; x++)
            {
                int source = (topY * width + x) * 3;
                byte r = (byte)Math.Abs(actual[source] - reference[source]);
                byte g = (byte)Math.Abs(actual[source + 1] - reference[source + 1]);
                byte b = (byte)Math.Abs(actual[source + 2] - reference[source + 2]);
                int unityY = height - 1 - topY;
                pixels[unityY * width + x] = new Color32(r, g, b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void WriteReference(string path, byte[] rgb, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (int topY = 0; topY < height; topY++)
            for (int x = 0; x < width; x++)
            {
                int source = (topY * width + x) * 3;
                int unityY = height - 1 - topY;
                pixels[unityY * width + x] = new Color32(
                    rgb[source], rgb[source + 1], rgb[source + 2], 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
