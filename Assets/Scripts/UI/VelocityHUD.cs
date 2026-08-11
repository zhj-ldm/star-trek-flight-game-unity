using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// Velocity/heading HUD overlay — toggle with H key.
    ///
    /// Markers (all projected onto camera screen, move with free-look):
    /// 1. Nosecone: horizontal line + open V — ship forward (always visible)
    /// 2. Tail: same shape as nosecone — ship backward (only when looking backward)
    /// 3. Circle-cross (⊕) — velocity direction (green, always visible)
    /// 4. Circle-dot — velocity opposite direction: large ring + dot at reverse-vel position
    ///    (only when velocity is behind camera)
    /// </summary>
    public class VelocityHUD
    {
        private HUDManager _mgr;
        private ShipController _controller;
        private TargetingSystem _targeting;

        private GameObject _root;
        private RectTransform _nosecone;
        private RectTransform _tail;
        private RectTransform _circleCross;
        private RectTransform _circleDot;  // large ring + dot
        private RectTransform _dot;        // the dot inside circle-dot
        private Text _circleLabel;

        private bool _visible;

        private const float MaxOffset = 500f;
        private const float CircleCrossBaseAlpha = 0.9f;
        private const float NoseconeBaseAlpha = 0.9f;
        private const float TailBaseAlpha = 0.9f;
        private const float CircleDotBaseAlpha = 0.9f;

        public static VelocityHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new VelocityHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;

            _root = new GameObject("VelocityHUD");
            _root.transform.SetParent(parent, false);
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // --- Nosecone: horizontal line + open V below ---
            _nosecone = CreateNosecone(_root.transform);
            _nosecone.anchorMin = new Vector2(0.5f, 0.5f);
            _nosecone.anchorMax = new Vector2(0.5f, 0.5f);
            _nosecone.pivot = new Vector2(0.5f, 0.5f);
            _nosecone.anchoredPosition = Vector2.zero;
            _nosecone.sizeDelta = new Vector2(80, 50);

            // --- Tail: same shape as nosecone (not inverted) ---
            _tail = CreateNosecone(_root.transform);
            _tail.anchorMin = new Vector2(0.5f, 0.5f);
            _tail.anchorMax = new Vector2(0.5f, 0.5f);
            _tail.pivot = new Vector2(0.5f, 0.5f);
            _tail.anchoredPosition = Vector2.zero;
            _tail.sizeDelta = new Vector2(80, 50);

            // --- Circle-cross (velocity direction marker) — green ---
            _circleCross = CreateCircleCross(_root.transform);
            _circleCross.anchorMin = new Vector2(0.5f, 0.5f);
            _circleCross.anchorMax = new Vector2(0.5f, 0.5f);
            _circleCross.pivot = new Vector2(0.5f, 0.5f);
            _circleCross.anchoredPosition = Vector2.zero;
            _circleCross.sizeDelta = new Vector2(48, 48);

            // --- Circle-dot (velocity opposite marker): large ring centered at screen,
            //     dot positioned at reverse velocity direction inside it ---
            _circleDot = new GameObject("CircleDot").AddComponent<RectTransform>();
            _circleDot.transform.SetParent(_root.transform, false);
            _circleDot.anchorMin = new Vector2(0.5f, 0.5f);
            _circleDot.anchorMax = new Vector2(0.5f, 0.5f);
            _circleDot.pivot = new Vector2(0.5f, 0.5f);
            _circleDot.anchoredPosition = Vector2.zero;
            _circleDot.sizeDelta = new Vector2(48, 48);

            // Large ring (fixed at center of this marker)
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(_circleDot.transform, false);
            var ringImg = ringGo.AddComponent<Image>();
            ringImg.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            ringImg.raycastTarget = false;
            ringImg.sprite = CreateRingSprite(4f);
            ringImg.type = Image.Type.Simple;
            var ringRect = ringGo.GetComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;

            // Dot — positioned dynamically inside the ring
            _dot = new GameObject("Dot").AddComponent<RectTransform>();
            _dot.transform.SetParent(_circleDot.transform, false);
            _dot.anchorMin = new Vector2(0.5f, 0.5f);
            _dot.anchorMax = new Vector2(0.5f, 0.5f);
            _dot.pivot = new Vector2(0.5f, 0.5f);
            _dot.anchoredPosition = Vector2.zero;
            _dot.sizeDelta = new Vector2(6, 6);
            var dotImg = _dot.gameObject.AddComponent<Image>();
            dotImg.color = new Color(0.3f, 0.9f, 0.4f, 0.95f);
            dotImg.raycastTarget = false;

            // Velocity label "-V[target] speed"
            _circleLabel = HUDManager.CreateText(_root.transform, "VelLabel", "",
                12, new Color(0.3f, 0.9f, 0.4f, 0.85f), TextAnchor.MiddleCenter);
            _circleLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _circleLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _circleLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _circleLabel.rectTransform.anchoredPosition = new Vector2(0, -32);
            _circleLabel.rectTransform.sizeDelta = new Vector2(300, 20);

            _root.SetActive(false);
        }

        /// <summary>
        /// Nosecone marker: long horizontal line above center, open V below center.
        /// Vertex (top of V) at RectTransform center.
        /// </summary>
        private RectTransform CreateNosecone(Transform parent)
        {
            var go = new GameObject("Nosecone");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            Color c = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            float thick = 2.5f;
            float lineLen = 40f;
            float armLen = 18f;
            float gap = 4f;

            // Horizontal line above center
            var lineGo = new GameObject("HLine");
            lineGo.transform.SetParent(go.transform, false);
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.color = c;
            lineImg.raycastTarget = false;
            var lineRect = lineGo.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0f);
            lineRect.anchoredPosition = new Vector2(0, gap);
            lineRect.sizeDelta = new Vector2(lineLen, thick);

            // Left arm — vertex at center, grows downward, leans left
            var leftGo = new GameObject("ArmLeft");
            leftGo.transform.SetParent(go.transform, false);
            var leftImg = leftGo.AddComponent<Image>();
            leftImg.color = c;
            leftImg.raycastTarget = false;
            var leftRect = leftGo.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0.5f, 0.5f);
            leftRect.anchorMax = new Vector2(0.5f, 0.5f);
            leftRect.pivot = new Vector2(0.5f, 1f);
            leftRect.anchoredPosition = new Vector2(0, 0f);
            leftRect.sizeDelta = new Vector2(thick, armLen);
            leftRect.localRotation = Quaternion.Euler(0, 0, 45f);

            // Right arm
            var rightGo = new GameObject("ArmRight");
            rightGo.transform.SetParent(go.transform, false);
            var rightImg = rightGo.AddComponent<Image>();
            rightImg.color = c;
            rightImg.raycastTarget = false;
            var rightRect = rightGo.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.5f, 0.5f);
            rightRect.anchorMax = new Vector2(0.5f, 0.5f);
            rightRect.pivot = new Vector2(0.5f, 1f);
            rightRect.anchoredPosition = new Vector2(0, 0f);
            rightRect.sizeDelta = new Vector2(thick, armLen);
            rightRect.localRotation = Quaternion.Euler(0, 0, -45f);

            return rt;
        }

        /// <summary>Create green circle with green cross inside (⊕ marker).</summary>
        private RectTransform CreateCircleCross(Transform parent)
        {
            var go = new GameObject("CircleCross");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            Color green = new Color(0.3f, 0.9f, 0.4f, 0.9f);

            var circleGo = new GameObject("Circle");
            circleGo.transform.SetParent(go.transform, false);
            var circleImg = circleGo.AddComponent<Image>();
            circleImg.color = green;
            circleImg.raycastTarget = false;
            circleImg.sprite = CreateRingSprite(4f);
            circleImg.type = Image.Type.Simple;
            var circleRect = circleGo.GetComponent<RectTransform>();
            circleRect.anchorMin = Vector2.zero;
            circleRect.anchorMax = Vector2.one;
            circleRect.offsetMin = Vector2.zero;
            circleRect.offsetMax = Vector2.zero;

            var hBarGo = new GameObject("HBar");
            hBarGo.transform.SetParent(go.transform, false);
            var hBarImg = hBarGo.AddComponent<Image>();
            hBarImg.color = green;
            hBarImg.raycastTarget = false;
            var hBarRect = hBarGo.GetComponent<RectTransform>();
            hBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            hBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            hBarRect.pivot = new Vector2(0.5f, 0.5f);
            hBarRect.anchoredPosition = Vector2.zero;
            hBarRect.sizeDelta = new Vector2(28, 2);

            var vBarGo = new GameObject("VBar");
            vBarGo.transform.SetParent(go.transform, false);
            var vBarImg = vBarGo.AddComponent<Image>();
            vBarImg.color = green;
            vBarImg.raycastTarget = false;
            var vBarRect = vBarGo.GetComponent<RectTransform>();
            vBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            vBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            vBarRect.pivot = new Vector2(0.5f, 0.5f);
            vBarRect.anchoredPosition = Vector2.zero;
            vBarRect.sizeDelta = new Vector2(2, 28);

            return rt;
        }

        private static Sprite _ringSprite;
        private static Sprite CreateRingSprite(float thickness)
        {
            if (_ringSprite != null) return _ringSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            Vector2 center = new Vector2(ts / 2f, ts / 2f);
            float outerR = ts / 2f - 1f;
            float innerR = outerR - thickness;
            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    px[y * ts + x] = (d <= outerR && d >= innerR) ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100);
            return _ringSprite;
        }

        public void UpdateHUD()
        {
            _controller = _mgr.controller;
            _targeting = _mgr.targeting;
            if (_controller == null) return;

            if (Input.GetKeyDown(KeyCode.H))
            {
                _visible = !_visible;
                _root.SetActive(_visible);
            }

            if (!_visible) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 camFwd = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;
            float scale = 300f;

            // --- Project ship forward (nosecone) ---
            Vector3 noseDir = _controller.transform.forward;
            float noseX = Vector3.Dot(noseDir, camRight);
            float noseY = Vector3.Dot(noseDir, camUp);
            float noseZ = Vector3.Dot(noseDir, camFwd);

            Vector2 noseOffset = new Vector2(noseX, noseY) * scale;
            if (noseOffset.magnitude > MaxOffset)
                noseOffset = noseOffset.normalized * MaxOffset;

            // Nosecone: visible when facing forward (noseZ > 0)
            bool noseVisible = noseZ > 0.01f;
            _nosecone.anchoredPosition = noseVisible ? noseOffset : -noseOffset.normalized * MaxOffset;
            SetAlpha(_nosecone, noseVisible ? 1f : 0f, NoseconeBaseAlpha);

            // Tail: same shape, at 180° opposite. Visible when looking backward (noseZ < 0)
            bool tailVisible = noseZ < -0.01f;
            Vector2 tailOffset = -noseOffset;
            _tail.anchoredPosition = tailVisible ? tailOffset : noseOffset.normalized * MaxOffset;
            SetAlpha(_tail, tailVisible ? 1f : 0f, TailBaseAlpha);

            // --- Project velocity direction ---
            Vector3 relVel = _controller.velocity;
            string targetName = "";

            if (_targeting != null && _targeting.primaryTarget != null)
            {
                targetName = _targeting.primaryTarget.name;
                var targetController = _targeting.primaryTarget.GetComponent<ShipController>();
                if (targetController != null)
                    relVel = _controller.velocity - targetController.velocity;
            }

            float speed = relVel.magnitude;
            Vector3 velDir = speed > 0.01f ? relVel.normalized : noseDir;

            float projX = Vector3.Dot(velDir, camRight);
            float projY = Vector3.Dot(velDir, camUp);
            float projZ = Vector3.Dot(velDir, camFwd);

            Vector2 offset = new Vector2(projX, projY) * scale;
            if (offset.magnitude > MaxOffset)
                offset = offset.normalized * MaxOffset;

            // Circle-cross: visible when velocity is in front (projZ > 0)
            bool velForward = projZ > 0.01f;
            Vector2 velPos = velForward ? offset : offset.normalized * MaxOffset;
            _circleCross.anchoredPosition = velPos;
            float velAlpha = Mathf.Clamp01(speed / 2f);
            SetAlpha(_circleCross, velForward ? velAlpha : 0f, CircleCrossBaseAlpha);

            // Circle-dot: visible when velocity is behind camera (projZ < 0)
            // Whole marker positioned at reverse-velocity projection on screen.
            // Dot stays at center of circle — does NOT move relative to ring.
            bool velBehind = projZ < -0.01f;
            // Reverse velocity direction projected on camera plane
            Vector2 revOffset = -offset;
            Vector2 dotPos = velBehind ? revOffset : revOffset.normalized * MaxOffset;
            _circleDot.anchoredPosition = dotPos;  // whole marker moves as one unit
            _dot.anchoredPosition = Vector2.zero;  // dot always at center of ring
            SetAlpha(_circleDot, velBehind ? velAlpha : 0f, CircleDotBaseAlpha);

            // Label
            string speedStr = speed >= 100f ? speed.ToString("F0") : speed.ToString("F1");
            string label = targetName.Length > 0
                ? $"-V {targetName}  {speedStr}m/s"
                : $"-V  {speedStr}m/s";
            _circleLabel.text = label;
            _circleLabel.rectTransform.anchoredPosition = new Vector2(velPos.x, velPos.y - 32);
            _circleLabel.color = new Color(0.3f, 0.9f, 0.4f, 0.85f * velAlpha);
        }

        private void SetAlpha(RectTransform rt, float alpha, float baseAlpha)
        {
            foreach (var img in rt.GetComponentsInChildren<Image>())
            {
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, baseAlpha * alpha);
            }
        }
    }
}
