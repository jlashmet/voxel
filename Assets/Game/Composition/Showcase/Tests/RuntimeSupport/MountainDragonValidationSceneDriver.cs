using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using UnityEngine;

namespace VoxelEngine.Showcase.Tests.RuntimeSupport
{
    /// <summary>
    /// Focused validation surface for Mountain Dragon. Geometry comes from the same natural
    /// MountainLandformSurface and resolved WorldRoadNetwork used by production; this driver only
    /// verifies and stages that authored result for the standalone validation scene.
    /// </summary>
    public sealed class MountainDragonValidationSceneDriver : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
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
                MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
                WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
                WorldRoadNetworkRoute route = ValidateResolvedRoute(surface, network);
                ValidateSummitApproach(surface, route);
                StageFocusedMountain(surface, route);
                ConfigureView(surface.Spec);

                _complete = true;
                _passed = true;
                _detail = $"PASS: natural mountain + resolved {route.Road.Points.Count}-point WorldRoad ascent + summit marker";
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

        private static WorldRoadNetworkRoute ValidateResolvedRoute(
            MountainLandformSurface surface,
            WorldRoadNetwork network)
        {
            if (!network.TryGetRoute(ShowcaseMountainDragonLayout.AscentRouteId, out WorldRoadNetworkRoute route))
                throw new InvalidOperationException("Production Mountain Dragon ascent route is missing.");
            if (!route.Road.IsResolved || route.Road.Points.Count < 2)
                throw new InvalidOperationException("Production Mountain Dragon ascent is not resolved.");

            int maximumGrade = route.Road.Intent.Profile.MaximumGradePermille;
            int maximumCutFill = route.Road.Intent.Profile.MaximumCutFillDm;
            int mountainSamples = 0;
            int risingSegments = 0;

            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                int mountainHeight = surface.HeightAtDm(point.Xdm, point.Zdm);
                if (mountainHeight > surface.Spec.OriginYdm)
                {
                    mountainSamples++;
                    if (Math.Abs(point.Ydm - mountainHeight) > maximumCutFill)
                    {
                        throw new InvalidOperationException(
                            $"Resolved road point {i} exceeds the authored mountain cut/fill bound.");
                    }
                }

                if (i == 0) continue;
                ResolvedWorldRoadPoint previous = route.Road.Points[i - 1];
                long dx = (long)point.Xdm - previous.Xdm;
                long dz = (long)point.Zdm - previous.Zdm;
                int horizontal = (int)Math.Sqrt(dx * dx + dz * dz);
                int rise = Math.Abs(point.Ydm - previous.Ydm);
                if (rise > 0) risingSegments++;
                if (horizontal <= 0)
                {
                    if (rise > 0)
                        throw new InvalidOperationException($"Resolved road segment {i - 1} forms a vertical tower.");
                    continue;
                }
                if ((long)rise * 1000L > (long)horizontal * maximumGrade)
                {
                    throw new InvalidOperationException(
                        $"Resolved road segment {i - 1} exceeds the configured grade bound.");
                }
            }

            if (mountainSamples < 8)
                throw new InvalidOperationException("Resolved ascent does not remain on the authored mountain long enough to validate integration.");
            if (risingSegments < 3)
                throw new InvalidOperationException("Resolved ascent does not materially climb the mountain.");
            return route;
        }

        private static void ValidateSummitApproach(
            MountainLandformSurface surface,
            WorldRoadNetworkRoute route)
        {
            MountainLandformMass summit = surface.GetMass(0);
            ResolvedWorldRoadPoint approach = route.Road.Points[route.Road.Points.Count - 1];
            long dx = (long)approach.Xdm - summit.CentreXdm;
            long dz = (long)approach.Zdm - summit.CentreZdm;
            long allowed = Math.Max(summit.TopRadiusDm, surface.Spec.SummitRadiusDm);
            if (dx * dx + dz * dz > allowed * allowed)
                throw new InvalidOperationException("Resolved Mountain Dragon approach no longer reaches the summit crest.");
            if (Math.Abs(approach.Ydm - surface.HeightAtDm(approach.Xdm, approach.Zdm))
                > route.Road.Intent.Profile.MaximumCutFillDm)
                throw new InvalidOperationException("Resolved summit approach is unsupported by the natural mountain surface.");
        }

        private void StageFocusedMountain(
            MountainLandformSurface surface,
            WorldRoadNetworkRoute route)
        {
            MountainLandformMass summit = surface.GetMass(0);
            int baseY = surface.Spec.OriginYdm;

            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Mountain Mass " + i;
                body.transform.SetParent(transform, false);
                body.transform.localPosition = new Vector3(
                    (mass.CentreXdm - summit.CentreXdm) * ViewScale,
                    (mass.BaseYdm - baseY + mass.HeightDm * 0.5f) * ViewScale,
                    (mass.CentreZdm - summit.CentreZdm) * ViewScale);
                body.transform.localScale = new Vector3(
                    mass.BaseRadiusDm * 2f * ViewScale,
                    mass.HeightDm * 0.5f * ViewScale,
                    mass.BaseRadiusDm * 2f * ViewScale);
                SetColor(body, new Color(0.22f, 0.24f, 0.22f));
            }

            for (int i = 1; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint previous = route.Road.Points[i - 1];
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                Vector3 a = ToLocal(previous, summit, baseY);
                Vector3 b = ToLocal(point, summit, baseY);
                StagePathSegment(a, b, route.CarriagewayWidthDm * ViewScale, "Resolved Road " + (i - 1));
            }

            GameObject dragon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dragon.name = "Dragon Placeholder";
            dragon.transform.SetParent(transform, false);
            dragon.transform.localPosition = new Vector3(
                0f,
                (summit.TopYdm + 1 - baseY + ShowcaseMountainDragonLayout.PlaceholderSize * 0.5f) * ViewScale,
                0f);
            dragon.transform.localScale = Vector3.one * ShowcaseMountainDragonLayout.PlaceholderSize * ViewScale;
            SetColor(dragon, new Color(0.75f, 0.08f, 0.05f));
        }

        private static Vector3 ToLocal(
            ResolvedWorldRoadPoint point,
            MountainLandformMass summit,
            int baseY) =>
            new Vector3(
                (point.Xdm - summit.CentreXdm) * ViewScale,
                (point.Ydm - baseY + 1) * ViewScale,
                (point.Zdm - summit.CentreZdm) * ViewScale);

        private void StagePathSegment(Vector3 a, Vector3 b, float width, string label)
        {
            Vector3 delta = b - a;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = label;
            path.transform.SetParent(transform, false);
            path.transform.localPosition = (a + b) * 0.5f;
            path.transform.localScale = new Vector3(horizontal + width, 0.16f, width);
            path.transform.localRotation = Quaternion.Euler(
                0f,
                -Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg,
                Mathf.Atan2(delta.y, Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg);
            SetColor(path, new Color(0.45f, 0.28f, 0.12f));
        }

        private void ConfigureView(in MountainLandformSpec spec)
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
            camera.transform.LookAt(new Vector3(0f, spec.HeightDm * 0.045f, 0f));

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
