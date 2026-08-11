using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// Minimalist throttle bar — thin vertical rounded bar, no text.
    /// </summary>
    public class ThrottleHUD
    {
        private HUDManager _mgr;
        private ShipController _controller;

        private Image _barFill;
        private RectTransform _fillRect;
        private float _barHeight = 280f;
        private float _barWidth = 6f;

        public static ThrottleHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new ThrottleHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private static Sprite _roundedSprite;
        private static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            float r = 3f;
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - center.x) - (center.x - r));
                    float dy = Mathf.Max(0, Mathf.Abs(y - center.y) - (center.y - r));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color(1, 1, 1, alpha);
                }
            tex.SetPixels(px);
            tex.Apply();
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedSprite;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;

            // Background bar — thin, rounded, semi-transparent dark
            float panelW = _barWidth + 8f;
            var panel = new GameObject("ThrottleBar", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(1f, 0.5f);
            pRt.anchorMax = new Vector2(1f, 0.5f);
            pRt.pivot = new Vector2(1f, 0.5f);
            pRt.anchoredPosition = new Vector2(-12, 0);
            pRt.sizeDelta = new Vector2(panelW, _barHeight + 8f);
            var pImg = panel.GetComponent<Image>();
            pImg.sprite = GetRoundedSprite();
            pImg.type = Image.Type.Sliced;
            pImg.color = new Color(0.08f, 0.08f, 0.12f, 0.5f);
            pImg.raycastTarget = false;

            // Center line (zero mark) — very subtle
            var zeroMark = new GameObject("ZeroMark", typeof(RectTransform), typeof(Image));
            zeroMark.transform.SetParent(panel.transform, false);
            var zRt = zeroMark.GetComponent<RectTransform>();
            zRt.anchorMin = new Vector2(0f, 0.5f);
            zRt.anchorMax = new Vector2(1f, 0.5f);
            zRt.pivot = new Vector2(0.5f, 0.5f);
            zRt.anchoredPosition = Vector2.zero;
            zRt.sizeDelta = new Vector2(-2f, 1f);
            var zImg = zeroMark.GetComponent<Image>();
            zImg.color = new Color(0.5f, 0.5f, 0.55f, 0.4f);
            zImg.raycastTarget = false;

            // Fill bar
            var fillObj = new GameObject("EngineFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(panel.transform, false);
            _fillRect = fillObj.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            _fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            _fillRect.pivot = new Vector2(0.5f, 0.5f);
            _fillRect.anchoredPosition = Vector2.zero;
            _fillRect.sizeDelta = new Vector2(_barWidth, 0);
            _barFill = fillObj.GetComponent<Image>();
            _barFill.sprite = GetRoundedSprite();
            _barFill.type = Image.Type.Sliced;
            _barFill.raycastTarget = false;
        }

        public void UpdateHUD()
        {
            _controller = _mgr.controller;
            if (_controller == null) return;

            float power = _controller.enginePower;
            float absPower = Mathf.Abs(power);
            float halfHeight = _barHeight / 2f;
            float fillSize = absPower * halfHeight;

            if (power >= 0f)
            {
                _fillRect.pivot = new Vector2(0.5f, 0f);
                _fillRect.anchoredPosition = new Vector2(0, 4f);
                _fillRect.sizeDelta = new Vector2(_barWidth, fillSize);
                _barFill.color = new Color(0.2f, 0.9f, 0.4f, 0.85f);
            }
            else
            {
                _fillRect.pivot = new Vector2(0.5f, 1f);
                _fillRect.anchoredPosition = new Vector2(0, -4f);
                _fillRect.sizeDelta = new Vector2(_barWidth, fillSize);
                _barFill.color = new Color(0.9f, 0.3f, 0.2f, 0.85f);
            }
        }
    }
}
