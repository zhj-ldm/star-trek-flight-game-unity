using UnityEngine;
using UnityEngine.UI;

// StatusLCARS — auto-orbit button removed

namespace StarTrekCombat
{
    /// <summary>
    /// LCARS status panel — top-left corner.
    /// Orange border 3px on top/bottom/left. NO right border (open).
    /// Rounded corners on top-left and bottom-left only.
    /// Contains: ship name, impulse, warp, heading, camera,
    /// and bottom area with shield on/off dot, shield bar, hull bar.
    /// </summary>
    public class StatusLCARS
    {
        private HUDManager _mgr;
        private ShipController _controller;

        private Text _impulseText;
        private Text _warpText;
        private Text _headingText;
        private Text _cameraText;
        private Text _warpLevelText;

        private Image _shieldDot;
        private Image _shieldBar;
        private Image _hullBar;
        private Text _shieldText;
        private Text _hullText;

        private static readonly Color Orange = new Color(0.96f, 0.64f, 0.19f, 1f);
        private static readonly Color Grey = new Color(0.72f, 0.75f, 0.78f, 0.9f);
        private static readonly Color Yellow = new Color(0.95f, 0.85f, 0.3f, 1f);
        private static readonly Color LineColor = new Color(0.4f, 0.42f, 0.45f, 0.4f);
        private static readonly Color ShieldOnColor = new Color(0.3f, 0.6f, 1f, 0.9f);
        private static readonly Color ShieldOffColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        private static readonly Color HullColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        private static readonly Color ShieldColor = new Color(0.2f, 0.5f, 1f, 0.8f);

        private const float CornerRadius = 8f;
        private const float BorderWidth = 3f;

        // 3-sided rounded border sprite: left + top + bottom, NO right
        private static Sprite _leftOpenRightSprite;
        private static Sprite GetLeftOpenRightBorder()
        {
            if (_leftOpenRightSprite != null) return _leftOpenRightSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            float r = CornerRadius;
            float bw = BorderWidth;
            Vector2 center = new Vector2(ts / 2f, ts / 2f);

            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    bool hasColor = false;
                    bool inCornerX = x < r || x >= ts - r;
                    bool inCornerY = y < r || y >= ts - r;

                    if (inCornerX && inCornerY)
                    {
                        // Only left corners (top-left, bottom-left) are rounded
                        // Right corners have no border (open)
                        bool isLeftCorner = x < r;
                        if (isLeftCorner)
                        {
                            float cx = r;
                            float cy = y < ts / 2f ? r : ts - 1 - r;
                            float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                            hasColor = dist <= r && dist > r - bw;
                        }
                    }
                    else if (inCornerY)
                    {
                        // Top or bottom edge (full width including right edge open)
                        // Left part has border, right part doesn't
                        float minEdge = Mathf.Min(x, y, ts - 1 - x, ts - 1 - y);
                        // Only show border if on left half or top/bottom edge
                        // Top/bottom edges go full width
                        hasColor = minEdge < bw;
                    }
                    else if (inCornerX)
                    {
                        // Left or right edge
                        bool isLeft = x < r;
                        if (isLeft)
                            hasColor = x < bw;
                        // Right edge: no border (open)
                    }

                    px[y * ts + x] = hasColor ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _leftOpenRightSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _leftOpenRightSprite;
        }

        // 3-sided rounded border sprite: right + top + bottom, NO left
        private static Sprite _rightOpenLeftSprite;
        private static Sprite GetRightOpenLeftBorder()
        {
            if (_rightOpenLeftSprite != null) return _rightOpenLeftSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            float r = CornerRadius;
            float bw = BorderWidth;

            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    bool hasColor = false;
                    bool inCornerX = x < r || x >= ts - r;
                    bool inCornerY = y < r || y >= ts - r;

                    if (inCornerX && inCornerY)
                    {
                        bool isRightCorner = x >= ts - r;
                        if (isRightCorner)
                        {
                            float cx = ts - 1 - r;
                            float cy = y < ts / 2f ? r : ts - 1 - r;
                            float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                            hasColor = dist <= r && dist > r - bw;
                        }
                    }
                    else if (inCornerY)
                    {
                        float minEdge = Mathf.Min(x, y, ts - 1 - x, ts - 1 - y);
                        hasColor = minEdge < bw;
                    }
                    else if (inCornerX)
                    {
                        bool isRight = x >= ts - r;
                        if (isRight)
                            hasColor = x >= ts - bw;
                    }

                    px[y * ts + x] = hasColor ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _rightOpenLeftSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _rightOpenLeftSprite;
        }

        public static Sprite GetRightOpenLeftBorderSprite() => GetRightOpenLeftBorder();

        public static StatusLCARS Create(Transform parent, HUDManager mgr)
        {
            var hud = new StatusLCARS();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            float sz = 200f;

            var root = new GameObject("StatusLCARS", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0, 1);
            rootRt.anchorMax = new Vector2(0, 1);
            rootRt.pivot = new Vector2(0, 1);
            rootRt.anchoredPosition = new Vector2(10, -10);
            rootRt.sizeDelta = new Vector2(sz, sz);

            // Single rounded border: left + top + bottom, open on right
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(root.transform, false);
            var bRt = border.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            var bImg = border.GetComponent<Image>();
            bImg.sprite = GetLeftOpenRightBorder();
            bImg.type = Image.Type.Sliced;
            bImg.color = Orange;
            bImg.raycastTarget = false;

            // Content area
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(BorderWidth + 6, BorderWidth + 6);
            cRt.offsetMax = new Vector2(-6, -(BorderWidth + 6));

            float y = 0;

            var name = MakeText(content.transform, "ShipName", "USS ENTERPRISE-D", 15, Orange);
            PosTop(name.rectTransform, ref y, 22);

            MakeLine(content.transform, ref y);

            _impulseText = MakeText(content.transform, "Impulse", "IMPULSE:  All Stop", 13, Grey);
            PosTop(_impulseText.rectTransform, ref y, 18);

            _warpText = MakeText(content.transform, "Warp", "WARP:  Standby", 13, Grey);
            PosTop(_warpText.rectTransform, ref y, 18);

            // Warp speed level control — compact row with label, level text, up/down arrows
            var warpRow = new GameObject("WarpSpeedRow", typeof(RectTransform));
            warpRow.transform.SetParent(content.transform, false);
            var wrRt = warpRow.GetComponent<RectTransform>();
            wrRt.anchorMin = new Vector2(0, 1);
            wrRt.anchorMax = new Vector2(1, 1);
            wrRt.pivot = new Vector2(0, 1);
            wrRt.anchoredPosition = new Vector2(0, -y);
            wrRt.sizeDelta = new Vector2(0, 18);
            y += 20;

            var warpLabel = MakeText(warpRow.transform, "WarpSpdLabel", "曲速:", 13, Yellow);
            warpLabel.rectTransform.anchorMin = new Vector2(0, 0.5f);
            warpLabel.rectTransform.anchorMax = new Vector2(0, 0.5f);
            warpLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            warpLabel.rectTransform.anchoredPosition = new Vector2(0, 0);
            warpLabel.rectTransform.sizeDelta = new Vector2(50, 18);
            warpLabel.alignment = TextAnchor.MiddleLeft;

            _warpLevelText = MakeText(warpRow.transform, "WarpLevel", "1", 14, new Color(1f, 0.85f, 0.3f, 1f));
            _warpLevelText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            _warpLevelText.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _warpLevelText.rectTransform.pivot = new Vector2(0, 0.5f);
            _warpLevelText.rectTransform.anchoredPosition = new Vector2(52, 0);
            _warpLevelText.rectTransform.sizeDelta = new Vector2(24, 18);
            _warpLevelText.alignment = TextAnchor.MiddleLeft;
            _warpLevelText.raycastTarget = false;

            // Down arrow button
            var downBtn = new GameObject("WarpDown", typeof(RectTransform), typeof(Image), typeof(Button));
            downBtn.transform.SetParent(warpRow.transform, false);
            var dRt = downBtn.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(1, 0.5f);
            dRt.anchorMax = new Vector2(1, 0.5f);
            dRt.pivot = new Vector2(1, 0.5f);
            dRt.anchoredPosition = new Vector2(-24, 0);
            dRt.sizeDelta = new Vector2(20, 18);
            var dImg = downBtn.GetComponent<Image>();
            dImg.sprite = HUDManager.GetFilledRoundedSprite(3f);
            dImg.type = Image.Type.Sliced;
            dImg.color = new Color(0.15f, 0.2f, 0.35f, 0.8f);
            var dLabel = MakeText(downBtn.transform, "Lbl", "▼", 10, new Color(0.7f, 0.8f, 1f, 0.9f), TextAnchor.MiddleCenter);
            dLabel.rectTransform.anchorMin = Vector2.zero;
            dLabel.rectTransform.anchorMax = Vector2.one;
            dLabel.rectTransform.offsetMin = Vector2.zero;
            dLabel.rectTransform.offsetMax = Vector2.zero;
            dLabel.raycastTarget = false;
            downBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var c = _mgr.controller;
                if (c != null)
                    c.SetGalaxyWarpLevel(c.GalaxyWarpLevel - 1);
            });

            // Up arrow button
            var upBtn = new GameObject("WarpUp", typeof(RectTransform), typeof(Image), typeof(Button));
            upBtn.transform.SetParent(warpRow.transform, false);
            var uRt = upBtn.GetComponent<RectTransform>();
            uRt.anchorMin = new Vector2(1, 0.5f);
            uRt.anchorMax = new Vector2(1, 0.5f);
            uRt.pivot = new Vector2(1, 0.5f);
            uRt.anchoredPosition = new Vector2(0, 0);
            uRt.sizeDelta = new Vector2(20, 18);
            var uImg = upBtn.GetComponent<Image>();
            uImg.sprite = HUDManager.GetFilledRoundedSprite(3f);
            uImg.type = Image.Type.Sliced;
            uImg.color = new Color(0.15f, 0.2f, 0.35f, 0.8f);
            var uLabel = MakeText(upBtn.transform, "Lbl", "▲", 10, new Color(0.7f, 0.8f, 1f, 0.9f), TextAnchor.MiddleCenter);
            uLabel.rectTransform.anchorMin = Vector2.zero;
            uLabel.rectTransform.anchorMax = Vector2.one;
            uLabel.rectTransform.offsetMin = Vector2.zero;
            uLabel.rectTransform.offsetMax = Vector2.zero;
            uLabel.raycastTarget = false;
            upBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var c = _mgr.controller;
                if (c != null)
                    c.SetGalaxyWarpLevel(c.GalaxyWarpLevel + 1);
            });

            _headingText = MakeText(content.transform, "Heading", "HEADING:  0° / 0° / 0°", 13, Yellow);
            PosTop(_headingText.rectTransform, ref y, 18);

            _cameraText = MakeText(content.transform, "Camera", "CAMERA:  EXTERNAL", 13, Orange);
            PosTop(_cameraText.rectTransform, ref y, 18);

            MakeLine(content.transform, ref y);

            // Shield / Hull status area (bottom)

            _shieldDot = CreateSmallImage(content.transform, "ShieldDot", ShieldOnColor,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -y - 4), new Vector2(12, -y - 14));

            var shLabel = MakeText(content.transform, "ShieldLabel", "护盾", 11, Grey);
            shLabel.rectTransform.anchorMin = new Vector2(0, 1);
            shLabel.rectTransform.anchorMax = new Vector2(0, 1);
            shLabel.rectTransform.pivot = new Vector2(0, 1);
            shLabel.rectTransform.anchoredPosition = new Vector2(16, -y);
            shLabel.rectTransform.sizeDelta = new Vector2(60, 12);
            y += 16;

            _shieldBar = CreateProgressBar(content.transform, "ShieldBar", ShieldColor, y);
            _shieldText = MakeText(content.transform, "ShieldPct", "100%", 11, Grey);
            _shieldText.rectTransform.anchorMin = new Vector2(1, 1);
            _shieldText.rectTransform.anchorMax = new Vector2(1, 1);
            _shieldText.rectTransform.pivot = new Vector2(1, 1);
            _shieldText.rectTransform.anchoredPosition = new Vector2(-2, -y + 1);
            _shieldText.rectTransform.sizeDelta = new Vector2(40, 10);
            _shieldText.alignment = TextAnchor.UpperRight;
            y += 10;

            _hullBar = CreateProgressBar(content.transform, "HullBar", HullColor, y);
            _hullText = MakeText(content.transform, "HullPct", "100%", 11, Grey);
            _hullText.rectTransform.anchorMin = new Vector2(1, 1);
            _hullText.rectTransform.anchorMax = new Vector2(1, 1);
            _hullText.rectTransform.pivot = new Vector2(1, 1);
            _hullText.rectTransform.anchoredPosition = new Vector2(-2, -y + 1);
            _hullText.rectTransform.sizeDelta = new Vector2(40, 10);
            _hullText.alignment = TextAnchor.UpperRight;
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }

        private Image CreateSmallImage(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Image CreateProgressBar(Transform parent, string name, Color fillColor, float yOffset)
        {
            var bg = new GameObject(name + "_BG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 1);
            bgRt.anchorMax = new Vector2(1, 1);
            bgRt.pivot = new Vector2(0, 1);
            bgRt.anchoredPosition = new Vector2(2, -yOffset);
            bgRt.sizeDelta = new Vector2(-44, 6);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            bgImg.raycastTarget = false;

            var fill = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = GetWhiteSprite();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        private Text MakeText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var t = obj.GetComponent<Text>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.font = HUDManager.UIFont;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private void PosTop(RectTransform rt, ref float y, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(0, h);
            y += h + 2;
        }

        private void MakeLine(Transform parent, ref float y)
        {
            var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(parent, false);
            var rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(0, 1);
            line.GetComponent<Image>().color = LineColor;
            line.GetComponent<Image>().raycastTarget = false;
            y += 6;
        }

        public void UpdateHUD()
        {
            _controller = _mgr.controller;
            if (_controller == null) return;

            float spd = _controller.currentSpeed;
            string spdStr = spd >= 1000f ? $"{spd / 1000f:F1} km/s" : $"{spd:F0} m/s";
            string impState = Mathf.Abs(_controller.enginePower) < 0.01f ? "All Stop" :
                              _controller.enginePower > 0 ? "Forward" : "Reverse";
            _impulseText.text = $"IMPULSE:  {impState} ({spdStr})";

            if (_controller.IsGalaxyWarping)
            {
                float ws = _controller.GalaxyWarpSpeed;
                string wsStr = ws >= 1000f ? $"{ws / 1000f:F0} km/s" : $"{ws:F0} m/s";
                _warpText.text = $"WARP { _controller.GalaxyWarpLevel}:  {wsStr}";
            }
            else if (_controller.IsWarpZooming) _warpText.text = "WARP:  CHARGING";
            else if (_controller.IsWarpExitZooming) _warpText.text = "WARP:  DISENGAGING";
            else _warpText.text = $"WARP L{_controller.GalaxyWarpLevel}:  Standby";

            _warpLevelText.text = _controller.GalaxyWarpLevel.ToString();

            var a = _controller.transform.eulerAngles;
            _headingText.text = $"HEADING:  {a.x:F0}° / {a.y:F0}° / {a.z:F0}°";
            _cameraText.text = "CAMERA:  EXTERNAL";

            var health = _mgr.health;
            if (health == null && _controller != null)
                health = _controller.health ?? _controller.GetComponent<ShipHealth>();
            if (health != null)
            {
                float hp = health.HullPercent;
                float sp = health.ShieldPercent;
                _hullBar.fillAmount = hp;
                _shieldBar.fillAmount = sp;
                _hullText.text = $"{hp * 100f:F0}%";
                _shieldText.text = $"{sp * 100f:F0}%";

                bool shieldOn = health.isShieldOn && health.IsShieldActive;
                _shieldDot.color = shieldOn ? ShieldOnColor : ShieldOffColor;

                _hullText.color = hp < 0.25f ? new Color(1f, 0.3f, 0.2f, 1f) : Grey;
                _shieldText.color = sp < 0.25f && sp > 0f ? new Color(1f, 0.5f, 0.3f, 1f) : Grey;
            }
        }
    }
}
