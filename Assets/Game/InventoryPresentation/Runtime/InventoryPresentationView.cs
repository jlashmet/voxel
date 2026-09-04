using System;
using Game.Input.Api;
using Game.Inventory.Api;
using Game.InventoryPresentation.Api;
using UnityEngine;

namespace Game.InventoryPresentation.Runtime
{
    /// <summary>
    /// Production inventory realization. It renders only presenter projections and edits only presenter-local
    /// selection/filter/sort state; authoritative quantities remain owned by Inventory/Loot.
    /// </summary>
    public sealed class InventoryPresentationView : MonoBehaviour
    {
        private InventoryPresenter _presenter;
        private IInputContextLease _uiLease;
        private Texture2D _stone;
        private Texture2D _stoneLight;
        private Texture2D _parchment;
        private Texture2D _parchmentLight;
        private Texture2D _gold;
        private Texture2D _ink;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _quantityStyle;
        private GUIStyle _rowButtonStyle;
        private GUIStyle _selectedRowButtonStyle;
        private GUIStyle _filterStyle;
        private GUIStyle _sortButtonStyle;
        private GUIStyle _pendingStyle;

        public bool IsBound => _presenter != null;

        public void Bind(InventoryPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (ReferenceEquals(_presenter, presenter) && _uiLease != null) return;
            Unbind();
            _presenter = presenter;
            AcquireUiLease();
            enabled = true;
        }

        public void Unbind()
        {
            ReleaseUiLease();
            _presenter = null;
        }

        private void OnEnable()
        {
            if (_presenter != null) AcquireUiLease();
        }

        private void OnDisable() => ReleaseUiLease();

        private void OnDestroy()
        {
            ReleaseUiLease();
            DestroyTexture(ref _stone);
            DestroyTexture(ref _stoneLight);
            DestroyTexture(ref _parchment);
            DestroyTexture(ref _parchmentLight);
            DestroyTexture(ref _gold);
            DestroyTexture(ref _ink);
        }

        private void OnGUI()
        {
            if (_presenter == null) return;
            EnsureStyles();

            InventoryPresentationSnapshot snapshot = _presenter.Capture();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), 0.72f, 1.25f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            float frameWidth = Mathf.Min(1320f, width - 70f);
            float frameHeight = Mathf.Min(790f, height - 64f);
            Rect frame = new Rect((width - frameWidth) * 0.5f, (height - frameHeight) * 0.5f, frameWidth, frameHeight);
            DrawTexture(frame, _stone);
            DrawBevel(frame, 8f);

            Rect parchment = Inset(frame, 18f);
            DrawTexture(parchment, _parchment);
            DrawInnerBorder(parchment, 3f);

            Rect banner = new Rect(parchment.x + 22f, parchment.y + 18f, parchment.width - 44f, 82f);
            DrawTexture(banner, _stoneLight);
            DrawGoldRule(new Rect(banner.x, banner.yMax - 5f, banner.width, 5f));
            GUI.Label(new Rect(banner.x + 28f, banner.y + 13f, 520f, 38f), "INVENTORY", _titleStyle);
            GUI.Label(new Rect(banner.x + 30f, banner.y + 49f, 690f, 24f), "Authoritative stores • local presentation state", _subtitleStyle);
            GUI.Label(new Rect(banner.xMax - 320f, banner.y + 28f, 286f, 30f), "INPUT  UI", _pendingStyle);

            float contentTop = banner.yMax + 22f;
            float footerHeight = 86f;
            float panelGap = 20f;
            int panelCount = Mathf.Max(1, snapshot.Panels.Count);
            float availableWidth = parchment.width - 44f - panelGap * (panelCount - 1);
            float panelWidth = availableWidth / panelCount;
            float panelHeight = parchment.yMax - footerHeight - contentTop - 22f;

            for (var i = 0; i < snapshot.Panels.Count; i++)
            {
                Rect panelRect = new Rect(parchment.x + 22f + i * (panelWidth + panelGap), contentTop, panelWidth, panelHeight);
                DrawPanel(snapshot.Panels[i], panelRect);
            }

            Rect footer = new Rect(parchment.x + 22f, parchment.yMax - footerHeight, parchment.width - 44f, footerHeight - 18f);
            DrawOperations(snapshot, footer);
            GUI.matrix = previous;
        }

        private void DrawPanel(InventoryPanelPresentation panel, Rect rect)
        {
            DrawTexture(rect, _parchmentLight);
            DrawStoneBorder(rect, 5f);

            string kind = string.IsNullOrWhiteSpace(panel.BindingKind) ? "INVENTORY" : panel.BindingKind.ToUpperInvariant();
            GUI.Label(new Rect(rect.x + 22f, rect.y + 18f, rect.width - 44f, 32f), kind, _panelTitleStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 51f, rect.width - 44f, 24f), panel.StableOwnerId, _mutedStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 76f, rect.width - 44f, 22f), "Revision " + panel.Revision, _mutedStyle);

            Rect filterRect = new Rect(rect.x + 22f, rect.y + 108f, rect.width - 44f, 34f);
            DrawTexture(filterRect, _parchment);
            string nextFilter = GUI.TextField(Inset(filterRect, 5f), panel.Filter ?? string.Empty, _filterStyle);
            if (!string.Equals(nextFilter, panel.Filter, StringComparison.Ordinal))
                _presenter.SetFilter(panel.InventoryId, nextFilter);

            float buttonWidth = (rect.width - 52f) / 3f;
            float sortY = rect.y + 151f;
            if (GUI.Button(new Rect(rect.x + 22f, sortY, buttonWidth, 28f), "NAME", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.DisplayName);
            if (GUI.Button(new Rect(rect.x + 26f + buttonWidth, sortY, buttonWidth, 28f), "ITEM", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.ItemId);
            if (GUI.Button(new Rect(rect.x + 30f + buttonWidth * 2f, sortY, buttonWidth, 28f), "QTY", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.Quantity);

            float rowY = rect.y + 196f;
            float rowHeight = 58f;
            int visibleCapacity = Mathf.Max(1, Mathf.FloorToInt((rect.yMax - rowY - 18f) / rowHeight));
            if (panel.Rows.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 28f, rowY + 18f, rect.width - 56f, 28f), "No items match this view.", _mutedStyle);
                return;
            }

            int count = Mathf.Min(visibleCapacity, panel.Rows.Count);
            for (var i = 0; i < count; i++)
            {
                InventoryRowPresentation row = panel.Rows[i];
                Rect rowRect = new Rect(rect.x + 20f, rowY + i * rowHeight, rect.width - 40f, rowHeight - 8f);
                bool selected = panel.HasSelection && panel.Selection == row.Key;
                GUIStyle style = selected ? _selectedRowButtonStyle : _rowButtonStyle;
                if (GUI.Button(rowRect, GUIContent.none, style)) _presenter.Select(row.Key);
                GUI.Label(new Rect(rowRect.x + 16f, rowRect.y + 10f, 36f, 30f), row.IconText, _panelTitleStyle);
                GUI.Label(new Rect(rowRect.x + 58f, rowRect.y + 8f, rowRect.width - 160f, 24f), row.DisplayName, _bodyStyle);
                GUI.Label(new Rect(rowRect.x + 58f, rowRect.y + 29f, rowRect.width - 160f, 18f), row.Key.Item.Id, _mutedStyle);
                GUI.Label(new Rect(rowRect.xMax - 92f, rowRect.y + 13f, 72f, 28f), "× " + row.Quantity, _quantityStyle);
            }
        }

        private void DrawOperations(InventoryPresentationSnapshot snapshot, Rect rect)
        {
            DrawTexture(rect, _stoneLight);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 8f, 180f, 25f), "TRANSACTIONS", _subtitleStyle);
            if (snapshot.Operations.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 190f, rect.y + 8f, rect.width - 210f, 25f), "No pending inventory action", _pendingStyle);
                return;
            }

            float x = rect.x + 190f;
            for (var i = 0; i < snapshot.Operations.Count; i++)
            {
                PendingOperationPresentation operation = snapshot.Operations[i];
                string label = operation.Kind + "  •  " + operation.Status + "  •  " + operation.Item.Id + " ×" + operation.Quantity;
                if (!string.IsNullOrWhiteSpace(operation.Error)) label += "  •  " + operation.Error;
                GUI.Label(new Rect(x, rect.y + 8f + i * 24f, rect.width - 210f, 24f), label, _pendingStyle);
            }
        }

        private void ToggleSort(InventoryPanelPresentation panel, InventorySortMode mode)
        {
            bool ascending = panel.SortMode == mode ? !panel.SortAscending : true;
            _presenter.SetSort(panel.InventoryId, mode, ascending);
        }

        private void AcquireUiLease()
        {
            if (_presenter != null && _uiLease == null) _uiLease = _presenter.OpenUi();
        }

        private void ReleaseUiLease()
        {
            if (_uiLease == null) return;
            _uiLease.Dispose();
            _uiLease = null;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _stone = Solid(new Color32(55, 57, 52, 255), "Inventory Stone");
            _stoneLight = Solid(new Color32(77, 72, 61, 255), "Inventory Stone Light");
            _parchment = Solid(new Color32(221, 203, 159, 255), "Inventory Parchment");
            _parchmentLight = Solid(new Color32(238, 223, 184, 255), "Inventory Parchment Light");
            _gold = Solid(new Color32(181, 132, 50, 255), "Inventory Gold");
            _ink = Solid(new Color32(58, 43, 31, 255), "Inventory Ink");

            _titleStyle = LabelStyle(30, FontStyle.Bold, new Color32(244, 227, 184, 255), TextAnchor.MiddleLeft);
            _subtitleStyle = LabelStyle(15, FontStyle.Normal, new Color32(225, 209, 173, 255), TextAnchor.MiddleLeft);
            _panelTitleStyle = LabelStyle(19, FontStyle.Bold, new Color32(67, 47, 31, 255), TextAnchor.MiddleLeft);
            _bodyStyle = LabelStyle(17, FontStyle.Bold, new Color32(62, 45, 32, 255), TextAnchor.MiddleLeft);
            _mutedStyle = LabelStyle(12, FontStyle.Normal, new Color32(107, 82, 57, 255), TextAnchor.MiddleLeft);
            _quantityStyle = LabelStyle(18, FontStyle.Bold, new Color32(75, 51, 29, 255), TextAnchor.MiddleRight);
            _pendingStyle = LabelStyle(14, FontStyle.Bold, new Color32(239, 219, 171, 255), TextAnchor.MiddleLeft);

            _rowButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _parchment, textColor = Color.clear },
                hover = { background = _parchmentLight, textColor = Color.clear },
                active = { background = _gold, textColor = Color.clear },
                border = new RectOffset(4, 4, 4, 4)
            };
            _selectedRowButtonStyle = new GUIStyle(_rowButtonStyle)
            {
                normal = { background = _gold, textColor = Color.clear },
                hover = { background = _gold, textColor = Color.clear }
            };
            _filterStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = _parchment, textColor = new Color32(63, 45, 32, 255) },
                focused = { background = _parchmentLight, textColor = new Color32(63, 45, 32, 255) },
                padding = new RectOffset(10, 10, 4, 4)
            };
            _sortButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _stoneLight, textColor = new Color32(239, 219, 171, 255) },
                hover = { background = _stone, textColor = new Color32(255, 236, 184, 255) },
                active = { background = _gold, textColor = new Color32(55, 42, 28, 255) }
            };
        }

        private static GUIStyle LabelStyle(int fontSize, FontStyle fontStyle, Color color, TextAnchor anchor) =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = anchor,
                normal = { textColor = color },
                clipping = TextClipping.Clip
            };

        private void DrawBevel(Rect rect, float thickness)
        {
            DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _stoneLight);
            DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _stoneLight);
            DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _ink);
            DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _ink);
            float cap = 18f;
            DrawTexture(new Rect(rect.x + 8f, rect.y + 8f, cap, cap), _gold);
            DrawTexture(new Rect(rect.xMax - cap - 8f, rect.y + 8f, cap, cap), _gold);
            DrawTexture(new Rect(rect.x + 8f, rect.yMax - cap - 8f, cap, cap), _gold);
            DrawTexture(new Rect(rect.xMax - cap - 8f, rect.yMax - cap - 8f, cap, cap), _gold);
        }

        private void DrawStoneBorder(Rect rect, float thickness)
        {
            DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _stone);
            DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _stone);
            DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _stone);
            DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _stone);
        }

        private void DrawInnerBorder(Rect rect, float thickness)
        {
            DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _gold);
            DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _gold);
            DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _gold);
            DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _gold);
        }

        private void DrawGoldRule(Rect rect) => DrawTexture(rect, _gold);
        private static Rect Inset(Rect rect, float amount) => new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        private static void DrawTexture(Rect rect, Texture2D texture) => GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);

        private static Texture2D Solid(Color color, string name)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
            texture = null;
        }
    }
}
