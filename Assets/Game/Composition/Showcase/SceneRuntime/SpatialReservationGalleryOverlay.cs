using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Presentation-only 3D inset for the spatial-reservation evidence report. The overlay owns no
    /// reservation state, colliders, or placement decisions: it copies the read-only report into one
    /// transient line mesh parented to the gallery camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialReservationGalleryOverlay : MonoBehaviour
    {
        private const string GallerySceneName = "WorldbuildingGalleryShowcase";
        private const float InsetDistance = 6.2f;

        private GameObject _meshObject;
        private Mesh _mesh;
        private Material _material;
        private WorldbuildingGalleryReservationReport _report;
        private bool _visible;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;

        public bool Visible => _visible;
        public WorldbuildingGalleryReservationReport Report => _report;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, GallerySceneName, StringComparison.Ordinal))
                return;
            WorldbuildingGalleryShowcase showcase =
                UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>();
            if (showcase == null || showcase.GetComponent<SpatialReservationGalleryOverlay>() != null)
                return;
            showcase.gameObject.AddComponent<SpatialReservationGalleryOverlay>();
        }

        private void Awake()
        {
            _report = WorldbuildingGalleryReservationInspection.Build();
            BuildMesh();
            SetVisible(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
                SetVisible(!_visible);
        }

        private void OnDestroy()
        {
            if (_meshObject != null) Destroy(_meshObject);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_meshObject != null) _meshObject.SetActive(visible);
        }

        private void BuildMesh()
        {
            var vertices = new List<Vector3>((_report.Primitives.Count + 1) * 24);
            var colours = new List<Color>(vertices.Capacity);
            var indices = new List<int>(vertices.Capacity);

            for (int i = 0; i < _report.Primitives.Count; i++)
            {
                ReservationInspectionPrimitive primitive = _report.Primitives[i];
                AddWireBox(vertices, colours, indices, primitive.BoundsDm, ColourFor(primitive), false);
            }
            AddWireBox(
                vertices,
                colours,
                indices,
                _report.RejectedCandidate.BoundsDm,
                new Color(1f, 0.15f, 0.12f, 1f),
                true);

            _mesh = new Mesh { name = "Spatial Reservation Inspection Lines" };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colours);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0, calculateBounds: true);

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                throw new InvalidOperationException("Spatial reservation gallery overlay requires an unlit runtime shader.");

            _material = new Material(shader)
            {
                name = "Spatial Reservation Inspection Material",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Overlay
            };
            if (_material.HasProperty("_ZWrite")) _material.SetInt("_ZWrite", 0);
            if (_material.HasProperty("_ZTest")) _material.SetInt("_ZTest", (int)CompareFunction.Always);

            _meshObject = new GameObject("Spatial Reservation Inspection Inset")
            {
                hideFlags = HideFlags.DontSave
            };
            _meshObject.transform.SetParent(transform, worldPositionStays: false);
            _meshObject.transform.localPosition = new Vector3(0f, -0.35f, InsetDistance);
            _meshObject.transform.localRotation = Quaternion.identity;
            _meshObject.transform.localScale = Vector3.one;
            MeshFilter filter = _meshObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            MeshRenderer renderer = _meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void AddWireBox(
            List<Vector3> vertices,
            List<Color> colours,
            List<int> indices,
            ReservationBoundsDm bounds,
            Color colour,
            bool inflate)
        {
            Vector3 min = Map(bounds.MinX, bounds.MinY, bounds.MinZ);
            Vector3 max = Map(bounds.MaxX, bounds.MaxY, bounds.MaxZ);
            if (inflate)
            {
                var padding = new Vector3(0.06f, 0.06f, 0.06f);
                min -= padding;
                max += padding;
            }

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
            };
            int[] edgeCorners =
            {
                0,1, 1,2, 2,3, 3,0,
                4,5, 5,6, 6,7, 7,4,
                0,4, 1,5, 2,6, 3,7
            };
            for (int edge = 0; edge < edgeCorners.Length; edge += 2)
            {
                int baseVertex = vertices.Count;
                vertices.Add(corners[edgeCorners[edge]]);
                vertices.Add(corners[edgeCorners[edge + 1]]);
                colours.Add(colour);
                colours.Add(colour);
                indices.Add(baseVertex);
                indices.Add(baseVertex + 1);
            }
        }

        private Vector3 Map(int xDm, int yDm, int zDm)
        {
            ReservationBoundsDm window = _report.Window;
            float centreX = (window.MinX + window.MaxX) * 0.5f;
            float centreY = (window.MinY + window.MaxY) * 0.5f;
            float centreZ = (window.MinZ + window.MaxZ) * 0.5f;
            float xScale = 5.4f / Math.Max(1, window.MaxX - window.MinX);
            float yScale = 2.7f / Math.Max(1, window.MaxY - window.MinY);
            float zScale = 3.6f / Math.Max(1, window.MaxZ - window.MinZ);
            return new Vector3(
                (xDm - centreX) * xScale,
                (yDm - centreY) * yScale,
                (zDm - centreZ) * zScale);
        }

        private static Color ColourFor(in ReservationInspectionPrimitive primitive)
        {
            if ((primitive.Category & ReservationCategory.Underground) != 0)
                return new Color(0.85f, 0.25f, 1f, 1f);
            if ((primitive.Category & ReservationCategory.PublicAccess) != 0)
                return new Color(0.25f, 1f, 0.35f, 1f);
            if ((primitive.Category & ReservationCategory.Road) != 0)
                return new Color(1f, 0.8f, 0.15f, 1f);
            if ((primitive.Semantics & ReservationSemantics.Clearance) != 0)
                return new Color(0.15f, 0.9f, 1f, 1f);
            return Color.white;
        }

        private void OnGUI()
        {
            if (!_visible || _report == null) return;
            EnsureStyles();

            const float width = 540f;
            Rect panel = new Rect(18f, 18f, width, 184f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(32f, 28f, width - 28f, 28f),
                "Spatial reservations — read-only inspection", _headingStyle);
            GUI.Label(new Rect(32f, 58f, width - 28f, 44f),
                "WHITE hard   CYAN clearance   GREEN access   YELLOW road   MAGENTA underground   RED rejected",
                _bodyStyle);
            ReservationQueryMetrics metrics = _report.RejectedCandidateMetrics;
            GUI.Label(new Rect(32f, 103f, width - 28f, 28f),
                $"claims={_report.SourceClaimCount}  query buckets={metrics.BucketsVisited} candidates={metrics.CandidatesVisited}",
                _bodyStyle);
            GUI.Label(new Rect(32f, 132f, width - 28f, 58f),
                "Rejected candidate: " + _report.RejectedCandidateDescription,
                _bodyStyle);
        }

        private void EnsureStyles()
        {
            if (_headingStyle != null) return;
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
        }
    }
}
