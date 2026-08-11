using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace StarTrekCombat
{
    public class GalaxyMap : MonoBehaviour
    {
        private HUDManager _mgr;
        private GameObject _overlay;
        private RectTransform _content;
        private GameObject _shipMarker;
        private GameObject _popup;
        private Text _popupLabel;

        private bool _isOpen;
        public bool IsOpen => _isOpen;

        private Vector2 _panOffset;
        private float _zoom = 1f;

        private Transform _selectedPlanet;
        private float _selectedPlanetRadius;
        private bool _selectedIsSun;
        private bool _markersBuilt;
        private GameObject _systemListPanel;

        private struct MarkerEntry { public GameObject go; public Transform planet; public float radius; public Image img; public bool isSun; }
        private List<MarkerEntry> _markers = new List<MarkerEntry>();

        // Each star system definition for the galaxy map.
        private struct SystemDef { public string name; public string sunName; public int planetCount; public string planetPrefix; }
        private static readonly SystemDef[] StarSystems = new SystemDef[]
        {
            new SystemDef { name="Bajor",     sunName="Bajor_Sun",     planetCount=15, planetPrefix="Bajor" },
            new SystemDef { name="Cardassia", sunName="Cardassia_Sun", planetCount=12, planetPrefix="Cardassia" },
            new SystemDef { name="Chin'toka", sunName="Chintoka_Sun",  planetCount=12, planetPrefix="Chintoka" },
        };
        private static readonly Color[] SystemSunColors =
        {
            new Color(1f, 0.6f, 0.1f, 1f),    // Bajor (orange)
            new Color(1f, 0.3f, 0.2f, 1f),    // Cardassia (red-orange)
            new Color(0.3f, 0.6f, 1f, 1f),     // Chin'toka (blue)
        };

        private static readonly Color PanelColor = new Color(0.02f, 0.04f, 0.08f, 0.95f);
        private static readonly Color PlanetColor = new Color(0.6f, 0.8f, 1f, 0.6f);
        private static readonly Color PlanetSelColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color ShipColor = new Color(0.3f, 1f, 0.4f, 1f);

        // Drag state
        private Vector2 _lastMousePos;
        private bool _isDragging;

        public static GalaxyMap Create(Transform parent, HUDManager mgr)
        {
            var go = new GameObject("GalaxyMap", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<GalaxyMap>();
            map.Init(parent, mgr);
            return map;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;

            _overlay = new GameObject("MapOverlay", typeof(RectTransform), typeof(Image));
            _overlay.transform.SetParent(parent, false);
            var ovrRt = _overlay.GetComponent<RectTransform>();
            ovrRt.anchorMin = Vector2.zero;
            ovrRt.anchorMax = Vector2.one;
            ovrRt.offsetMin = Vector2.zero;
            ovrRt.offsetMax = Vector2.zero;
            var ovrImg = _overlay.GetComponent<Image>();
            ovrImg.color = PanelColor;
            ovrImg.raycastTarget = true;

            // Title
            var title = HUDManager.CreateText(_overlay.transform, "Title", "星系图", 16, new Color(0.3f, 0.5f, 0.9f, 0.8f), TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -6);
            title.rectTransform.sizeDelta = new Vector2(0, 24);
            title.raycastTarget = false;

            var hint = HUDManager.CreateText(_overlay.transform, "Hint", "滚轮缩放 · 拖拽平移 · 点星球导航 · 点恒星曲速 · ESC关闭", 11, new Color(0.5f, 0.6f, 0.7f, 0.5f), TextAnchor.UpperCenter);
            hint.rectTransform.anchorMin = new Vector2(0, 1);
            hint.rectTransform.anchorMax = new Vector2(1, 1);
            hint.rectTransform.pivot = new Vector2(0.5f, 1);
            hint.rectTransform.anchoredPosition = new Vector2(0, -28);
            hint.rectTransform.sizeDelta = new Vector2(0, 16);
            hint.raycastTarget = false;

            // Content layer — IMPORTANT: rendered ON TOP of overlay Image so markers get raycasts
            _content = new GameObject("MapContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(_overlay.transform, false);
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            // Ship marker — triangle shape
            _shipMarker = new GameObject("ShipMarker", typeof(RectTransform), typeof(Image));
            _shipMarker.transform.SetParent(_content, false);
            _shipMarker.GetComponent<Image>().color = ShipColor;
            _shipMarker.GetComponent<Image>().sprite = CreateTriangleSprite();
            _shipMarker.GetComponent<Image>().raycastTarget = false;
            var smRt = _shipMarker.GetComponent<RectTransform>();
            smRt.sizeDelta = new Vector2(18, 18);
            smRt.anchorMin = new Vector2(0.5f, 0.5f);
            smRt.anchorMax = new Vector2(0.5f, 0.5f);
            // Always keep the ship marker drawn on top of every planet marker.
            _shipMarker.transform.SetAsLastSibling();

            // Close button
            var closeBtn = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(_overlay.transform, false);
            var cbRt = closeBtn.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(1, 1);
            cbRt.anchorMax = new Vector2(1, 1);
            cbRt.pivot = new Vector2(1, 1);
            cbRt.anchoredPosition = new Vector2(-8, -8);
            cbRt.sizeDelta = new Vector2(28, 28);
            var cbImg = closeBtn.GetComponent<Image>();
            cbImg.sprite = HUDManager.GetFilledRoundedSprite(14f);
            cbImg.type = Image.Type.Sliced;
            cbImg.color = new Color(0.3f, 0.1f, 0.1f, 0.85f);
            var cbLabel = HUDManager.CreateText(closeBtn.transform, "Lbl", "✕", 14, new Color(1f, 0.6f, 0.5f, 0.9f), TextAnchor.MiddleCenter);
            cbLabel.rectTransform.anchorMin = Vector2.zero;
            cbLabel.rectTransform.anchorMax = Vector2.one;
            cbLabel.rectTransform.offsetMin = Vector2.zero;
            cbLabel.rectTransform.offsetMax = Vector2.zero;
            cbLabel.raycastTarget = false;
            closeBtn.GetComponent<Button>().onClick.AddListener(Close);

            // Star system selector button — top-left corner
            var sysBtn = new GameObject("SystemSelectBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            sysBtn.transform.SetParent(_overlay.transform, false);
            var sbRt = sysBtn.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(0, 1);
            sbRt.anchorMax = new Vector2(0, 1);
            sbRt.pivot = new Vector2(0, 1);
            sbRt.anchoredPosition = new Vector2(8, -8);
            sbRt.sizeDelta = new Vector2(90, 24);
            var sbImg = sysBtn.GetComponent<Image>();
            sbImg.sprite = HUDManager.GetFilledRoundedSprite(6f);
            sbImg.type = Image.Type.Sliced;
            sbImg.color = new Color(0.08f, 0.15f, 0.3f, 0.9f);
            var sbLabel = HUDManager.CreateText(sysBtn.transform, "Lbl", "选择星系", 11, new Color(0.4f, 0.7f, 1f, 0.9f), TextAnchor.MiddleCenter);
            sbLabel.rectTransform.anchorMin = Vector2.zero;
            sbLabel.rectTransform.anchorMax = Vector2.one;
            sbLabel.rectTransform.offsetMin = Vector2.zero;
            sbLabel.rectTransform.offsetMax = Vector2.zero;
            sbLabel.raycastTarget = false;
            sysBtn.GetComponent<Button>().onClick.AddListener(ToggleSystemList);

            _overlay.SetActive(false);
        }

        public void Toggle()
        {
            if (_isOpen) Close(); else Open();
        }

        public void Open()
        {
            _isOpen = true;
            _overlay.SetActive(true);
            ShipInput.SuppressInput = true;
            ShipInput.UnlockCursor();
            _markersBuilt = false;
        }

        public void Close()
        {
            _isOpen = false;
            _overlay.SetActive(false);
            ShipInput.SuppressInput = false;
            _selectedPlanet = null;
            if (_popup != null) _popup.SetActive(false);
            if (_systemListPanel != null) _systemListPanel.SetActive(false);
        }

        private void ToggleSystemList()
        {
            if (_systemListPanel == null) BuildSystemListPanel();
            _systemListPanel.SetActive(!_systemListPanel.activeSelf);
        }

        private void BuildSystemListPanel()
        {
            _systemListPanel = new GameObject("SystemListPanel", typeof(RectTransform), typeof(Image));
            _systemListPanel.transform.SetParent(_overlay.transform, false);
            var rt = _systemListPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(8, -38);
            rt.sizeDelta = new Vector2(120, 26 + StarSystems.Length * 26);
            var img = _systemListPanel.GetComponent<Image>();
            img.sprite = HUDManager.GetFilledRoundedSprite(6f);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.04f, 0.08f, 0.16f, 0.96f);

            var hdr = HUDManager.CreateText(_systemListPanel.transform, "Hdr", "星系列表", 11, new Color(0.5f, 0.7f, 1f, 0.7f), TextAnchor.MiddleCenter);
            hdr.rectTransform.anchorMin = new Vector2(0, 1);
            hdr.rectTransform.anchorMax = new Vector2(1, 1);
            hdr.rectTransform.pivot = new Vector2(0.5f, 1);
            hdr.rectTransform.anchoredPosition = new Vector2(0, -3);
            hdr.rectTransform.sizeDelta = new Vector2(0, 16);
            hdr.raycastTarget = false;

            for (int i = 0; i < StarSystems.Length; i++)
            {
                var sys = StarSystems[i];
                var btn = new GameObject("Sys_" + sys.name, typeof(RectTransform), typeof(Image), typeof(Button));
                btn.transform.SetParent(_systemListPanel.transform, false);
                var bRt = btn.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0, 1);
                bRt.anchorMax = new Vector2(1, 1);
                bRt.pivot = new Vector2(0.5f, 1);
                bRt.anchoredPosition = new Vector2(0, -22 - i * 26);
                bRt.sizeDelta = new Vector2(-8, 22);
                var bImg = btn.GetComponent<Image>();
                bImg.sprite = HUDManager.GetFilledRoundedSprite(4f);
                bImg.type = Image.Type.Sliced;
                bImg.color = new Color(SystemSunColors[i].r * 0.3f, SystemSunColors[i].g * 0.3f, SystemSunColors[i].b * 0.3f, 0.9f);
                var bLbl = HUDManager.CreateText(btn.transform, "Lbl", sys.name, 12, SystemSunColors[i], TextAnchor.MiddleCenter);
                bLbl.rectTransform.anchorMin = Vector2.zero;
                bLbl.rectTransform.anchorMax = Vector2.one;
                bLbl.rectTransform.offsetMin = Vector2.zero;
                bLbl.rectTransform.offsetMax = Vector2.zero;
                bLbl.raycastTarget = false;

                int captured = i;
                btn.GetComponent<Button>().onClick.AddListener(() => CenterOnSystem(captured));
            }
        }

        private void CenterOnSystem(int systemIndex)
        {
            var sys = StarSystems[systemIndex];
            var sun = GameObject.Find(sys.sunName);
            if (sun == null || _mgr == null || _mgr.controller == null) return;

            _selectedPlanet = sun.transform;
            _selectedPlanetRadius = 2500f;
            _selectedIsSun = true;

            // Pan map so this sun is at screen center
            float scale = 0.015f * _zoom;
            Vector2 shipMap = WorldToMap(_mgr.controller.transform.position, scale);
            Vector2 targetMap = WorldToMap(sun.transform.position, scale);
            _panOffset = shipMap - targetMap;

            // Hide the list panel after selection
            if (_systemListPanel != null) _systemListPanel.SetActive(false);

            // Show popup with warp option
            ShowPopup(sys.sunName, sun.transform);
        }

        void Update()
        {
            if (!_isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.G))
            {
                Close();
                return;
            }

            // Build markers once (not every frame!)
            if (!_markersBuilt)
            {
                BuildMarkers();
                _markersBuilt = true;
            }

            UpdatePositions();
            HandleDragZoom();
        }

        private void BuildMarkers()
        {
            // Clear old
            foreach (var m in _markers)
                if (m.go != null) Destroy(m.go);
            _markers.Clear();

            // All three star systems
            for (int s = 0; s < StarSystems.Length; s++)
            {
                var sys = StarSystems[s];

                // Sun — clickable for interstellar warp navigation
                var sun = GameObject.Find(sys.sunName);
                if (sun != null)
                {
                    AddMarker(sys.sunName, sun.transform, 2500f, SystemSunColors[s], true);

                    // Planet label color per system (slightly tinted)
                    var pc = new Color(0.6f + SystemSunColors[s].r * 0.2f, 0.7f + SystemSunColors[s].g * 0.1f, 1f, 0.6f);

                    for (int i = 1; i <= sys.planetCount; i++)
                    {
                        var planet = GameObject.Find($"{sys.planetPrefix}{i}");
                        if (planet == null) continue;
                        AddMarker($"{sys.planetPrefix}{i}", planet.transform, planet.transform.localScale.x, pc, false);
                    }
                }
            }

            // Space stations — clickable auto-warp targets, distinct cyan color.
            foreach (var st in FindObjectsOfType<StarbaseStation>())
            {
                if (st == null) continue;
                float stationRadius = Mathf.Max(0.1f, st.transform.lossyScale.magnitude * 0.5f);
                AddMarker(st.name, st.transform, stationRadius, new Color(0.2f, 0.9f, 0.9f), false);
            }
        }

        private void AddMarker(string label, Transform planet, float radius, Color color, bool isSun)
        {
            var marker = new GameObject("MK_" + label, typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(_content, false);
            var rt = marker.GetComponent<RectTransform>();
            rt.sizeDelta = isSun ? new Vector2(40, 40) : new Vector2(28, 28); // generous hit area
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = marker.GetComponent<Image>();
            img.sprite = CreateCircleSprite();
            img.color = color;
            img.raycastTarget = true;

            // Inner dot (visual) — sun gets a larger dot
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(marker.transform, false);
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = isSun ? new Vector2(14, 14) : new Vector2(8, 8);
            dot.GetComponent<Image>().color = color;
            dot.GetComponent<Image>().raycastTarget = false;

            // Sun gets a subtle glow ring
            if (isSun)
            {
                var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
                glow.transform.SetParent(marker.transform, false);
                var gr = glow.GetComponent<RectTransform>();
                gr.anchorMin = new Vector2(0.5f, 0.5f);
                gr.anchorMax = new Vector2(0.5f, 0.5f);
                gr.sizeDelta = new Vector2(30, 30);
                var gi = glow.GetComponent<Image>();
                gi.sprite = CreateCircleSprite();
                gi.color = new Color(color.r, color.g, color.b, 0.2f);
                gi.raycastTarget = false;
            }

            // Label
            var lblColor = isSun ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.7f, 0.8f, 1f, 0.7f);
            var lblSize = isSun ? 12 : 10;
            var lbl = HUDManager.CreateText(marker.transform, "Lbl", label, lblSize, lblColor, TextAnchor.UpperCenter);
            lbl.rectTransform.anchorMin = new Vector2(0.5f, 0);
            lbl.rectTransform.anchorMax = new Vector2(0.5f, 0);
            lbl.rectTransform.pivot = new Vector2(0.5f, 1);
            lbl.rectTransform.anchoredPosition = new Vector2(0, -2);
            lbl.rectTransform.sizeDelta = new Vector2(80, 14);
            lbl.raycastTarget = false;

            // Button — survives because marker is NOT recreated every frame
            var btn = marker.AddComponent<Button>();
            var capturedPlanet = planet;
            var capturedRadius = radius;
            var capturedImg = img;
            var capturedIsSun = isSun;
            btn.onClick.AddListener(() =>
            {
                _selectedPlanet = capturedPlanet;
                _selectedPlanetRadius = capturedRadius;
                _selectedIsSun = capturedIsSun;

                // Color feedback
                foreach (var m in _markers)
                    if (m.img != null) m.img.color = m.planet == capturedPlanet ? PlanetSelColor : (m.isSun ? GetSunColor(m.planet.name) : PlanetColor);

                ShowPopup(label, capturedPlanet);
            });

            _markers.Add(new MarkerEntry { go = marker, planet = planet, radius = radius, img = img, isSun = isSun });
        }

        private Color GetSunColor(string sunName)
        {
            for (int i = 0; i < StarSystems.Length; i++)
                if (StarSystems[i].sunName == sunName) return SystemSunColors[i];
            return new Color(1f, 0.6f, 0.1f, 1f);
        }

        private void UpdatePositions()
        {
            if (_mgr == null || _mgr.controller == null) return;
            var ship = _mgr.controller.transform;
            float scale = 0.015f * _zoom;
            Vector2 shipMap = WorldToMap(ship.position, scale);

            // Ship marker — moves with pan offset just like planets
            _shipMarker.GetComponent<RectTransform>().anchoredPosition = _panOffset;
            _shipMarker.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, 0, -ship.eulerAngles.y);

            foreach (var m in _markers)
            {
                if (m.go == null || m.planet == null) continue;
                var pos = WorldToMap(m.planet.position, scale) - shipMap + _panOffset;
                m.go.GetComponent<RectTransform>().anchoredPosition = pos;
            }

            // Popup follows selected planet
            if (_selectedPlanet != null && _popup != null && _popup.activeSelf)
            {
                var pos = WorldToMap(_selectedPlanet.position, scale) - shipMap + _panOffset;
                _popup.GetComponent<RectTransform>().anchoredPosition = pos + new Vector2(0, -25);
            }
        }

        private void HandleDragZoom()
        {
            // Drag
            if (Input.GetMouseButtonDown(0))
            {
                _lastMousePos = Input.mousePosition;
                _isDragging = true;
            }
            if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _lastMousePos;
                // Only pan if not clicking a button (simple check: if delta is large it's a drag)
                if (delta.magnitude > 2f)
                    _panOffset += delta;
                _lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
                _isDragging = false;

            // Zoom — anchored to the mouse cursor so the point under the cursor stays put.
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float oldScale = 0.015f * _zoom;
                float oldZoom = _zoom;
                _zoom = Mathf.Clamp(_zoom * (1f + scroll * 0.1f), 0.1f, 20f);
                float newScale = 0.015f * _zoom;
                if (Mathf.Abs(newScale - oldScale) < 0.0001f) return;

                // Ship's screen offset from canvas center is _panOffset (ship marker sits there).
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 mouseRel = (Vector2)Input.mousePosition - center; // offset from screen center

                // World coords of the point currently under the cursor (mix: only x/z used, y ignored).
                // screenOffset = (world - shipWorld) * oldScale + panOffset  == mouseRel
                Vector2 shipXY = new Vector2(_mgr != null && _mgr.controller != null ? _mgr.controller.transform.position.x : 0f,
                                              _mgr != null && _mgr.controller != null ? _mgr.controller.transform.position.z : 0f);
                Vector2 pwXY = shipXY + (mouseRel - _panOffset) / oldScale;

                // After zoom, keep that same world point under the cursor:
                // mouseRel == (world - ship) * newScale + panOffset'
                Vector2 newPan = mouseRel - (pwXY - shipXY) * newScale;
                _panOffset = newPan;
            }
        }

        private void ShowPopup(string label, Transform planet)
        {
            if (_popup == null)
            {
                _popup = new GameObject("Popup", typeof(RectTransform), typeof(Image));
                _popup.transform.SetParent(_content, false);
                var rt = _popup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(130, 44);
                var img = _popup.GetComponent<Image>();
                img.sprite = HUDManager.GetFilledRoundedSprite(6f);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.05f, 0.12f, 0.25f, 0.95f);
                img.raycastTarget = false;

                _popupLabel = HUDManager.CreateText(_popup.transform, "Lbl", "", 12, new Color(0.8f, 0.9f, 1f, 0.9f), TextAnchor.UpperCenter);
                _popupLabel.rectTransform.anchorMin = new Vector2(0, 1);
                _popupLabel.rectTransform.anchorMax = new Vector2(1, 1);
                _popupLabel.rectTransform.pivot = new Vector2(0.5f, 1);
                _popupLabel.rectTransform.anchoredPosition = new Vector2(0, -3);
                _popupLabel.rectTransform.sizeDelta = new Vector2(0, 14);
                _popupLabel.raycastTarget = false;

                var wb = new GameObject("WarpBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                wb.transform.SetParent(_popup.transform, false);
                var wbRt = wb.GetComponent<RectTransform>();
                wbRt.anchorMin = new Vector2(0.5f, 0);
                wbRt.anchorMax = new Vector2(0.5f, 0);
                wbRt.pivot = new Vector2(0.5f, 0);
                wbRt.anchoredPosition = new Vector2(0, 3);
                wbRt.sizeDelta = new Vector2(116, 18);
                var wbImg = wb.GetComponent<Image>();
                wbImg.sprite = HUDManager.GetFilledRoundedSprite(4f);
                wbImg.type = Image.Type.Sliced;
                wbImg.color = new Color(0.1f, 0.3f, 0.15f, 0.9f);
                var wbLabel = HUDManager.CreateText(wb.transform, "Lbl", "自动曲速导航", 11, new Color(0.5f, 1f, 0.6f, 1f), TextAnchor.MiddleCenter);
                wbLabel.rectTransform.anchorMin = Vector2.zero;
                wbLabel.rectTransform.anchorMax = Vector2.one;
                wbLabel.rectTransform.offsetMin = Vector2.zero;
                wbLabel.rectTransform.offsetMax = Vector2.zero;
                wbLabel.raycastTarget = false;
                wb.GetComponent<Button>().onClick.AddListener(OnWarpClicked);

                // Center button — pans the map to center the selected object
                var cb = new GameObject("CenterBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                cb.transform.SetParent(_popup.transform, false);
                var cbRt = cb.GetComponent<RectTransform>();
                cbRt.anchorMin = new Vector2(0.5f, 0);
                cbRt.anchorMax = new Vector2(0.5f, 0);
                cbRt.pivot = new Vector2(0.5f, 0);
                cbRt.anchoredPosition = new Vector2(0, 24);
                cbRt.sizeDelta = new Vector2(116, 18);
                var cbImg = cb.GetComponent<Image>();
                cbImg.sprite = HUDManager.GetFilledRoundedSprite(4f);
                cbImg.type = Image.Type.Sliced;
                cbImg.color = new Color(0.1f, 0.2f, 0.35f, 0.9f);
                var cbLabel = HUDManager.CreateText(cb.transform, "Lbl", "居中", 11, new Color(0.4f, 0.7f, 1f, 1f), TextAnchor.MiddleCenter);
                cbLabel.rectTransform.anchorMin = Vector2.zero;
                cbLabel.rectTransform.anchorMax = Vector2.one;
                cbLabel.rectTransform.offsetMin = Vector2.zero;
                cbLabel.rectTransform.offsetMax = Vector2.zero;
                cbLabel.raycastTarget = false;
                cb.GetComponent<Button>().onClick.AddListener(OnCenterClicked);

                // Adjust popup height to fit both buttons
                _popup.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 68);
            }

            _popup.SetActive(true);
            _popupLabel.text = label;
        }

        private void OnWarpClicked()
        {
            if (_selectedPlanet == null || _mgr.controller == null) return;
            var nav = _mgr.controller.GetComponent<AutoWarpNavigator>();
            if (nav == null) nav = _mgr.controller.gameObject.AddComponent<AutoWarpNavigator>();
            nav.NavigateTo(_selectedPlanet, _selectedPlanetRadius);
            Close();
        }

        private void OnCenterClicked()
        {
            if (_selectedPlanet == null || _mgr == null || _mgr.controller == null) return;
            // Pan map so the selected object appears at screen center.
            float scale = 0.015f * _zoom;
            Vector2 shipMap = WorldToMap(_mgr.controller.transform.position, scale);
            Vector2 targetMap = WorldToMap(_selectedPlanet.position, scale);
            // We want targetMap - shipMap + _panOffset == 0 (screen center)
            _panOffset = shipMap - targetMap;
        }

        private Vector2 WorldToMap(Vector3 worldPos, float scale)
            => new Vector2(worldPos.x * scale, worldPos.z * scale);

        private static Sprite _triangleSprite;
        private static Sprite CreateTriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            // Triangle: top-center, bottom-left, bottom-right
            Vector2 top = new Vector2(ts / 2f, ts * 0.9f);
            Vector2 bl = new Vector2(ts * 0.15f, ts * 0.1f);
            Vector2 br = new Vector2(ts * 0.85f, ts * 0.1f);
            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    var p = new Vector2(x, y);
                    // Barycentric technique for point-in-triangle
                    float d1 = Sign(p, top, bl);
                    float d2 = Sign(p, bl, br);
                    float d3 = Sign(p, br, top);
                    bool inside = (d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0);
                    px[y * ts + x] = inside ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), ts);
            return _triangleSprite;
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        private static Sprite _circleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            int ts = 64;
            var tex = new Texture2D(ts, ts, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[ts * ts];
            Vector2 center = new Vector2(ts / 2f, ts / 2f);
            float r = ts / 2f - 1f;
            for (int y = 0; y < ts; y++)
            {
                for (int x = 0; x < ts; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    px[y * ts + x] = d <= r ? Color.white : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, ts, ts), new Vector2(0.5f, 0.5f), ts);
            return _circleSprite;
        }

        void OnDestroy()
        {
            if (_isOpen) ShipInput.SuppressInput = false;
        }
    }
}
