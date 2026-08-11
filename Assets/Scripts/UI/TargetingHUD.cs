using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Targeting HUD — lock circle during locking, horizontal health bar after lock.
    /// Lock: green circle arc fills over 2s.
    /// After lock: horizontal bar above enemy — blue=shield (main), red=hull (small).
    /// Text: distance only.
    /// </summary>
    public class TargetingHUD
    {
        private HUDManager _mgr;
        private TargetingSystem _targeting;
        private Camera _cam;

        // Center scanning circle
        private GameObject _scanCircleObj;
        private Image _scanCircleImg;

        // Lock mode text


        // Sticky lock indicator
        // Per-target tracking
        private RectTransform _markerContainer;
        private List<TargetCircle> _targetCircles = new List<TargetCircle>();

        private float _centerCircleSize = 200f;
        private float _targetCircleSize = 40f;
        private float _barWidth = 30f;
        private float _barHeight = 2.5f;

        private struct TargetCircle
        {
            public GameObject container;
            public Image lockArcImg;       // green circle during locking
            public GameObject healthBarObj; // parent of the horizontal bar
            public Image barBgImg;          // gray background
            public Image shieldBarImg;      // blue shield bar
            public Image hullBarImg;         // red hull bar
            public Text infoText;
            public Transform trackedTarget;
        }

        public static TargetingHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new TargetingHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            _targeting = mgr.targeting;

            // Center scanning circle
            _scanCircleObj = new GameObject("ScanCircle", typeof(RectTransform), typeof(Image));
            _scanCircleObj.transform.SetParent(parent, false);
            var scRt = _scanCircleObj.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(0.5f, 0.5f);
            scRt.anchorMax = new Vector2(0.5f, 0.5f);
            scRt.anchoredPosition = Vector2.zero;
            scRt.sizeDelta = new Vector2(_centerCircleSize, _centerCircleSize);
            _scanCircleImg = _scanCircleObj.GetComponent<Image>();
            _scanCircleImg.sprite = CreateRingSprite();
            _scanCircleImg.color = new Color(0.3f, 0.7f, 1f, 0f);
            _scanCircleImg.raycastTarget = false;
            _scanCircleImg.type = Image.Type.Simple;

            // Sticky lock indicator
            // Marker container
            var mcObj = new GameObject("MarkerContainer", typeof(RectTransform));
            mcObj.transform.SetParent(parent, false);
            _markerContainer = mcObj.GetComponent<RectTransform>();
            _markerContainer.anchorMin = Vector2.zero;
            _markerContainer.anchorMax = Vector2.one;
            _markerContainer.offsetMin = Vector2.zero;
            _markerContainer.offsetMax = Vector2.zero;
        }

        private static Sprite _ringSprite;
        private static Sprite CreateRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float innerR = size / 2f - 6f;
            float outerR = size / 2f - 0f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = (dist >= innerR && dist <= outerR) ? 1f : 0f;
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        private static Sprite _whiteSprite;
        private static Sprite CreateWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }

        private TargetCircle CreateTargetCircle()
        {
            var tc = new TargetCircle();

            tc.container = new GameObject("TargetCircle", typeof(RectTransform));
            tc.container.transform.SetParent(_markerContainer, false);
            var rt = tc.container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(_targetCircleSize, _targetCircleSize);

            // Green lock circle — shown during locking only
            var lockObj = new GameObject("LockArc", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(tc.container.transform, false);
            var laRt = lockObj.GetComponent<RectTransform>();
            laRt.anchorMin = Vector2.zero;
            laRt.anchorMax = Vector2.one;
            laRt.offsetMin = Vector2.zero;
            laRt.offsetMax = Vector2.zero;
            tc.lockArcImg = lockObj.GetComponent<Image>();
            tc.lockArcImg.sprite = CreateRingSprite();
            tc.lockArcImg.type = Image.Type.Filled;
            tc.lockArcImg.fillMethod = Image.FillMethod.Radial360;
            tc.lockArcImg.fillOrigin = 0;
            tc.lockArcImg.fillClockwise = true;
            tc.lockArcImg.fillAmount = 0f;
            tc.lockArcImg.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            tc.lockArcImg.raycastTarget = false;
            tc.lockArcImg.gameObject.SetActive(false);

            // Horizontal health bar — shown after lock complete
            tc.healthBarObj = new GameObject("HealthBar", typeof(RectTransform));
            tc.healthBarObj.transform.SetParent(tc.container.transform, false);
            var hbRt = tc.healthBarObj.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0.5f);
            hbRt.anchorMax = new Vector2(0.5f, 0.5f);
            hbRt.pivot = new Vector2(0.5f, 0.5f);
            hbRt.anchoredPosition = new Vector2(0, _targetCircleSize * 0.6f);
            hbRt.sizeDelta = new Vector2(_barWidth, _barHeight);
            tc.healthBarObj.SetActive(false);

            // Gray background bar
            tc.barBgImg = CreateFillImage(tc.healthBarObj.transform, "BarBg", new Color(0.3f, 0.3f, 0.3f, 0.6f));
            tc.barBgImg.rectTransform.anchorMin = Vector2.zero;
            tc.barBgImg.rectTransform.anchorMax = Vector2.one;
            tc.barBgImg.rectTransform.offsetMin = Vector2.zero;
            tc.barBgImg.rectTransform.offsetMax = Vector2.zero;

            // Blue shield bar (left, main portion)
            tc.shieldBarImg = CreateFillImage(tc.healthBarObj.transform, "ShieldBar", new Color(0.3f, 0.6f, 1f, 0.9f));
            tc.shieldBarImg.rectTransform.anchorMin = new Vector2(0f, 0f);
            tc.shieldBarImg.rectTransform.anchorMax = new Vector2(0f, 1f);
            tc.shieldBarImg.rectTransform.pivot = new Vector2(0f, 0.5f);
            tc.shieldBarImg.rectTransform.anchoredPosition = Vector2.zero;
            tc.shieldBarImg.rectTransform.sizeDelta = new Vector2(_barWidth, 0);

            // Red hull bar (right of shield, small portion)
            tc.hullBarImg = CreateFillImage(tc.healthBarObj.transform, "HullBar", new Color(1f, 0.2f, 0.2f, 0.9f));
            tc.hullBarImg.rectTransform.anchorMin = new Vector2(0f, 0f);
            tc.hullBarImg.rectTransform.anchorMax = new Vector2(0f, 1f);
            tc.hullBarImg.rectTransform.pivot = new Vector2(0f, 0.5f);
            tc.hullBarImg.rectTransform.anchoredPosition = Vector2.zero;
            tc.hullBarImg.rectTransform.sizeDelta = new Vector2(_barWidth * 0.15f, 0);

            // Info text — distance only, positioned above health bar
            tc.infoText = HUDManager.CreateText(tc.container.transform, "InfoText", "", 10, new Color(0.8f, 0.9f, 1f, 0.9f), TextAnchor.LowerCenter);
            var itRt = tc.infoText.rectTransform;
            itRt.anchorMin = new Vector2(0.5f, 0.5f);
            itRt.anchorMax = new Vector2(0.5f, 0.5f);
            itRt.pivot = new Vector2(0.5f, 0f);
            itRt.anchoredPosition = new Vector2(0, _targetCircleSize * 0.6f + _barHeight + 1f);
            itRt.sizeDelta = new Vector2(120, 12);

            return tc;
        }

        private static Sprite _roundedSprite;
        private static Sprite CreateRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            float r = 4f; // corner radius in pixels
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Distance to nearest rounded corner
                    float dx = Mathf.Max(0, Mathf.Abs(x - center.x) - (center.x - r));
                    float dy = Mathf.Max(0, Mathf.Abs(y - center.y) - (center.y - r));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color(1, 1, 1, alpha);
                }
            tex.SetPixels(px);
            tex.Apply();
            // Use 9-slice borders for stretching
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedSprite;
        }

        private static Image CreateFillImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.sprite = CreateRoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public void UpdateHUD()
        {
            _targeting = _mgr.targeting;
            if (_targeting == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Scan circle size
            float radius = _targeting.GetCurrentScreenRadius();
            _centerCircleSize = radius * 2f;
            _scanCircleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(_centerCircleSize, _centerCircleSize);

            var lockedTargets = _targeting.GetLockedTargets();

            while (_targetCircles.Count < lockedTargets.Count)
                _targetCircles.Add(CreateTargetCircle());

            for (int i = 0; i < _targetCircles.Count; i++)
            {
                if (i < lockedTargets.Count && lockedTargets[i] != null)
                {
                    var target = lockedTargets[i];
                    Vector3 worldCenter = TargetingSystem.GetModelCenter(target);
                    Vector3 screenPos = _cam.WorldToScreenPoint(worldCenter);

                    if (screenPos.z > 0f)
                    {
                        _targetCircles[i].container.SetActive(true);
                        var rt = _targetCircles[i].container.GetComponent<RectTransform>();
                        rt.position = new Vector3(screenPos.x, screenPos.y, 0);

                        float dist = Vector3.Distance(_cam.transform.position, target.position);
                        float scale = Mathf.Clamp(400f / dist, 0.4f, 2.5f);
                        rt.sizeDelta = new Vector2(_targetCircleSize * scale, _targetCircleSize * scale);

                        bool isPrimaryTarget = (target == _targeting.primaryTarget);
                        bool isLocked = _targeting.IsLockComplete;
                        var targetHealth = target.GetComponent<ShipHealth>();
                        float hullPct = targetHealth != null ? targetHealth.HullPercent : 0f;
                        float shieldPct = targetHealth != null ? targetHealth.ShieldPercent : 0f;

                        // Distance text — only for primary target after lock
                        if (isPrimaryTarget && isLocked)
                        {
                            _targetCircles[i].infoText.gameObject.SetActive(true);
                            _targetCircles[i].infoText.text = $"{dist:F0}m";
                            _targetCircles[i].infoText.color = new Color(0.9f, 0.95f, 1f, 0.95f);
                        }
                        else
                        {
                            _targetCircles[i].infoText.gameObject.SetActive(false);
                        }

                        // Lock circle — green arc during locking only
                        float lockProgress = _targeting.lockProgress;
                        if (isPrimaryTarget && !isLocked && lockProgress > 0f)
                        {
                            _targetCircles[i].lockArcImg.gameObject.SetActive(true);
                            _targetCircles[i].lockArcImg.fillAmount = lockProgress;
                        }
                        else
                        {
                            _targetCircles[i].lockArcImg.gameObject.SetActive(false);
                        }

                        // Horizontal health bar — after lock complete
                        if (isPrimaryTarget && isLocked)
                        {
                            _targetCircles[i].healthBarObj.SetActive(true);

                            // Scale bar with distance
                            float barW = _barWidth * scale;
                            float barH = _barHeight * scale;
                            float barY = _targetCircleSize * scale * 0.6f;
                            var hbRt = _targetCircles[i].healthBarObj.GetComponent<RectTransform>();
                            hbRt.sizeDelta = new Vector2(barW, barH);
                            hbRt.anchoredPosition = new Vector2(0, barY);

                            // Move text above bar
                            var itRt = _targetCircles[i].infoText.rectTransform;
                            itRt.anchoredPosition = new Vector2(0, barY + barH + 1f);

                            // Shield bar: blue, fills from left
                            float shieldW = barW * shieldPct;
                            _targetCircles[i].shieldBarImg.rectTransform.sizeDelta = new Vector2(shieldW, 0);
                            _targetCircles[i].shieldBarImg.color = new Color(0.3f, 0.6f, 1f, 0.9f);

                            // Hull bar: red, positioned right after shield bar
                            float hullW = barW * 0.15f * hullPct;
                            _targetCircles[i].hullBarImg.rectTransform.anchoredPosition = new Vector2(shieldW, 0);
                            _targetCircles[i].hullBarImg.rectTransform.sizeDelta = new Vector2(hullW, 0);
                            _targetCircles[i].hullBarImg.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                        }
                        else
                        {
                            _targetCircles[i].healthBarObj.SetActive(false);
                        }
                    }
                    else
                    {
                        _targetCircles[i].container.SetActive(false);
                    }
                }
                else
                {
                    _targetCircles[i].container.SetActive(false);
                }
            }
        }
    }
}
