using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// 抖动诊断面板 — 左下角可点击按钮 + 实时数据显示。
    /// A) 角速度死区开关
    /// B) 日志输出
    /// C) Doppler=0
    /// (Rigidbody已移除，不再需要插值开关)
    /// </summary>
    public class JitterDiagPanel
    {
        private HUDManager _mgr;
        private ShipController _controller;

        private bool _logging = true;
        private bool _dopplerOff;

        private GameObject _panel;
        private Text _btnLogging;
        private Text _btnDopplerOff;
        private Text _dataText;

        private int _frameCount;
        private const int LogInterval = 15;

        private static readonly Color OnColor = new Color(0.2f, 0.9f, 0.4f, 0.95f);
        private static readonly Color OffColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        private static readonly Color PanelBg = new Color(0.05f, 0.1f, 0.15f, 0.8f);

        public static JitterDiagPanel Create(Transform parent, HUDManager mgr)
        {
            var panel = new JitterDiagPanel();
            panel.Init(parent, mgr);
            return panel;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;

            float panelW = 200f;
            float panelH = 120f;

            _panel = HUDManager.CreatePanel(parent, "JitterDiagPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(5, 5), new Vector2(panelW, panelH));
            _panel.GetComponent<Image>().color = PanelBg;

            var title = HUDManager.CreateText(_panel.transform, "Title", "诊断", 12,
                new Color(1f, 0.8f, 0.3f, 0.95f), TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(5, -3);
            title.rectTransform.sizeDelta = new Vector2(-10, 18);

            float y = -22f;
            float btnH = 22f;
            float gap = 2f;

            _btnDopplerOff = CreateToggleButton(_panel.transform, "BtnDopplerOff",
                "Doppler=0", y, btnH);
            BindButtonClick(_btnDopplerOff, () => ToggleDopplerOff());
            y -= btnH + gap;

            _btnLogging = CreateToggleButton(_panel.transform, "BtnLogging",
                "日志输出", y, btnH);
            BindButtonClick(_btnLogging, () => ToggleLogging());
            y -= btnH + gap + 3;

            _dataText = HUDManager.CreateText(_panel.transform, "DataText", "", 10,
                new Color(0.7f, 0.85f, 1f, 0.9f));
            _dataText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _dataText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _dataText.rectTransform.anchoredPosition = new Vector2(5, 3);
            _dataText.rectTransform.sizeDelta = new Vector2(-10, 40);
            _dataText.alignment = TextAnchor.LowerLeft;
            _dataText.horizontalOverflow = HorizontalWrapMode.Overflow;

            UpdateButtonColors();
        }

        private Text CreateToggleButton(Transform parent, string name, string label, float y, float h)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(5, y);
            rt.sizeDelta = new Vector2(-10, h);
            btnObj.GetComponent<Image>().color = OffColor;

            var txt = HUDManager.CreateText(btnObj.transform, "Label", label, 11, Color.white,
                TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;

            return txt;
        }

        private void BindButtonClick(Text btnText, System.Action action)
        {
            var btn = btnText.GetComponentInParent<Button>();
            if (btn != null) btn.onClick.AddListener(() => action());
        }

        private void UpdateButtonColors()
        {
            UpdateBtnColor(_btnDopplerOff, _dopplerOff);
            UpdateBtnColor(_btnLogging, _logging);
        }

        private void UpdateBtnColor(Text btnText, bool on)
        {
            var img = btnText.GetComponentInParent<Image>();
            if (img != null) img.color = on ? OnColor : OffColor;
            if (btnText != null)
            {
                string baseLabel = btnText.text;
                int circleIdx = baseLabel.LastIndexOf(' ');
                if (circleIdx > 0) baseLabel = baseLabel.Substring(0, circleIdx);
                btnText.text = baseLabel + (on ? " ●" : " ○");
            }
        }

        private void ToggleDopplerOff()
        {
            _dopplerOff = !_dopplerOff;
            UpdateBtnColor(_btnDopplerOff, _dopplerOff);
            Debug.Log($"[Diag] Doppler=0: {(_dopplerOff ? "ON" : "OFF")}");
        }

        private void ToggleLogging()
        {
            _logging = !_logging;
            UpdateBtnColor(_btnLogging, _logging);
            Debug.Log($"[Diag] 日志输出: {(_logging ? "ON" : "OFF")}");
        }

        public void UpdateHUD()
        {
            _controller = _mgr.controller;
            if (_controller == null) return;

            // Doppler fix
            if (_dopplerOff)
            {
                var sources = _controller.GetComponents<AudioSource>();
                foreach (var src in sources)
                {
                    if (src.dopplerLevel > 0f) src.dopplerLevel = 0f;
                    if (src.spatialBlend > 0f) src.spatialBlend = 0f;
                }
            }

            // Data display
            float avMag = _controller.angularVelocity.magnitude * Mathf.Rad2Deg;
            float lvMag = _controller.currentSpeed;
            string status = avMag < 0.01f ? "ZERO" : "ACTIVE";

            _dataText.text = $"角速度: {avMag:F4}°/s [{status}]\n速度: {lvMag:F1} m/s";

            if (_logging)
            {
                _frameCount++;
                if (_frameCount >= LogInterval)
                {
                    _frameCount = 0;
                    Debug.Log($"[Diag] angVel={avMag:F4}°/s [{status}] | linVel={lvMag:F2} m/s | stab={_controller.autoStabilize} | fullStop={_controller.fullStop}");
                }
            }
        }
    }
}
