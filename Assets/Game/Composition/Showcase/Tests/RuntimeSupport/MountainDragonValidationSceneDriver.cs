using System;
using Game.WorldBuilder.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase.Tests.RuntimeSupport
{
    /// <summary>
    /// Player-safe focused validation surface for Mountain Dragon topology/headroom.
    /// Geometry comes from the same production ShowcaseMountainDragonLayout and
    /// WorldBuilderMountainLandmarkCatalogue used by the shipped composition; this
    /// driver only validates/stages that authored result for a small standalone scene.
    /// </summary>
    public sealed class MountainDragonValidationSceneDriver : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
        private const byte RockMaterial = 6;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;
        private const float ViewScale = 0.1f;

        private bool _complete;
        private bool _passed;
        private string _detail = "NOT RUN";

        public bool Complete => _complete;
        public bool Passed => _passed;
        public string Detail => _detail;

        private void Start() => RunValidation();

        public void RunValidation()
        {
            ClearGeneratedChildren();
            try
            {
                MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
                ValidateRouteGeometry(in spec);
                ValidateProductionHeadroomProgram(in spec);
                StageFocusedMountain(in spec);
                ConfigureView(in spec);

                _complete = true;
                _passed = true;
                _detail = $"PASS: shell-following route + {spec.PathClearanceWidthVoxels}-voxel centered headroom + focused mountain staging";
                Debug.Log("[MountainDragonValidation] " + _detail);
            }
            catch (Exception exception)
            {
                _complete = true;
                _passed = false;
                _detail = exception.GetType().Name + ": " + exception.Message;
                Debug.LogError("[MountainDragonValidation] FAIL: " + _detail);
                Debug.LogException(exception);
            }
        }

        private static void ValidateRouteGeometry(in MountainLandmarkSpec spec)
        {
            MountainPathTierGeometry previousTier = default;
            bool narrowed = false;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                if (tier.PathWidth != spec.PathWidth)
                    throw new InvalidOperationException($"Tier {level} path width drifted from authored width.");

                for (int segment = 0; segment < tier.SegmentCount; segment++)
                {
                    MountainPathSegmentGeometry geometry = tier.SegmentGeometry(segment);
                    int coreStart = spec.CoreMinLocalZAtHeight(geometry.StartY);
                    int coreEnd = spec.CoreMinLocalZAtHeight(geometry.EndY);

                    if (geometry.PathWidth != spec.PathWidth)
                        throw new InvalidOperationException($"Tier {level} segment {segment} changed path width.");
                    if (!(geometry.StartLocalZ < coreStart && geometry.StartLocalZ + spec.PathWidth > coreStart))
                        throw new InvalidOperationException($"Tier {level} segment {segment} misses shell at low end.");
                    if (!(geometry.EndLocalZ < coreEnd && geometry.EndLocalZ + spec.PathWidth > coreEnd))
                        throw new InvalidOperationException($"Tier {level} segment {segment} misses shell at high end.");
                    int horizontalAdvance = geometry.Run - spec.PathWidth;
                    if (!spec.TraversalProfile.SupportsRamp(horizontalAdvance, geometry.Rise))
                        throw new InvalidOperationException($"Tier {level} segment {segment} exceeds the configured traversal grade.");
                }

                if (level > 0)
                {
                    if (tier.LowLandingMinX != previousTier.HighLandingMinX || tier.LocalZ != previousTier.EndLocalZ)
                        throw new InvalidOperationException($"Tier {level} no longer joins the prior landing exactly.");
                    if (tier.Run > previousTier.Run)
                        throw new InvalidOperationException($"Tier {level} widens while the core tapers.");
                    narrowed |= tier.Run < previousTier.Run;
                }

                previousTier = tier;
            }

            if (!narrowed)
                throw new InvalidOperationException("Upper switchbacks never narrow with elevation.");
        }

        private static void ValidateProductionHeadroomProgram(in MountainLandmarkSpec spec)
        {
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec, RockMaterial, PathMaterial, DragonMaterial, Allocator.Temp);
            try
            {
                FeatureDefinition landform = catalogue.Definitions[0];
                int expectedSegmentCarves = 0;
                for (int level = 0; level < spec.SwitchbackCount; level++)
                    expectedSegmentCarves += spec.PathTier(level).SegmentCount;

                int carveCount = 0;
                int segmentCarveCount = 0;
                int end = landform.ProgramOffset + landform.ProgramLength;
                for (int pc = landform.ProgramOffset; pc < end;)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    if (op == ShapeOp.End) break;
                    int length = ShapeOps.InstructionLength(op);
                    if (length <= 0)
                        throw new InvalidOperationException("Mountain program contains an invalid instruction length.");

                    if (op == ShapeOp.EmitBox && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Carve)
                    {
                        carveCount++;
                        if (segmentCarveCount < expectedSegmentCarves)
                        {
                            int carveDepth = catalogue.Program[pc + 7];
                            if (carveDepth != spec.PathClearanceWidthVoxels)
                            {
                                throw new InvalidOperationException(
                                    $"Segment headroom carve {segmentCarveCount} is {carveDepth} voxels wide; expected " +
                                    spec.PathClearanceWidthVoxels + ".");
                            }
                            segmentCarveCount++;
                        }
                    }
                    pc += length;
                }

                if (segmentCarveCount != expectedSegmentCarves)
                    throw new InvalidOperationException($"Expected {expectedSegmentCarves} segmented headroom carves, found {segmentCarveCount}.");
                if (carveCount != expectedSegmentCarves + 2)
                    throw new InvalidOperationException($"Expected {expectedSegmentCarves + 2} total headroom carves, found {carveCount}.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private void StageFocusedMountain(in MountainLandmarkSpec spec)
        {
            const int layers = 9;
            for (int layer = 0; layer < layers; layer++)
            {
                float t0 = layer / (float)layers;
                float t1 = (layer + 1) / (float)layers;
                int y0 = Mathf.RoundToInt(spec.MountainHeight * t0);
                int y1 = Mathf.RoundToInt(spec.MountainHeight * t1);
                int radius = spec.CoreRadiusAtHeight((y0 + y1) / 2);
                GameObject mass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mass.name = "Mountain Core " + layer;
                mass.transform.SetParent(transform, false);
                mass.transform.localPosition = new Vector3(0f, (y0 + y1) * 0.5f * ViewScale, 0f);
                mass.transform.localScale = new Vector3(radius * 2f * ViewScale, (y1 - y0) * 0.5f * ViewScale, radius * 2f * ViewScale);
                SetColor(mass, new Color(0.22f, 0.24f, 0.22f));
            }

            Vector3 origin = new Vector3(-spec.CentreLocal, 0f, -spec.CentreLocal) * ViewScale;
            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                for (int segment = 0; segment < tier.SegmentCount; segment++)
                {
                    MountainPathSegmentGeometry geometry = tier.SegmentGeometry(segment);
                    Vector3 a = origin + new Vector3(geometry.LowCentreX, geometry.StartY + 1, geometry.LowCentreZ) * ViewScale;
                    Vector3 b = origin + new Vector3(geometry.HighCentreX, geometry.EndY + 1, geometry.HighCentreZ) * ViewScale;
                    StagePathSegment(a, b, spec.PathWidth * ViewScale, $"Tier {level} Segment {segment}");
                }
            }

            GameObject dragon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dragon.name = "Dragon Placeholder";
            dragon.transform.SetParent(transform, false);
            dragon.transform.localPosition = new Vector3(0f, (spec.MountainHeight + 1 + spec.PlaceholderSize * 0.5f) * ViewScale, 0f);
            dragon.transform.localScale = Vector3.one * spec.PlaceholderSize * ViewScale;
            SetColor(dragon, new Color(0.75f, 0.08f, 0.05f));
        }

        private void StagePathSegment(Vector3 a, Vector3 b, float width, string label)
        {
            Vector3 delta = b - a;
            float length = new Vector2(delta.x, delta.z).magnitude;
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = label;
            path.transform.SetParent(transform, false);
            path.transform.localPosition = (a + b) * 0.5f;
            path.transform.localScale = new Vector3(length + width, 0.16f, width);
            path.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg, Mathf.Atan2(delta.y, Mathf.Max(0.001f, length)) * Mathf.Rad2Deg);
            SetColor(path, new Color(0.45f, 0.28f, 0.12f));
        }

        private void ConfigureView(in MountainLandmarkSpec spec)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Validation Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(transform, false);
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 42f;
            camera.transform.position = new Vector3(65f, 42f, -72f);
            camera.transform.LookAt(new Vector3(0f, spec.MountainHeight * 0.045f, 0f));

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Validation Sun");
                lightObject.transform.SetParent(transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }
    }
}
