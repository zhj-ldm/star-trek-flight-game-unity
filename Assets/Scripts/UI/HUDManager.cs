using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// Root HUD manager — creates the Canvas and instantiates all HUD sub-modules.
    /// Each sub-module manages its own panel (status, radar, weapons, targeting, combat log).
    /// Star Trek style: semi-transparent blue, clean, minimal.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HUDManager : MonoBehaviour
    {
        [Header("Ship References")]
        public ShipController controller;
        public ShipHealth health;
        public ShipWeaponManager weapons;
        public TargetingSystem targeting;
        public AutoOrbitController autoOrbit;

        [Header("Style")]
        public Color panelColor = new Color(0.05f, 0.15f, 0.3f, 0.65f);
        public Color borderColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        public Color textColor = new Color(0.6f, 0.85f, 1f, 0.95f);
        public Color warningColor = new Color(1f, 0.4f, 0.3f, 0.95f);
        public Color goodColor = new Color(0.3f, 1f, 0.5f, 0.95f);

        private Canvas _canvas;
        private ThrottleHUD _throttleHUD;
        private StatusLCARS _statusHUD;
        private NavLCARS _navHUD;
        private TargetingHUD _targetingHUD;
        private GalaxyMap _galaxyMap;
        private VelocityHUD _velocityHUD;

        // Font — use OS dynamic font for crisp rendering on high-DPI displays
        private static Font _uiFont;
        public static Font UIFont
        {
            get
            {
                if (_uiFont == null)
                {
                    _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
                    _uiFont.material.mainTexture.filterMode = FilterMode.Point;
                }
                return _uiFont;
            }
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            // Graphic raycaster — required for UI button clicks
            if (gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Canvas scaler for consistent UI
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Dynamic reference resolution: if the screen is smaller than the design
            // resolution (1920x1080), use the screen resolution as reference so the UI
            // is never scaled below 100%. On larger screens, keep 1920x1080 and scale up.
            float designW = 1920f;
            float designH = 1080f;
            float refW = Mathf.Min(Screen.width, designW);
            float refH = Mathf.Min(Screen.height, designH);
            scaler.referenceResolution = new Vector2(refW, refH);

            Debug.Log($"[HUDManager] Screen: {Screen.width}x{Screen.height} (DPI={Screen.dpi}), " +
                      $"RefRes: {refW}x{refH}, ScaleFactor: {scaler.scaleFactor}");

            // High-quality text rendering
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 8;
            QualitySettings.SetQualityLevel(5, true); // Ultra

            // Event system check
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        void Start()
        {
            // Auto-find player ship specifically (not any enemy ShipController)
            if (controller == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    controller = playerObj.GetComponent<ShipController>();
                if (controller == null)
                {
                    foreach (var sc in FindObjectsOfType<ShipController>())
                    {
                        if (sc.isPlayerControlled) { controller = sc; break; }
                    }
                }
            }
            if (health == null) health = controller != null ? (controller.health ?? controller.GetComponent<ShipHealth>()) : null;
            if (weapons == null) weapons = controller != null ? controller.GetComponent<ShipWeaponManager>() : null;
            if (targeting == null) targeting = controller != null ? controller.GetComponent<TargetingSystem>() : null;
            if (autoOrbit == null) autoOrbit = controller != null ? controller.GetComponent<AutoOrbitController>() : null;

            CreateAllHUD();
        }

        private void CreateAllHUD()
        {
            _statusHUD = StatusLCARS.Create(_canvas.transform, this);
            _navHUD = NavLCARS.Create(_canvas.transform, this);
            _targetingHUD = TargetingHUD.Create(_canvas.transform, this);
            _throttleHUD = ThrottleHUD.Create(_canvas.transform, this);
            _galaxyMap = GalaxyMap.Create(_canvas.transform, this);
            _velocityHUD = VelocityHUD.Create(_canvas.transform, this);
            CreateMapButton();
        }

        private void CreateMapButton()
        {
            var btn = new GameObject("MapButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(_canvas.transform, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-12, -12);
            rt.sizeDelta = new Vector2(32, 32);
            var img = btn.GetComponent<Image>();
            img.sprite = GetFilledRoundedSprite(16f);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.15f, 0.3f, 0.85f);

            var label = CreateText(btn.transform, "Lbl", "★", 16, new Color(0.6f, 0.8f, 1f, 0.9f), TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.raycastTarget = false;

            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_galaxyMap == null)
                    _galaxyMap = GalaxyMap.Create(_canvas.transform, this);
                _galaxyMap.Toggle();
            });
        }

        void Update()
        {
            // Toggle galaxy map with G key
            if (Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log("[HUDManager] G key pressed, toggling galaxy map. _galaxyMap=" + (_galaxyMap != null));
                if (_galaxyMap == null)
                    _galaxyMap = GalaxyMap.Create(_canvas.transform, this);
                _galaxyMap.Toggle();
            }

            if (_galaxyMap != null && _galaxyMap.IsOpen) return;

            _statusHUD?.UpdateHUD();
            _navHUD?.UpdateHUD();
            _targetingHUD?.UpdateHUD();
            _throttleHUD?.UpdateHUD();
            _velocityHUD?.UpdateHUD();
        }

        /// <summary>Update ship references.</summary>
        public void SetShipReferences(ShipController ctrl, ShipHealth hp, ShipWeaponManager wpn, TargetingSystem tgt)
        {
            controller = ctrl;
            health = hp;
            weapons = wpn;
            targeting = tgt;
            autoOrbit = ctrl != null ? ctrl.GetComponent<AutoOrbitController>() : null;
        }

        // Helper: create a panel with background and border
        public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return obj;
        }

        // Helper: create a text element
        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.font = UIFont;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Helper: create an Image element
        public static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        // Combat log removed — no-op for backward compat
        public void LogMessage(string msg, Color color)
        {
        }

        // Rounded border sprite (ring with transparent center, 9-sliced)
        private static Sprite _roundedBorderSprite;
        public static Sprite GetRoundedBorderSprite(float cornerRadius = 8f, float borderWidth = 3f)
        {
            if (_roundedBorderSprite != null) return _roundedBorderSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];

            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    bool inCornerX = x < cornerRadius || x >= ts - cornerRadius;
                    bool inCornerY = y < cornerRadius || y >= ts - cornerRadius;

                    if (inCornerX && inCornerY)
                    {
                        // Corner pixel — quarter annulus
                        float cx = x < ts / 2f ? cornerRadius : ts - 1 - cornerRadius;
                        float cy = y < ts / 2f ? cornerRadius : ts - 1 - cornerRadius;
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        bool insideOuter = dist <= cornerRadius;
                        bool outsideInner = dist > cornerRadius - borderWidth;
                        px[y * ts + x] = (insideOuter && outsideInner) ? Color.white : new Color(0, 0, 0, 0);
                    }
                    else if (inCornerX || inCornerY)
                    {
                        // Edge pixel — straight band
                        float minEdge = Mathf.Min(x, y, ts - 1 - x, ts - 1 - y);
                        px[y * ts + x] = (minEdge < borderWidth) ? Color.white : new Color(0, 0, 0, 0);
                    }
                    else
                    {
                        px[y * ts + x] = new Color(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _roundedBorderSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            return _roundedBorderSprite;
        }

        // Filled rounded rectangle sprite (9-sliced)
        private static Sprite _filledRoundedSprite;
        public static Sprite GetFilledRoundedSprite(float cornerRadius = 8f)
        {
            if (_filledRoundedSprite != null) return _filledRoundedSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            Vector2 center = new Vector2(ts / 2f, ts / 2f);

            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - center.x) - (center.x - cornerRadius));
                    float dy = Mathf.Max(0, Mathf.Abs(y - center.y) - (center.y - cornerRadius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * ts + x] = new Color(1, 1, 1, Mathf.Clamp01(cornerRadius - d));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _filledRoundedSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            return _filledRoundedSprite;
        }
    }
}
