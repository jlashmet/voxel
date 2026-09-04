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
        private Texture2D _veil;
        private Texture2D _shadow;
        private Texture2D _stone;
        private Texture2D _stoneLight;
        private Texture2D _parchment;
        private Texture2D _parchmentLight;
        private Texture2D _gold;
        private Texture2D _goldDark;
        private Texture2D _ink;
        private Texture2D _medallion;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _quantityStyle;
        private GUIStyle _iconStyle;
        private GUIStyle _rowButtonStyle;
        private GUIStyle _selectedRowButtonStyle;
        private GUIStyle _filterStyle;
        private GUIStyle _sortButtonStyle;
        private GUIStyle _activityStyle;

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
            DestroyTexture(ref _veil);
            DestroyTexture(ref _shadow);
            DestroyTexture(ref _stone);
            DestroyTexture(ref _stoneLight);
            DestroyTexture(ref _parchment);
            DestroyTexture(ref _parchmentLight);
            DestroyTexture(ref _gold);
            DestroyTexture(ref _goldDark);
            DestroyTexture(ref _ink);
            DestroyTexture(ref _medallion);
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

            DrawTexture(new Rect(0f, 0f, width, height), _veil);

            float frameWidth = Mathf.Min(1270f, width - 90f);
            float frameHeight = Mathf.Min(770f, height - 74f);
            Rect frame = new Rect((width - frameWidth) * 0.5f, (height - frameHeight) * 0.5f, frameWidth, frameHeight);
            DrawTexture(new Rect(frame.x + 13f, frame.y + 16f, frame.width, frame.height), _shadow);
            DrawTexture(frame, _stone);
            DrawBevel(frame, 8f);

            Rect parchment = Inset(frame, 18f);
            DrawTexture(parchment, _parchment);
            DrawInnerBorder(parchment, 3f);

            Rect banner = new Rect(parchment.x + 24f, parchment.y + 20f, parchment.width - 48f, 88f);
            DrawTexture(banner, _stoneLight);
            DrawGoldRule(new Rect(banner.x, banner.yMax - 5f, banner.width, 5f));
            DrawHeaderOrnament(banner);
            GUI.Label(new Rect(banner.x + 30f, banner.y + 12f, 520f, 40f), "INVENTORY", _titleStyle);
            GUI.Label(new Rect(banner.x + 32f, banner.y + 51f, 650f, 24f), "Your pack and nearby storage", _subtitleStyle);
            GUI.Label(new Rect(banner.xMax - 285f, banner.y + 29f, 245f, 28f), "TRAVELER'S LEDGER", _sectionStyle);

            float contentTop = banner.yMax + 22f;
            float footerHeight = 84f;
            float panelGap = 20f;
            int panelCount = Mathf.Max(1, snapshot.Panels.Count);
            float availableWidth = parchment.width - 48f - panelGap * (panelCount - 1);
            float panelWidth = availableWidth / panelCount;
            float panelHeight = parchment.yMax - footerHeight - contentTop - 22f;

            for (var i = 0; i < snapshot.Panels.Count; i++)
            {
                Rect panelRect = new Rect(parchment.x + 24f + i * (panelWidth + panelGap), contentTop, panelWidth, panelHeight);
                DrawPanel(snapshot.Panels[i], panelRect);
            }

            Rect footer = new Rect(parchment.x + 24f, parchment.yMax - footerHeight, parchment.width - 48f, footerHeight - 18f);
            DrawOperations(snapshot, footer);
            GUI.matrix = previous;
        }

        private void DrawPanel(InventoryPanelPresentation panel, Rect rect)
        {
            DrawTexture(rect, _parchmentLight);
            DrawStoneBorder(rect, 5f);
            DrawGoldRule(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 2f));

            GUI.Label(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, 32f), PanelTitle(panel), _panelTitleStyle);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 51f, rect.width - 48f, 22f), PanelSubtitle(panel), _mutedStyle);

            GUI.Label(new Rect(rect.x + 24f, rect.y + 83f, 110f, 20f), "SEARCH", _sectionStyle);
            Rect filterRect = new Rect(rect.x + 24f, rect.y + 105f, rect.width - 48f, 36f);
            DrawTexture(filterRect, _parchment);
            DrawThinBorder(filterRect, _goldDark, 2f);
            string nextFilter = GUI.TextField(Inset(filterRect, 5f), panel.Filter ?? string.Empty, _filterStyle);
            if (!string.Equals(nextFilter, panel.Filter, StringComparison.Ordinal))
                _presenter.SetFilter(panel.InventoryId, nextFilter);

            float buttonWidth = (rect.width - 56f) / 3f;
            float sortY = rect.y + 151f;
            if (GUI.Button(new Rect(rect.x + 24f, sortY, buttonWidth, 29f), "Name", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.DisplayName);
            if (GUI.Button(new Rect(rect.x + 28f + buttonWidth, sortY, buttonWidth, 29f), "Type", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.ItemId);
            if (GUI.Button(new Rect(rect.x + 32f + buttonWidth * 2f, sortY, buttonWidth, 29f), "Count", _sortButtonStyle))
                ToggleSort(panel, InventorySortMode.Quantity);

            float rowY = rect.y + 194f;
            float rowHeight = 62f;
            int visibleCapacity = Mathf.Max(1, Mathf.FloorToInt((rect.yMax - rowY - 18f) / rowHeight));
            if (panel.Rows.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 30f, rowY + 22f, rect.width - 60f, 28f), "Nothing here matches your search.", _mutedStyle);
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

                if (selected) DrawTexture(new Rect(rowRect.x, rowRect.y, 5f, rowRect.height), _goldDark);
                Rect emblem = new Rect(rowRect.x + 14f, rowRect.y + 6f, 42f, 42f);
                GUI.DrawTexture(emblem, _medallion, ScaleMode.ScaleToFit, true);
                GUI.Label(emblem, row.IconText, _iconStyle);
                GUI.Label(new Rect(rowRect.x + 68f, rowRect.y + 8f, rowRect.width - 180f, 34f), row.DisplayName, _bodyStyle);
                GUI.Label(new Rect(rowRect.xMax - 103f, rowRect.y + 11f, 82f, 30f), "× " + row.Quantity, _quantityStyle);
            }
        }

        private void DrawOperations(InventoryPresentationSnapshot snapshot, Rect rect)
        {
            DrawTexture(rect, _stoneLight);
            DrawGoldRule(new Rect(rect.x, rect.y, rect.width, 2f));
            GUI.Label(new Rect(rect.x + 20f, rect.y + 9f, 145f, 24f), "ACTIVITY", _sectionStyle);

            if (snapshot.Operations.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 165f, rect.y + 8f, rect.width - 190f, 28f), "Your inventory is up to date.", _activityStyle);
                return;
            }

            for (var i = 0; i < snapshot.Operations.Count; i++)
            {
                PendingOperationPresentation operation = snapshot.Operations[i];
                string itemName = ResolveItemName(snapshot, operation.Item);
                string label = OperationLabel(operation, itemName);
                GUI.Label(new Rect(rect.x + 165f, rect.y + 7f + i * 25f, rect.width - 190f, 25f), label, _activityStyle);
            }
        }

        private static string PanelTitle(InventoryPanelPresentation panel)
        {
            if (string.Equals(panel.BindingKind, "character", StringComparison.OrdinalIgnoreCase)) return "ADVENTURER'S PACK";
            if (string.Equals(panel.BindingKind, "container", StringComparison.OrdinalIgnoreCase)) return "NEARBY STORAGE";
            return "INVENTORY";
        }

        private static string PanelSubtitle(InventoryPanelPresentation panel)
        {
            if (string.Equals(panel.BindingKind, "character", StringComparison.OrdinalIgnoreCase)) return "What you are carrying";
            if (string.Equals(panel.BindingKind, "container", StringComparison.OrdinalIgnoreCase)) return "Items within reach";
            return "Items available here";
        }

        private static string ResolveItemName(InventoryPresentationSnapshot snapshot, ItemRef item)
        {
            for (var p = 0; p < snapshot.Panels.Count; p++)
            {
                InventoryPanelPresentation panel = snapshot.Panels[p];
                for (var r = 0; r < panel.Rows.Count; r++)
                    if (panel.Rows[r].Key.Item == item) return panel.Rows[r].DisplayName;
            }
            return FriendlyItemName(item.Id);
        }

        private static string FriendlyItemName(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "item";
            int separator = id.LastIndexOf(':');
            string value = separator >= 0 && separator + 1 < id.Length ? id.Substring(separator + 1) : id;
            value = value.Replace('-', ' ').Replace('_', ' ').Trim();
            if (value.Length == 0) return "item";
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string OperationLabel(PendingOperationPresentation operation, string itemName)
        {
            string action;
            if (operation.Kind == PendingOperationKind.Drop)
            {
                action = operation.Status == PendingOperationStatus.Pending ? "Dropping" :
                    operation.Status == PendingOperationStatus.Succeeded ? "Dropped" : "Could not drop";
            }
            else
            {
                action = operation.Status == PendingOperationStatus.Pending ? "Moving" :
                    operation.Status == PendingOperationStatus.Succeeded ? "Moved" : "Could not move";
            }

            string suffix = operation.Status == PendingOperationStatus.Pending ? "…" :
                operation.Status == PendingOperationStatus.Rejected ? " — inventory changed" : string.Empty;
            return action + " " + itemName + " ×" + operation.Quantity + suffix;
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
            _veil = Solid(new Color32(19, 14, 18, 168), "Inventory Veil");
            _shadow = Solid(new Color32(12, 8, 7, 188), "Inventory Shadow");
            _stone = Grain(new Color32(52, 47, 40, 255), 17u, 8, "Inventory Dark Oak");
            _stoneLight = Grain(new Color32(73, 63, 49, 255), 31u, 9, "Inventory Carved Oak");
            _parchment = Grain(new Color32(207, 188, 142, 255), 53u, 7, "Inventory Parchment");
            _parchmentLight = Grain(new Color32(230, 211, 166, 255), 79u, 6, "Inventory Parchment Light");
            _gold = Grain(new Color32(184, 132, 45, 255), 97u, 8, "Inventory Brass");
            _goldDark = Solid(new Color32(128, 87, 29, 255), "Inventory Dark Brass");
            _ink = Solid(new Color32(45, 32, 24, 255), "Inventory Ink");
            _medallion = MedallionTexture();

            _titleStyle = LabelStyle(31, FontStyle.Bold, new Color32(247, 229, 181, 255), TextAnchor.MiddleLeft);
            _subtitleStyle = LabelStyle(15, FontStyle.Italic, new Color32(218, 196, 149, 255), TextAnchor.MiddleLeft);
            _panelTitleStyle = LabelStyle(20, FontStyle.Bold, new Color32(62, 42, 27, 255), TextAnchor.MiddleLeft);
            _sectionStyle = LabelStyle(12, FontStyle.Bold, new Color32(210, 177, 105, 255), TextAnchor.MiddleLeft);
            _bodyStyle = LabelStyle(18, FontStyle.Bold, new Color32(58, 39, 26, 255), TextAnchor.MiddleLeft);
            _mutedStyle = LabelStyle(13, FontStyle.Italic, new Color32(102, 75, 50, 255), TextAnchor.MiddleLeft);
            _quantityStyle = LabelStyle(19, FontStyle.Bold, new Color32(72, 47, 27, 255), TextAnchor.MiddleRight);
            _iconStyle = LabelStyle(18, FontStyle.Bold, new Color32(240, 215, 155, 255), TextAnchor.MiddleCenter);
            _activityStyle = LabelStyle(14, FontStyle.Bold, new Color32(239, 216, 164, 255), TextAnchor.MiddleLeft);

            _rowButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _parchment, textColor = Color.clear },
                hover = { background = _parchmentLight, textColor = Color.clear },
                active = { background = _gold, textColor = Color.clear },
                border = new RectOffset(4, 4, 4, 4)
            };
            _selectedRowButtonStyle = new GUIStyle(_rowButtonStyle)
            {
                normal = { background = _parchmentLight, textColor = Color.clear },
                hover = { background = _parchmentLight, textColor = Color.clear }
            };
            _filterStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = _parchment, textColor = new Color32(58, 39, 27, 255) },
                focused = { background = _parchmentLight, textColor = new Color32(58, 39, 27, 255) },
                padding = new RectOffset(10, 10, 4, 4)
            };
            _sortButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _stoneLight, textColor = new Color32(234, 209, 158, 255) },
                hover = { background = _stone, textColor = new Color32(255, 235, 188, 255) },
                active = { background = _gold, textColor = new Color32(51, 34, 23, 255) }
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

        private void DrawHeaderOrnament(Rect banner)
        {
            float y = banner.y + 28f;
            Rect line = new Rect(banner.xMax - 330f, y, 36f, 2f);
            DrawTexture(line, _gold);
            DrawTexture(new Rect(line.x + 42f, y - 4f, 10f, 10f), _goldDark);
            DrawTexture(new Rect(line.x + 58f, y, 26f, 2f), _gold);
        }

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

        private static void DrawThinBorder(Rect rect, Texture2D texture, float thickness)
        {
            DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), texture);
            DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), texture);
            DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), texture);
            DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), texture);
        }

        private void DrawGoldRule(Rect rect) => DrawTexture(rect, _gold);
        private static Rect Inset(Rect rect, float amount) => new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        private static void DrawTexture(Rect rect, Texture2D texture) => GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);

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

        private static Texture2D Grain(Color32 baseColor, uint seed, int variation, string name)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            uint state = seed;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    state = state * 1664525u + 1013904223u;
                    int noise = (int)((state >> 24) % (uint)(variation * 2 + 1)) - variation;
                    int fiber = ((x + y * 3) % 11 == 0) ? -2 : 0;
                    pixels[y * size + x] = new Color32(
                        Shift(baseColor.r, noise + fiber),
                        Shift(baseColor.g, noise + fiber),
                        Shift(baseColor.b, noise + fiber),
                        baseColor.a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MedallionTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Inventory Item Medallion",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 color;
                    if (distance > 0.96f) color = new Color32(0, 0, 0, 0);
                    else if (distance > 0.76f) color = new Color32(164, 113, 37, 255);
                    else if (distance > 0.67f) color = new Color32(222, 178, 84, 255);
                    else color = new Color32(67, 52, 39, 255);
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static byte Shift(byte value, int delta) => (byte)Mathf.Clamp(value + delta, 0, 255);

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
            texture = null;
        }
    }
}
