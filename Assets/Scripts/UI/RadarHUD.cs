using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Top-left HUD: circular radar (compact) or galaxy map (expanded on click).
    /// Compact: ships as triangles with heading direction.
    /// Expanded: full-screen galaxy map with all planets, enemy dots, and click-to-set-warp.
    /// </summary>
    public class RadarHUD
    {
        private HUDManager _mgr;
        private GameObject _panel;
        private Image _radarBg;
        private RectTransform _blipContainer;
        private Image _playerArrow;

        private TargetingSystem _targeting;
        private ShipController _controller;
        private float _radarSize = 160f;
        private float _radarRange = 1000f;

        private List<GameObject> _blipPool = new List<GameObject>();

        // Galaxy map
        private GameObject _galaxyMapPanel;
        private bool _isExpanded;
        private Vector3? _warpDestination;
        private GameObject _warpMarker;
        private List<GameObject> _galaxyBlips = new List<GameObject>();
        private float _galaxyScale = 0.08f; // world-to-map scale

        // Click detection
        private Button _expandButton;

        public static RadarHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new RadarHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            _targeting = mgr.targeting;
            _controller = mgr.controller;

            // === Compact radar panel: top-left ===
            _panel = HUDManager.CreatePanel(parent, "RadarPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10, -180), new Vector2(190, -10));
            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = mgr.panelColor;

            // Click button overlay to expand
            _expandButton = _panel.AddComponent<Button>();
            _expandButton.targetGraphic = panelImg;
            _expandButton.onClick.AddListener(ToggleGalaxyMap);

            // Radar circle background
            _radarBg = HUDManager.CreateImage(_panel.transform, "RadarBg",
                new Color(0f, 0.1f, 0.2f, 0.7f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-_radarSize / 2, -_radarSize / 2 - 10),
                new Vector2(_radarSize / 2, _radarSize / 2 - 10));
            _radarBg.sprite = CreateCircleSprite();

            var blipObj = new GameObject("BlipContainer", typeof(RectTransform));
            blipObj.transform.SetParent(_panel.transform, false);
            _blipContainer = blipObj.GetComponent<RectTransform>();
            _blipContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _blipContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _blipContainer.anchoredPosition = new Vector2(0, -10);
            _blipContainer.sizeDelta = new Vector2(_radarSize, _radarSize);

            // Player arrow (center, white, shows heading)
            var playerObj = new GameObject("PlayerArrow", typeof(RectTransform), typeof(Image));
            playerObj.transform.SetParent(_blipContainer, false);
            var pRt = playerObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(12, 12);
            pRt.anchoredPosition = Vector2.zero;
            _playerArrow = playerObj.GetComponent<Image>();
            _playerArrow.sprite = CreateTriangleSprite();
            _playerArrow.color = new Color(1f, 1f, 1f, 0.9f);
            _playerArrow.raycastTarget = false;

            // Hint text
            var hint = HUDManager.CreateText(_panel.transform, "Hint", "点击查看星图  Z=曲速", 9, mgr.textColor);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0, 2);
            hint.rectTransform.sizeDelta = new Vector2(0, 12);
            hint.alignment = TextAnchor.LowerCenter;

            // === Galaxy map panel (hidden by default) ===
            CreateGalaxyMap(parent);
        }

        private void CreateGalaxyMap(Transform parent)
        {
            _galaxyMapPanel = HUDManager.CreatePanel(parent, "GalaxyMap",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            var gmImg = _galaxyMapPanel.GetComponent<Image>();
            gmImg.color = new Color(0.02f, 0.05f, 0.1f, 0.92f);

            // Title
            var title = HUDManager.CreateText(_galaxyMapPanel.transform, "GalaxyTitle", "星图 — 按Z键沿船头方向曲速飞行，再按Z停止", 16, _mgr.borderColor, TextAnchor.UpperCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0, -20);
            title.rectTransform.sizeDelta = new Vector2(600, 24);

            // Map click area — invisible button covering the whole map (created FIRST so it's below close button)
            var clickArea = new GameObject("MapClickArea", typeof(RectTransform), typeof(Image));
            clickArea.transform.SetParent(_galaxyMapPanel.transform, false);
            var caRt = clickArea.GetComponent<RectTransform>();
            caRt.anchorMin = Vector2.zero;
            caRt.anchorMax = Vector2.one;
            caRt.offsetMin = Vector2.zero;
            caRt.offsetMax = Vector2.zero;
            var caImg = clickArea.GetComponent<Image>();
            caImg.color = new Color(0, 0, 0, 0); // invisible
            var caBtn = clickArea.AddComponent<Button>();
            caBtn.targetGraphic = caImg;
            caBtn.onClick.AddListener(OnMapClick);

            // Close button — created AFTER clickArea so it's on top in hierarchy
            var closeBtn = HUDManager.CreatePanel(_galaxyMapPanel.transform, "CloseBtn",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-100, -50), new Vector2(-20, -20));
            var closeImg = closeBtn.GetComponent<Image>();
            closeImg.color = new Color(0.5f, 0.2f, 0.2f, 0.8f);
            var closeText = HUDManager.CreateText(closeBtn.transform, "CloseText", "关闭", 14, Color.white, TextAnchor.MiddleCenter);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;
            var btn = closeBtn.AddComponent<Button>();
            btn.targetGraphic = closeImg;
            btn.onClick.AddListener(() => ToggleGalaxyMap());

            // Blip container for galaxy map
            var galaxyBlipObj = new GameObject("GalaxyBlips", typeof(RectTransform));
            galaxyBlipObj.transform.SetParent(_galaxyMapPanel.transform, false);
            var gbRt = galaxyBlipObj.GetComponent<RectTransform>();
            gbRt.anchorMin = Vector2.zero;
            gbRt.anchorMax = Vector2.one;
            gbRt.offsetMin = Vector2.zero;
            gbRt.offsetMax = Vector2.zero;

            // Warp destination marker (hidden by default)
            _warpMarker = new GameObject("WarpMarker", typeof(RectTransform), typeof(Image));
            _warpMarker.transform.SetParent(_galaxyMapPanel.transform, false);
            var wmRt = _warpMarker.GetComponent<RectTransform>();
            wmRt.anchorMin = new Vector2(0.5f, 0.5f);
            wmRt.anchorMax = new Vector2(0.5f, 0.5f);
            wmRt.sizeDelta = new Vector2(16, 16);
            var wmImg = _warpMarker.GetComponent<Image>();
            wmImg.sprite = CreateCircleSprite();
            wmImg.color = new Color(0f, 1f, 0.5f, 0.9f);
            wmImg.raycastTarget = false;
            _warpMarker.SetActive(false);

            _galaxyMapPanel.SetActive(false);
        }

        private void ToggleGalaxyMap()
        {
            _isExpanded = !_isExpanded;
            _galaxyMapPanel.SetActive(_isExpanded);
            _panel.SetActive(!_isExpanded);
        }

        private void OnMapClick()
        {
            // No destination setting — warp is now heading-based via Z key
            // Click does nothing on map (view only)
        }

        // === Sprite helpers ===
        private static Sprite _circleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = dist <= size / 2f ? 1f : 0f;
                    if (dist > size / 2f - 2f) alpha = 0.5f;
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        private static Sprite _triangleSprite;
        private static Sprite CreateTriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            Vector2 top = new Vector2(size / 2f, size - 2f);
            Vector2 bl = new Vector2(3f, 3f);
            Vector2 br = new Vector2(size - 3f, 3f);
            FillTriangle(tex, px, size, top, bl, br, Color.white);
            tex.SetPixels(px);
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _triangleSprite;
        }

        private static void FillTriangle(Texture2D tex, Color[] px, int sz, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
        {
            float minX = Mathf.Min(v0.x, v1.x, v2.x);
            float maxX = Mathf.Max(v0.x, v1.x, v2.x);
            float minY = Mathf.Min(v0.y, v1.y, v2.y);
            float maxY = Mathf.Max(v0.y, v1.y, v2.y);
            for (int y = (int)minY; y <= (int)maxY; y++)
                for (int x = (int)minX; x <= (int)maxX; x++)
                {
                    if (x < 0 || y < 0 || x >= sz || y >= sz) continue;
                    if (PointInTriangle(new Vector2(x, y), v0, v1, v2))
                        px[y * sz + x] = color;
                }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        public void UpdateHUD()
        {
            _targeting = _mgr.targeting;
            _controller = _mgr.controller;

            if (_isExpanded)
                UpdateGalaxyMap();
            else
                UpdateCompactRadar();
        }

        private void UpdateCompactRadar()
        {
            if (_targeting == null || _controller == null) return;

            Vector3 shipPos = _controller.transform.position;
            Vector3 shipForward = _controller.transform.forward;
            Vector3 shipRight = _controller.transform.right;

            _playerArrow.rectTransform.rotation = Quaternion.identity;

            var targets = _targeting.GetAllTargets();

            while (_blipPool.Count < targets.Count)
            {
                var blip = new GameObject("Blip", typeof(RectTransform), typeof(Image));
                blip.transform.SetParent(_blipContainer, false);
                var blipRt = blip.GetComponent<RectTransform>();
                blipRt.anchorMin = new Vector2(0.5f, 0.5f);
                blipRt.anchorMax = new Vector2(0.5f, 0.5f);
                blipRt.sizeDelta = new Vector2(10, 10);
                var blipImg = blip.GetComponent<Image>();
                blipImg.sprite = CreateTriangleSprite();
                blipImg.raycastTarget = false;
                blip.SetActive(false);
                _blipPool.Add(blip);
            }

            for (int i = 0; i < _blipPool.Count; i++)
            {
                if (i < targets.Count && targets[i] != null)
                {
                    var target = targets[i];
                    Vector3 toTarget = target.position - shipPos;
                    float dist = toTarget.magnitude;

                    if (dist > _radarRange) { _blipPool[i].SetActive(false); continue; }

                    float localX = Vector3.Dot(toTarget.normalized, shipRight);
                    float localZ = Vector3.Dot(toTarget.normalized, shipForward);
                    float normalizedDist = dist / _radarRange;
                    float radarX = localX * normalizedDist * _radarSize / 2f;
                    float radarY = localZ * normalizedDist * _radarSize / 2f;

                    _blipPool[i].SetActive(true);
                    var rt = _blipPool[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(radarX, radarY);

                    Vector3 targetForward = target.forward;
                    float angleDiff = Vector3.SignedAngle(shipForward, targetForward, Vector3.up);
                    rt.rotation = Quaternion.Euler(0, 0, -angleDiff);

                    var img = _blipPool[i].GetComponent<Image>();
                    if (target.CompareTag("Enemy"))
                        img.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                    else
                        img.color = new Color(0.8f, 0.8f, 0.8f, 0.7f);

                    float scale = Mathf.Lerp(1.2f, 0.6f, normalizedDist);
                    rt.sizeDelta = new Vector2(10 * scale, 10 * scale);
                }
                else
                {
                    _blipPool[i].SetActive(false);
                }
            }
        }

        private void UpdateGalaxyMap()
        {
            if (_controller == null) return;

            Vector3 playerPos = _controller.transform.position;
            var cam = Camera.main;
            Vector2 mapCenter = Vector2.zero;

            // Draw all planets
            var planets = GameObject.FindObjectsOfType<MeshRenderer>();
            var planetList = new List<(string name, Vector3 pos)>();
            foreach (var mr in planets)
            {
                if (mr.gameObject.name.Contains("Planet"))
                    planetList.Add((mr.gameObject.name, mr.transform.position));
            }

            // Clear old blips
            foreach (var blip in _galaxyBlips)
            {
                if (blip != null) Object.Destroy(blip);
            }
            _galaxyBlips.Clear();

            // Draw planets with labels
            foreach (var planet in planetList)
            {
                Vector2 mapPos = WorldToMap(planet.pos, playerPos);
                var blip = CreateGalaxyBlip(planet.name, mapPos, new Color(0.6f, 0.8f, 1f, 0.9f), 14f, false);
                _galaxyBlips.Add(blip);

                // Add label text next to planet
                var label = HUDManager.CreateText(_galaxyMapPanel.transform, $"Label_{planet.name}", planet.name, 11, new Color(0.7f, 0.9f, 1f, 0.9f), TextAnchor.UpperLeft);
                label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = mapPos + new Vector2(10, 0);
                label.rectTransform.sizeDelta = new Vector2(120, 14);
                label.raycastTarget = false;
                _galaxyBlips.Add(label.gameObject);
            }

            // Draw all enemies as triangles with heading
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                Vector2 mapPos = WorldToMap(enemy.transform.position, playerPos);
                var blip = CreateGalaxyBlip("Enemy", mapPos, new Color(1f, 0.2f, 0.2f, 0.8f), 8f, true);
                // Rotate triangle tip to match enemy heading
                Vector3 efwd = enemy.transform.forward;
                efwd.y = 0;
                if (efwd.sqrMagnitude > 0.001f)
                {
                    float eAngle = Vector3.SignedAngle(Vector3.forward, efwd, Vector3.up);
                    blip.GetComponent<RectTransform>().rotation = Quaternion.Euler(0, 0, -eAngle);
                }
                _galaxyBlips.Add(blip);
            }

            // Draw player as triangle with heading
            {
                Vector2 mapPos = WorldToMap(playerPos, playerPos);
                var blip = CreateGalaxyBlip("Player", mapPos, new Color(0.3f, 1f, 0.5f, 1f), 10f, true);
                // Rotate triangle tip to match ship heading
                Vector3 pfwd = _controller.transform.forward;
                pfwd.y = 0;
                if (pfwd.sqrMagnitude > 0.001f)
                {
                    float pAngle = Vector3.SignedAngle(Vector3.forward, pfwd, Vector3.up);
                    blip.GetComponent<RectTransform>().rotation = Quaternion.Euler(0, 0, -pAngle);
                }
                _galaxyBlips.Add(blip);
            }

            // Update warp marker if destination is set
            if (_warpDestination.HasValue)
            {
                Vector2 mapPos = WorldToMap(_warpDestination.Value, playerPos);
                var wmRt = _warpMarker.GetComponent<RectTransform>();
                wmRt.anchoredPosition = mapPos;
                _warpMarker.SetActive(true);
            }
        }

        private Vector2 WorldToMap(Vector3 worldPos, Vector3 refPos)
        {
            // Center map on player position
            Vector3 relative = worldPos - refPos;
            return new Vector2(relative.x * _galaxyScale, relative.z * _galaxyScale);
        }

        private GameObject CreateGalaxyBlip(string name, Vector2 mapPos, Color color, float size, bool useTriangle)
        {
            var blip = new GameObject($"GB_{name}", typeof(RectTransform), typeof(Image));
            blip.transform.SetParent(_galaxyMapPanel.transform, false);
            var rt = blip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = mapPos;
            rt.sizeDelta = new Vector2(size, size);
            var img = blip.GetComponent<Image>();
            img.sprite = useTriangle ? CreateTriangleSprite() : CreateCircleSprite();
            img.color = color;
            img.raycastTarget = false;
            return blip;
        }
    }
}
