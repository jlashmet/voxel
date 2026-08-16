using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Human-review animation gallery: every ambient-life species occupies a deterministic cell
    /// with its movement form labelled underneath. The production renderer still owns every agent;
    /// labels and layout are validation-only presentation.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Ambient Life Animation Validation")]
    [DisallowMultipleComponent]
    public sealed class AmbientLifeAnimationValidationShowcase : MonoBehaviour
    {
        [SerializeField] private uint m_Seed = 0xA11F17E5u;
        [SerializeField] private bool m_CreateEnvironment = true;
        [SerializeField] private bool m_CreateLabels = true;

        private readonly List<AmbientLifeCluster> _clusters = new List<AmbientLifeCluster>();
        private IAmbientLifeBatchRenderer _renderer;
        private Transform _labelsRoot;
        private Camera _reviewCamera;
        private CameraState _cameraBeforeReview;
        private bool _hasCameraBeforeReview;

        public IAmbientLifeBatchRenderer Renderer => _renderer;
        public IReadOnlyList<AmbientLifeCluster> Clusters => _clusters;
        public int ClusterCount => _clusters.Count;
        public int AgentCount => _renderer != null ? _renderer.AgentCount : 0;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Rebuild();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            RestoreCameraBeforeReview();
        }

        private void LateUpdate()
        {
            FaceLabelsToCamera();
        }

        public void Rebuild()
        {
            if (_renderer == null)
                _renderer = VegetationLifeRenderingComposition.EnsureAmbientLifeBatchRenderer(gameObject);

            if (m_CreateEnvironment)
            {
                SubsystemRenderingShowcaseEnvironment.Ensure(transform);
                CaptureCameraBeforeReview(Camera.main);
                ConfigureReviewCamera(_reviewCamera);
            }

            BuildClusters(m_Seed, _clusters);
            _renderer.SetClusters(_clusters);

            if (m_CreateLabels)
                RebuildLabels();
        }

        public void SetLabelsVisible(bool visible)
        {
            if (_labelsRoot != null)
                _labelsRoot.gameObject.SetActive(visible);

            // Temporal CI hides the labels before measuring image-space motion. Restore the camera
            // that CI explicitly created so human-review framing can never change numeric metrics;
            // when labels return, put the dedicated review framing back for the labelled artifact.
            if (!m_CreateEnvironment) return;
            if (visible)
                ConfigureReviewCamera(_reviewCamera != null ? _reviewCamera : Camera.main);
            else
                RestoreCameraBeforeReview();
        }

        public static void BuildClusters(uint seed, List<AmbientLifeCluster> output)
        {
            output.Clear();
            const int columns = 4;
            const float spacingX = 4.4f;
            // A little more depth spacing plus a steep orthographic review camera keeps all four
            // rows equally readable instead of foreshortening the distant labels into nearby swarms.
            const float spacingZ = 5.0f;

            for (int i = 0; i < AmbientLifeCatalogue.Count; i++)
            {
                AmbientLifeKind kind = AmbientLifeCatalogue.KindAt(i);
                int column = i % columns;
                int row = i / columns;
                uint clusterSeed = seed + (uint)i * 0x9E3779B9u;

                output.Add(new AmbientLifeCluster
                {
                    PositionMetres = new float3(
                        (column - 1.5f) * spacingX,
                        0.05f,
                        1.4f + row * spacingZ),
                    Kind = kind,
                    Seed = clusterSeed == 0u ? 1u : clusterSeed,
                    Count = (ushort)(5 + i % 2),
                    RadiusMetres = 1.55f,
                });
            }
        }

        private void RebuildLabels()
        {
            Transform existing = transform.Find("Animation Validation Labels");
            if (existing != null)
                Destroy(existing.gameObject);

            GameObject root = new GameObject("Animation Validation Labels");
            root.transform.SetParent(transform, false);
            _labelsRoot = root.transform;

            for (int i = 0; i < _clusters.Count; i++)
            {
                AmbientLifeCluster cluster = _clusters[i];
                AmbientLifeProfile profile = AmbientLifeCatalogue.Get(cluster.Kind);

                GameObject labelObject = new GameObject(cluster.Kind + " Label");
                labelObject.transform.SetParent(_labelsRoot, false);
                // Keep each caption in the inter-row gap but biased toward its own species row so
                // tall agents from the following visible row cannot occlude the review text.
                labelObject.transform.position = new Vector3(
                    cluster.PositionMetres.x,
                    0.10f,
                    cluster.PositionMetres.z - 1.65f);

                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.text = cluster.Kind + " / " + profile.Movement;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.045f;
                label.fontSize = 64;
                label.fontStyle = FontStyle.Bold;
                label.color = new Color(0.96f, 0.98f, 1f, 1f);
            }

            FaceLabelsToCamera();
        }

        private void CaptureCameraBeforeReview(Camera camera)
        {
            if (camera == null || _hasCameraBeforeReview) return;

            _reviewCamera = camera;
            _cameraBeforeReview = new CameraState
            {
                Orthographic = camera.orthographic,
                OrthographicSize = camera.orthographicSize,
                FieldOfView = camera.fieldOfView,
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
            };
            _hasCameraBeforeReview = true;
        }

        private void RestoreCameraBeforeReview()
        {
            if (!_hasCameraBeforeReview || _reviewCamera == null) return;

            _reviewCamera.orthographic = _cameraBeforeReview.Orthographic;
            _reviewCamera.orthographicSize = _cameraBeforeReview.OrthographicSize;
            _reviewCamera.fieldOfView = _cameraBeforeReview.FieldOfView;
            _reviewCamera.transform.SetPositionAndRotation(_cameraBeforeReview.Position, _cameraBeforeReview.Rotation);
        }

        private static void ConfigureReviewCamera(Camera camera)
        {
            if (camera == null) return;

            // This is a species/movement review plate, not a perspective beauty shot. A steeper
            // orthographic view converts the gallery's Z spacing into real screen-space row
            // separation; the wider frame keeps every caption band safely inside the image.
            camera.orthographic = true;
            camera.orthographicSize = 8.4f;
            camera.transform.position = new Vector3(0f, 13.35f, -4.3f);
            camera.transform.LookAt(new Vector3(0f, 1.35f, 8.9f));
        }

        private void FaceLabelsToCamera()
        {
            if (_labelsRoot == null || !_labelsRoot.gameObject.activeInHierarchy) return;
            Camera camera = Camera.main;
            if (camera == null) return;

            for (int i = 0; i < _labelsRoot.childCount; i++)
            {
                Transform label = _labelsRoot.GetChild(i);
                label.LookAt(camera.transform.position, camera.transform.up);
                label.Rotate(0f, 180f, 0f);
            }
        }

        private struct CameraState
        {
            public bool Orthographic;
            public float OrthographicSize;
            public float FieldOfView;
            public Vector3 Position;
            public Quaternion Rotation;
        }
    }
}
