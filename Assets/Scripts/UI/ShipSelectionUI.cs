using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace StarTrekCombat
{
    /// <summary>
    /// Full-screen ship selection UI with LCARS-style rounded buttons.
    /// Click to select + keyboard ←→ + ENTER.
    /// </summary>
    public class ShipSelectionUI : MonoBehaviour
    {
        private int _selectedIndex = 0;
        private readonly string[] _shipNames = { "USS Enterprise NCC-1701-D", "USS Excelsior NCC-2000", "USS Voyager NCC-74656", "USS Defiant NX-74205", "USS Enterprise NCC-1701-XI" };
        private readonly string[] _shipKeys = { "Enterprise", "Excelsior", "Voyager", "Defiant", "EnterpriseXI" };
        private Button[] _buttons = new Button[5];
        private GameObject _settingsPanel;
        private int _selectedControlMode;  // 0=Simple, 1=Realistic
        private static Font _font;
        static Font GetFont()
        {
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 28);
                _font.material.mainTexture.filterMode = FilterMode.Point;
            }
            return _font;
        }

        private static Sprite _roundedSprite;
        static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = 12f;
            Vector2 center = new Vector2(ts / 2f, ts / 2f);
            Color[] px = new Color[ts * ts];
            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - center.x) - (center.x - r));
                    float dy = Mathf.Max(0, Mathf.Abs(y - center.y) - (center.y - r));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * ts + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedSprite;
        }

        void Start() { CreateUI(); }

        void CreateUI()
        {
            // Canvas
            var canvasGo = new GameObject("SelectionCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Event system
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.06f, 1f);
            var bgRect = bgImg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "选择飞船";
            titleText.font = GetFont();
            titleText.fontSize = 48;
            titleText.color = new Color(0.7f, 0.85f, 1f);
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -60);
            titleRect.sizeDelta = new Vector2(600, 70);

            // Buttons — vertical list, compact and centered
            float btnW = 360f;
            float btnH = 64f;
            float gap = 12f;
            float totalH = _shipNames.Length * btnH + (_shipNames.Length - 1) * gap;
            float startY = totalH / 2f - btnH / 2f;

            for (int i = 0; i < _shipNames.Length; i++)
            {
                CreateShipButton(canvasGo.transform, i, _shipNames[i],
                    new Vector2(0, startY - i * (btnH + gap)), btnW, btnH);
            }

            // Instruction text
            var instGo = new GameObject("Instructions");
            instGo.transform.SetParent(canvasGo.transform, false);
            var instText = instGo.AddComponent<Text>();
            instText.text = "← → 选择    ENTER 确认    或点击飞船名称";
            instText.font = GetFont();
            instText.fontSize = 22;
            instText.color = new Color(0.5f, 0.6f, 0.7f, 0.8f);
            instText.alignment = TextAnchor.MiddleCenter;
            var instRect = instText.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0.5f, 0f);
            instRect.anchorMax = new Vector2(0.5f, 0f);
            instRect.pivot = new Vector2(0.5f, 0f);
            instRect.anchoredPosition = new Vector2(0, 30);
            instRect.sizeDelta = new Vector2(500, 40);

            // Settings button — top-right corner
            var settingsBtnGo = new GameObject("SettingsButton");
            settingsBtnGo.transform.SetParent(canvasGo.transform, false);
            var settingsBtn = settingsBtnGo.AddComponent<Button>();
            var settingsImg = settingsBtnGo.AddComponent<Image>();
            settingsImg.sprite = GetRoundedSprite();
            settingsImg.type = Image.Type.Sliced;
            settingsImg.color = new Color(0.08f, 0.15f, 0.3f, 0.85f);
            var settingsRect = settingsBtnGo.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(1f, 1f);
            settingsRect.anchorMax = new Vector2(1f, 1f);
            settingsRect.pivot = new Vector2(1f, 1f);
            settingsRect.anchoredPosition = new Vector2(-20, -20);
            settingsRect.sizeDelta = new Vector2(120, 44);

            var settingsLabelGo = new GameObject("Label");
            settingsLabelGo.transform.SetParent(settingsBtnGo.transform, false);
            var settingsLabelText = settingsLabelGo.AddComponent<Text>();
            settingsLabelText.text = "⚙ 设置";
            settingsLabelText.font = GetFont();
            settingsLabelText.fontSize = 20;
            settingsLabelText.color = new Color(0.6f, 0.85f, 1f);
            settingsLabelText.alignment = TextAnchor.MiddleCenter;
            var sLabelRect = settingsLabelText.GetComponent<RectTransform>();
            sLabelRect.anchorMin = Vector2.zero;
            sLabelRect.anchorMax = Vector2.one;
            sLabelRect.offsetMin = Vector2.zero;
            sLabelRect.offsetMax = Vector2.zero;
            settingsLabelText.raycastTarget = false;

            var sColors = settingsBtn.colors;
            sColors.normalColor = Color.white;
            sColors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            sColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            settingsBtn.colors = sColors;
            settingsBtn.onClick.AddListener(ToggleSettingsPanel);

            // Load saved control mode
            _selectedControlMode = PlayerPrefs.GetInt("FlightControlMode", 0);

            // Settings panel (hidden by default)
            CreateSettingsPanel(canvasGo.transform);
        }

        void CreateSettingsPanel(Transform parent)
        {
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(parent, false);

            // Dim background
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(_settingsPanel.transform, false);
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.7f);
            var dimRect = dimImg.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            // Panel background
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(_settingsPanel.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite = GetRoundedSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.04f, 0.08f, 0.16f, 0.98f);
            var panelRect = panelImg.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520, 360);

            // Title
            var titleGo = new GameObject("SettingsTitle");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "飞行控制设置";
            titleText.font = GetFont();
            titleText.fontSize = 32;
            titleText.color = new Color(0.7f, 0.85f, 1f);
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -24);
            titleRect.sizeDelta = new Vector2(400, 50);

            // Mode buttons
            string[] modeNames = { "简易模式", "真实模式" };
            string[] modeDescs = {
                "当前控制方式不变\nP/L 蓄力推进  R 一次性稳定",
                "P/L 瞬时满推力/归零\nAlt+P/L 平滑调速  R 姿态锁定\nLIN 惯性滑行+残余力矩"
            };

            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var btnGo = new GameObject($"ModeBtn_{i}");
                btnGo.transform.SetParent(panelGo.transform, false);
                var btn = btnGo.AddComponent<Button>();
                var img = btnGo.AddComponent<Image>();
                img.sprite = GetRoundedSprite();
                img.type = Image.Type.Sliced;
                img.color = i == _selectedControlMode
                    ? new Color(0.15f, 0.35f, 0.65f, 0.95f)
                    : new Color(0.08f, 0.12f, 0.2f, 0.85f);
                var btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.anchoredPosition = new Vector2(0, 30 - i * 110);
                btnRect.sizeDelta = new Vector2(440, 90);

                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(btnGo.transform, false);
                var descText = descGo.AddComponent<Text>();
                descText.text = modeNames[i] + "\n" + modeDescs[i];
                descText.font = GetFont();
                descText.fontSize = i == _selectedControlMode ? 20 : 18;
                descText.color = i == _selectedControlMode
                    ? new Color(0.9f, 0.95f, 1f)
                    : new Color(0.6f, 0.7f, 0.8f);
                descText.alignment = TextAnchor.MiddleCenter;
                var descRect = descText.GetComponent<RectTransform>();
                descRect.anchorMin = Vector2.zero;
                descRect.anchorMax = Vector2.one;
                descRect.offsetMin = new Vector2(16, 4);
                descRect.offsetMax = new Vector2(-16, -4);
                descText.raycastTarget = false;

                var bColors = btn.colors;
                bColors.normalColor = Color.white;
                bColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                bColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                btn.colors = bColors;
                btn.onClick.AddListener(() => SelectControlMode(idx));
            }

            // Close button
            var closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(panelGo.transform, false);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            var closeImg = closeBtnGo.AddComponent<Image>();
            closeImg.sprite = GetRoundedSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.15f, 0.2f, 0.35f, 0.9f);
            var closeRect = closeBtnGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0, 16);
            closeRect.sizeDelta = new Vector2(160, 40);

            var closeLabelGo = new GameObject("Label");
            closeLabelGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLabelText = closeLabelGo.AddComponent<Text>();
            closeLabelText.text = "关闭";
            closeLabelText.font = GetFont();
            closeLabelText.fontSize = 20;
            closeLabelText.color = new Color(0.6f, 0.85f, 1f);
            closeLabelText.alignment = TextAnchor.MiddleCenter;
            var cLabelRect = closeLabelText.GetComponent<RectTransform>();
            cLabelRect.anchorMin = Vector2.zero;
            cLabelRect.anchorMax = Vector2.one;
            cLabelRect.offsetMin = Vector2.zero;
            cLabelRect.offsetMax = Vector2.zero;
            closeLabelText.raycastTarget = false;

            closeBtn.onClick.AddListener(ToggleSettingsPanel);

            _settingsPanel.SetActive(false);
        }

        void SelectControlMode(int mode)
        {
            _selectedControlMode = mode;
            PlayerPrefs.SetInt("FlightControlMode", mode);
            PlayerPrefs.Save();
            Debug.Log($"[Settings] ControlMode saved = {(ControlMode)mode}");

            // Update button visuals
            var panel = _settingsPanel.transform.Find("Panel");
            if (panel == null) return;
            for (int i = 0; i < 2; i++)
            {
                var btn = panel.Find($"ModeBtn_{i}");
                if (btn == null) continue;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = i == _selectedControlMode
                        ? new Color(0.15f, 0.35f, 0.65f, 0.95f)
                        : new Color(0.08f, 0.12f, 0.2f, 0.85f);
                var desc = btn.Find("Desc");
                if (desc != null)
                {
                    var txt = desc.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.fontSize = i == _selectedControlMode ? 20 : 18;
                        txt.color = i == _selectedControlMode
                            ? new Color(0.9f, 0.95f, 1f)
                            : new Color(0.6f, 0.7f, 0.8f);
                    }
                }
            }
        }

        void ToggleSettingsPanel()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(show);
        }

        void CreateShipButton(Transform parent, int index, string label, Vector2 pos, float w, float h)
        {
            var go = new GameObject($"Button_{_shipKeys[index]}");
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = index == _selectedIndex
                ? new Color(0.15f, 0.35f, 0.65f, 0.95f)
                : new Color(0.08f, 0.12f, 0.2f, 0.85f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(w, h);

            // Selection indicator (left accent bar)
            var barGo = new GameObject("Accent");
            barGo.transform.SetParent(go.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = index == _selectedIndex
                ? new Color(0.4f, 0.8f, 1f, 1f)
                : new Color(0.2f, 0.3f, 0.5f, 0.6f);
            var barRect = barImg.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(0, 1);
            barRect.pivot = new Vector2(0, 0.5f);
            barRect.anchoredPosition = new Vector2(8, 0);
            barRect.sizeDelta = new Vector2(4, -16);
            barImg.raycastTarget = false;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.font = GetFont();
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24, 0);
            labelRect.offsetMax = new Vector2(-12, 0);
            labelRect.offsetMin = new Vector2(24, -8);
            labelRect.offsetMax = new Vector2(-12, 8);
            labelText.raycastTarget = false;

            // Hover colors
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            int captured = index;
            btn.onClick.AddListener(() => SelectShip(captured));
            _buttons[index] = btn;
        }

        void SelectShip(int index)
        {
            _selectedIndex = index;
            ConfirmSelection();
        }

        void ConfirmSelection()
        {
            string key = _shipKeys[_selectedIndex];
            PlayerPrefs.SetString("SelectedShip", key);
            PlayerPrefs.Save();
            SceneManager.LoadScene("BattleScene");
        }

        void Update()
        {
            // Close settings panel with Escape
            if (Input.GetKeyDown(KeyCode.Escape) && _settingsPanel != null && _settingsPanel.activeSelf)
            {
                _settingsPanel.SetActive(false);
                return;
            }

            // Block ship selection input when settings panel is open
            if (_settingsPanel != null && _settingsPanel.activeSelf)
                return;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                _selectedIndex = Mathf.Min(_shipNames.Length - 1, _selectedIndex + 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ConfirmSelection();

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                {
                    var img = _buttons[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = i == _selectedIndex
                            ? new Color(0.15f, 0.35f, 0.65f, 0.95f)
                            : new Color(0.08f, 0.12f, 0.2f, 0.85f);
                    }
                    // Update accent bar
                    var bar = _buttons[i].transform.Find("Accent");
                    if (bar != null)
                    {
                        var barImg = bar.GetComponent<Image>();
                        if (barImg != null)
                            barImg.color = i == _selectedIndex
                                ? new Color(0.4f, 0.8f, 1f, 1f)
                                : new Color(0.2f, 0.3f, 0.5f, 0.6f);
                    }
                }
            }
        }
    }
}
