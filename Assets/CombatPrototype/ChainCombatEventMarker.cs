using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Presentation-only readability layer. It makes the unresolved physical fact conspicuous without exposing
    /// which recruits/capabilities can answer it. Reflection is intentionally confined to this prototype adapter
    /// so the authoritative board does not depend on presentation code.
    /// </summary>
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainCombatEventMarker : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private Camera _camera;
        private GameObject _marker;
        private Renderer _markerRenderer;
        private int _lastTreeId;
        private GUIStyle _eventStyle;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
            CreateMarker();
        }

        private void Update()
        {
            if (_board == null && _controller != null && BoardField != null)
            {
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            }

            if (_camera == null)
            {
                GameObject cameraObject = GameObject.Find("Chain Combat Lab Camera");
                if (cameraObject != null) _camera = cameraObject.GetComponent<Camera>();
            }

            ChainReactionOpportunity reaction = _board?.PendingReaction;
            if (reaction == null)
            {
                if (_marker != null) _marker.SetActive(false);
                RestoreLastTree();
                return;
            }

            if (_marker != null)
            {
                _marker.SetActive(true);
                _marker.transform.position = new Vector3(reaction.Position.X, 0.18f, reaction.Position.Z);
                float pulse = 0.78f + Mathf.PingPong(Time.unscaledTime * 0.42f, 0.22f);
                _marker.transform.localScale = new Vector3(pulse, 0.06f, pulse);

                if (_markerRenderer != null)
                {
                    _markerRenderer.material.color = reaction.IsClaimed
                        ? new Color(0.30f, 0.72f, 1f, 0.85f)
                        : ImpactColor(reaction.ImpactForce);
                }
            }

            if (reaction.Kind == ChainReactionKind.TreeImpact && reaction.TreeId != 0)
            {
                StressTree(reaction.TreeId, reaction.ImpactForce);
            }
            else
            {
                RestoreLastTree();
            }
        }

        private void OnGUI()
        {
            ChainReactionOpportunity reaction = _board?.PendingReaction;
            if (reaction == null || _camera == null) return;

            EnsureStyle();
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(reaction.Position.X, 1.0f, reaction.Position.Z));
            if (screen.z <= 0f) return;

            string owner = reaction.IsClaimed ? $"  •  P{reaction.ClaimedByCommandGroup} CLAIMED" : "  •  UNCLAIMED";
            string text = $"{EventName(reaction.Kind)}  •  FORCE {reaction.ImpactForce} ({ChainCombatBoard.ForceWord(reaction.ImpactForce)}){owner}";
            GUI.Label(new Rect(screen.x - 150f, Screen.height - screen.y - 22f, 300f, 44f), text, _eventStyle);
        }

        private void CreateMarker()
        {
            _marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _marker.name = "Chain Physical Event Marker";
            _marker.transform.SetParent(transform, false);
            Collider collider = _marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            _markerRenderer = _marker.GetComponent<Renderer>();
            if (_markerRenderer != null) _markerRenderer.material.color = new Color(1f, 0.65f, 0.12f, 0.85f);
            _marker.SetActive(false);
        }

        private void StressTree(int treeId, int force)
        {
            if (_lastTreeId != 0 && _lastTreeId != treeId) RestoreLastTree();
            _lastTreeId = treeId;

            GameObject tree = GameObject.Find($"Chain Tree {treeId}");
            if (tree == null) return;

            float angle = Mathf.Clamp(3f + force * 1.4f, 5f, 16f);
            tree.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 8f) * angle);
        }

        private void RestoreLastTree()
        {
            if (_lastTreeId == 0) return;
            GameObject tree = GameObject.Find($"Chain Tree {_lastTreeId}");
            if (tree != null) tree.transform.rotation = Quaternion.identity;
            _lastTreeId = 0;
        }

        private void EnsureStyle()
        {
            if (_eventStyle != null) return;
            _eventStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
        }

        private static string EventName(ChainReactionKind kind)
        {
            switch (kind)
            {
                case ChainReactionKind.Airborne: return "AIRBORNE MOTION";
                case ChainReactionKind.Collision: return "COLLISION";
                case ChainReactionKind.TreeImpact: return "TREE IMPACT";
                default: return "PHYSICAL EVENT";
            }
        }

        private static Color ImpactColor(int force)
        {
            if (force <= 1) return new Color(0.92f, 0.82f, 0.25f, 0.75f);
            if (force <= 3) return new Color(1f, 0.62f, 0.12f, 0.82f);
            if (force <= 6) return new Color(1f, 0.32f, 0.10f, 0.88f);
            return new Color(0.92f, 0.10f, 0.10f, 0.92f);
        }
    }
}
