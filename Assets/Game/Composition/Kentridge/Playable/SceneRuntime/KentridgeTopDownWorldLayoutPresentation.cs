using System;
using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Lightweight scene evidence for the source-backed macro layout. This is intentionally a
    /// presentation of the production WorldBuilder result rather than a second map model.
    /// </summary>
    internal sealed class KentridgeTopDownWorldLayoutPresentation : MonoBehaviour
    {
        private const float PanelWidth = 390f;
        private const float PanelHeight = 270f;
        private const float PlotPadding = 24f;

        private TopDownWorldLayout _layout;

        public TopDownWorldLayout Layout => _layout;

        public static void Ensure(uint seed)
        {
            KentridgeTopDownWorldLayoutPresentation presentation =
                UnityEngine.Object.FindFirstObjectByType<KentridgeTopDownWorldLayoutPresentation>();
            if (presentation == null)
            {
                var host = new GameObject("Kentridge Top-Down World Layout");
                Scene target = SceneManager.GetSceneByName("KentridgePlayableSlice");
                if (target.IsValid() && target.isLoaded)
                    SceneManager.MoveGameObjectToScene(host, target);
                presentation = host.AddComponent<KentridgeTopDownWorldLayoutPresentation>();
            }

            presentation.Initialize(seed);
        }

        private void Initialize(uint seed)
        {
            _layout = KentridgeTopDownWorldLayout.Build(seed);

            var summary = new StringBuilder(512);
            summary.Append("KENTRIDGE_WORLD_LAYOUT source=legacy-warps root=")
                .Append(_layout.RootId)
                .Append(" nodes=").Append(_layout.Nodes.Count)
                .Append(" routes=").Append(_layout.Routes.Count);
            for (var i = 0; i < _layout.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement node = _layout.Nodes[i];
                if (node.Node.Kind != TopDownWorldNodeKind.Settlement
                    && node.Node.Kind != TopDownWorldNodeKind.Landmark)
                    continue;
                summary.Append(" | ").Append(node.Node.Id).Append('=').Append(node.Position);
            }
            Debug.Log(summary.ToString(), this);
        }

        private void OnGUI()
        {
            if (_layout == null || Event.current.type != EventType.Repaint)
                return;

            float left = Mathf.Max(8f, Screen.width - PanelWidth - 12f);
            float top = 12f;
            var panel = new Rect(left, top, PanelWidth, PanelHeight);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.86f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 12f, top + 7f, PanelWidth - 24f, 22f),
                "SOURCE-BACKED WORLD LAYOUT — KENTRIDGE");

            Rect plot = new Rect(
                left + PlotPadding,
                top + 34f,
                PanelWidth - PlotPadding * 2f,
                PanelHeight - 48f);

            GetBounds(out int minX, out int maxX, out int minY, out int maxY);
            for (var i = 0; i < _layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = _layout.Routes[i];
                if (!_layout.TryGetPosition(route.FromId, out TopDownWorldGridPoint from)
                    || !_layout.TryGetPosition(route.ToId, out TopDownWorldGridPoint to))
                    continue;

                DrawLine(
                    Map(from, plot, minX, maxX, minY, maxY),
                    Map(to, plot, minX, maxX, minY, maxY),
                    2f,
                    new Color(0.6f, 0.66f, 0.72f, 0.78f));
            }

            for (var i = 0; i < _layout.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement placement = _layout.Nodes[i];
                Vector2 point = Map(placement.Position, plot, minX, maxX, minY, maxY);
                bool major = placement.Node.Kind == TopDownWorldNodeKind.Settlement
                    || placement.Node.Kind == TopDownWorldNodeKind.Landmark;
                float size = major ? 8f : 5f;
                GUI.color = major ? new Color(1f, 0.88f, 0.45f, 1f) : new Color(0.75f, 0.82f, 0.9f, 1f);
                GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size),
                    Texture2D.whiteTexture);

                if (major)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(point.x + 5f, point.y - 9f, 105f, 18f), placement.Node.DisplayName);
                }
            }

            GUI.color = new Color(0.78f, 0.82f, 0.86f, 1f);
            GUI.Label(new Rect(left + 12f, top + PanelHeight - 21f, PanelWidth - 24f, 18f),
                "Routes: verified legacy traversal • spacing: inferred composition");
            GUI.color = previousColor;
        }

        private void GetBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;
            for (var i = 0; i < _layout.Nodes.Count; i++)
            {
                TopDownWorldGridPoint point = _layout.Nodes[i].Position;
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        private static Vector2 Map(
            TopDownWorldGridPoint point,
            Rect plot,
            int minX,
            int maxX,
            int minY,
            int maxY)
        {
            float x = maxX == minX ? 0.5f : (point.X - minX) / (float)(maxX - minX);
            float y = maxY == minY ? 0.5f : (maxY - point.Y) / (float)(maxY - minY);
            return new Vector2(
                Mathf.Lerp(plot.xMin, plot.xMax - 100f, x),
                Mathf.Lerp(plot.yMin, plot.yMax, y));
        }

        private static void DrawLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width),
                Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
    }
}
