using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// Top-left HUD: ship HP, shield, energy, flight mode, coordinates.
    /// </summary>
    public class StatusHUD
    {
        private HUDManager _mgr;
        private GameObject _panel;

        private Text _hullText;
        private Text _shieldText;
        private Text _energyText;
        private Text _modeText;
        private Text _coordText;
        private Text _allocText;

        private Image _hullBar;
        private Image _shieldBar;
        private Image _energyBar;

        private ShipHealth _health;
        private ShipController _controller;

        public static StatusHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new StatusHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            _health = mgr.health;
            _controller = mgr.controller;

            // Panel: top-left
            _panel = HUDManager.CreatePanel(parent, "StatusPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10, -180), new Vector2(280, -10));
            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = mgr.panelColor;

            float y = 5f;

            // Title
            var title = HUDManager.CreateText(_panel.transform, "Title", "飞船状态", 14, mgr.borderColor);
            SetPos(title.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y));
            y += 22f;

            // Hull bar + text
            _hullBar = CreateBar(_panel.transform, "HullBar", new Color(0.8f, 0.2f, 0.2f, 0.8f), y);
            _hullText = HUDManager.CreateText(_panel.transform, "HullText", "船体 100%", 12, mgr.textColor);
            SetPos(_hullText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y - 16));
            y += 38f;

            // Shield bar + text
            _shieldBar = CreateBar(_panel.transform, "ShieldBar", new Color(0.2f, 0.5f, 1f, 0.8f), y);
            _shieldText = HUDManager.CreateText(_panel.transform, "ShieldText", "护盾 100%", 12, mgr.textColor);
            SetPos(_shieldText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y - 16));
            y += 38f;

            // Energy bar + text
            _energyBar = CreateBar(_panel.transform, "EnergyBar", new Color(0.9f, 0.8f, 0.2f, 0.8f), y);
            _energyText = HUDManager.CreateText(_panel.transform, "EnergyText", "能量 100%", 12, mgr.textColor);
            SetPos(_energyText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y - 16));
            y += 38f;

            // Flight mode
            _modeText = HUDManager.CreateText(_panel.transform, "ModeText", "模式: 标准", 12, mgr.textColor);
            SetPos(_modeText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y));
            y += 18f;

            // Coordinates
            _coordText = HUDManager.CreateText(_panel.transform, "CoordText", "坐标: 0, 0, 0", 11, mgr.textColor);
            SetPos(_coordText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y));

            // Energy allocation text
            _allocText = HUDManager.CreateText(_panel.transform, "AllocText", "武器:50% 护盾:50%", 11, mgr.textColor);
            SetPos(_allocText.rectTransform, new Vector2(0f, 1f), new Vector2(5, -y - 18));
        }

        private Image CreateBar(Transform parent, string name, Color fillColor, float yOffset)
        {
            var bg = HUDManager.CreateImage(parent, name + "_BG", new Color(0.1f, 0.1f, 0.1f, 0.6f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5, -yOffset - 14), new Vector2(-5, -yOffset));

            var fill = HUDManager.CreateImage(parent, name + "_Fill", fillColor,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(0, 0));
            fill.transform.SetParent(bg.transform, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            return fill;
        }

        private static void SetPos(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(260, 16);
        }

        public void UpdateHUD()
        {
            if (_health == null || _controller == null || _controller.stats == null) return;

            float hp = _health.HullPercent;
            float sp = _health.ShieldPercent;
            float ep = _health.EnergyPercent;

            _hullBar.fillAmount = hp;
            _shieldBar.fillAmount = sp;
            _energyBar.fillAmount = ep;

            _hullText.text = $"船体 {hp * 100f:F0}%";
            _shieldText.text = $"护盾 {sp * 100f:F0}%";
            _energyText.text = $"能量 {ep * 100f:F0}%";

            // Color warning
            _hullText.color = hp < 0.25f ? _mgr.warningColor : _mgr.textColor;
            _shieldText.color = sp < 0.25f && sp > 0f ? _mgr.warningColor : _mgr.textColor;

            // Mode
            string modeStr;
            switch (_controller.flightMode)
            {
                case FlightMode.Combat: modeStr = "战斗"; break;
                case FlightMode.Warp: modeStr = "曲速"; break;
                default: modeStr = "标准"; break;
            }
            bool overloading = _health.IsInvulnerable;
            _modeText.text = $"模式: {modeStr}{(overloading ? " [过载]" : "")}";
            _modeText.color = overloading ? _mgr.warningColor : _mgr.textColor;

            // Coordinates
            Vector3 pos = _controller.transform.position;
            _coordText.text = $"坐标: {pos.x:F0}, {pos.y:F0}, {pos.z:F0}";

            // Energy allocation
            float wea = _health.weaponEnergyAllocation * 100f;
            float shd = _health.ShieldEnergyAllocation * 100f;
            _allocText.text = $"武器分配:{wea:F0}% 护盾分配:{shd:F0}%";
        }
    }
}
