using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// LCARS navigation panel — top-right corner.
    /// Rounded border, transparent inner. Compact radar with click-to-expand.
    /// Small size = 90px radar, large size = 320px radar.
    /// </summary>
    public class NavLCARS
    {
        private HUDManager _mgr;
        private ShipController _controller;

        private RectTransform _blipContainer;
        private RectTransform _radarRect;
        private RectTransform _rootRt;
        private GameObject _playerIcon;
        private Text _titleText;

        private bool _expanded;

        private const float SmallRadarSize = 90f;
        private const float LargeRadarSize = 320f;
        private const float SmallRange = 1000f;
        private const float LargeRange = 8000f;

        private float CurrentRadarSize => _expanded ? LargeRadarSize : SmallRadarSize;
        private float CurrentRadarRange => _expanded ? LargeRange : SmallRange;

        private static readonly Color Blue = new Color(0.55f, 0.68f, 0.82f, 0.9f);
        private static readonly Color Orange = new Color(0.96f, 0.64f, 0.19f, 1f);
        private static readonly Color Red = new Color(1f, 0.25f, 0.15f, 0.9f);

        public static NavLCARS Create(Transform parent, HUDManager mgr)
        {
            var hud = new NavLCARS();
            hud.Init(parent, mgr);
            return hud;
        }

        private static Sprite _triangleSprite;
        private static Sprite GetTriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ny = (float)y / (size - 1);
                    float nx = (float)x / (size - 1);
                    float halfWidth = (1f - ny) * 0.5f;
                    bool inside = ny < 0.9f && nx > (0.5f - halfWidth) && nx < (0.5f + halfWidth);
                    px[y * size + x] = inside ? Color.white : new Color(0, 0, 0, 0);
                }
            tex.SetPixels(px);
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _triangleSprite;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            float borderW = 3f;
            float sz = SmallRadarSize + 16;
            float h = sz + 24;

            var root = new GameObject("NavLCARS", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            _rootRt = root.GetComponent<RectTransform>();
            _rootRt.anchorMin = new Vector2(1, 1);
            _rootRt.anchorMax = new Vector2(1, 1);
            _rootRt.pivot = new Vector2(1, 1);
            _rootRt.anchoredPosition = new Vector2(-10, -10);
            _rootRt.sizeDelta = new Vector2(sz, h);

            // Transparent background (for click detection)
            var rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0.01f); // nearly transparent but clickable

            // Click to toggle expand
            var btn = root.GetComponent<Button>();
            btn.onClick.AddListener(ToggleExpand);

            // Rounded border
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(root.transform, false);
            var bRt = border.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            var bImg = border.GetComponent<Image>();
            bImg.sprite = HUDManager.GetRoundedBorderSprite(8f, borderW);
            bImg.type = Image.Type.Sliced;
            bImg.color = Blue;
            bImg.raycastTarget = false;

            // Title
            var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
            title.transform.SetParent(root.transform, false);
            var tRt = title.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0, 1);
            tRt.anchorMax = new Vector2(1, 1);
            tRt.pivot = new Vector2(0.5f, 1);
            tRt.anchoredPosition = new Vector2(0, -4);
            tRt.sizeDelta = new Vector2(-8, 16);
            _titleText = title.GetComponent<Text>();
            _titleText.text = "NAVIGATION [M]";
            _titleText.fontSize = 11;
            _titleText.color = Blue;
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.alignment = TextAnchor.UpperCenter;
            _titleText.raycastTarget = false;

            // Radar viewport
            var radarGo = new GameObject("RadarViewport", typeof(RectTransform));
            radarGo.transform.SetParent(root.transform, false);
            _radarRect = radarGo.GetComponent<RectTransform>();
            _radarRect.anchorMin = new Vector2(0.5f, 0.5f);
            _radarRect.anchorMax = new Vector2(0.5f, 0.5f);
            _radarRect.pivot = new Vector2(0.5f, 0.5f);
            _radarRect.anchoredPosition = new Vector2(0, -8);
            _radarRect.sizeDelta = new Vector2(SmallRadarSize, SmallRadarSize);

            // Cross lines
            MakeRadarLine(_radarRect, true, Blue, 0.06f);
            MakeRadarLine(_radarRect, false, Blue, 0.06f);

            // Player icon — orange triangle
            _playerIcon = new GameObject("PlayerIcon", typeof(RectTransform), typeof(Image));
            _playerIcon.transform.SetParent(_radarRect, false);
            var pRt = _playerIcon.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = Vector2.zero;
            pRt.sizeDelta = new Vector2(10, 10);
            var pImg = _playerIcon.GetComponent<Image>();
            pImg.sprite = GetTriangleSprite();
            pImg.color = Orange;
            pImg.raycastTarget = false;

            // Blip container
            _blipContainer = new GameObject("Blips", typeof(RectTransform)).GetComponent<RectTransform>();
            _blipContainer.SetParent(_radarRect, false);
            _blipContainer.anchorMin = Vector2.zero;
            _blipContainer.anchorMax = Vector2.one;
            _blipContainer.offsetMin = Vector2.zero;
            _blipContainer.offsetMax = Vector2.zero;
        }

        private void ToggleExpand()
        {
            _expanded = !_expanded;
            float radarSize = CurrentRadarSize;
            float sz = radarSize + 16;
            float h = sz + 24;

            _rootRt.sizeDelta = new Vector2(sz, h);
            _radarRect.sizeDelta = new Vector2(radarSize, radarSize);

            // Scale player icon
            float iconScale = _expanded ? 1.5f : 1f;
            _playerIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(10 * iconScale, 10 * iconScale);

            // Update title
            _titleText.text = _expanded ? "NAVIGATION [点击缩小]" : "NAVIGATION [M]";
        }

        private void MakeRadarLine(Transform parent, bool horizontal, Color color, float alpha)
        {
            var line = new GameObject(horizontal ? "HLine" : "VLine", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(parent, false);
            var rt = line.GetComponent<RectTransform>();
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(1, 0.5f);
                rt.sizeDelta = new Vector2(0, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(1, 0);
            }
            line.GetComponent<Image>().color = new Color(color.r, color.g, color.b, alpha);
            line.GetComponent<Image>().raycastTarget = false;
        }

        public void UpdateHUD()
        {
            _controller = _mgr.controller;
            if (_controller == null) return;

            for (int i = _blipContainer.childCount - 1; i >= 0; i--)
                if (_blipContainer.GetChild(i) != null) Object.Destroy(_blipContainer.GetChild(i).gameObject);

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            Vector3 playerPos = _controller.transform.position;
            Vector3 fwd = _controller.transform.forward;
            Vector3 right = _controller.transform.right;
            float half = CurrentRadarSize / 2f;
            float range = CurrentRadarRange;
            float blipSize = _expanded ? 8f : 6f;

            foreach (var go in enemies)
            {
                if (go == null) continue;
                var health = go.GetComponent<ShipHealth>();
                if (health == null || !health.IsAlive) continue;

                Vector3 toEnemy = go.transform.position - playerPos;
                float dist = toEnemy.magnitude;
                if (dist > range) continue;

                float relX = Vector3.Dot(toEnemy, right);
                float relZ = Vector3.Dot(toEnemy, fwd);
                float norm = dist / range;
                float bx = (relX / dist) * norm * half;
                float by = (relZ / dist) * norm * half;

                var blip = new GameObject("Blip", typeof(RectTransform), typeof(Image));
                blip.transform.SetParent(_blipContainer, false);
                var bRt = blip.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0.5f, 0.5f);
                bRt.anchorMax = new Vector2(0.5f, 0.5f);
                bRt.pivot = new Vector2(0.5f, 0.5f);
                bRt.anchoredPosition = new Vector2(bx, by);
                bRt.sizeDelta = new Vector2(blipSize, blipSize);
                var bImg = blip.GetComponent<Image>();
                bImg.sprite = GetTriangleSprite();
                bImg.color = Red;
                bImg.raycastTarget = false;
            }
        }
    }
}
