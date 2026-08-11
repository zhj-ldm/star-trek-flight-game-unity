using UnityEngine;
using UnityEngine.UI;

namespace StarTrekCombat
{
    /// <summary>
    /// Bottom-right HUD: compact weapon status dot + hull/shield bars.
    /// </summary>
    public class WeaponHUD
    {
        private HUDManager _mgr;
        private GameObject _panel;

        private Image _statusDot;       // green=ready, orange=recharging, red=locked
        private Image _shieldDot;       // blue=shield on, gray=shield off
        private Text _weaponText;        // short status text

        private Image _hullBar;
        private Image _shieldBar;
        private Text _hullText;
        private Text _shieldText;

        private ShipWeaponManager _weapons;
        private ShipHealth _health;

        public static WeaponHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new WeaponHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;
            _weapons = mgr.weapons;
            _health = mgr.health;

            // Compact panel: bottom-right
            _panel = HUDManager.CreatePanel(parent, "WeaponPanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-180, 10), new Vector2(-10, 120));
            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = mgr.panelColor;

            float y = 5f;

            // Status dot + weapon text on same row
            _statusDot = HUDManager.CreateImage(_panel.transform, "StatusDot",
                mgr.goodColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8, -y - 6), new Vector2(18, -y - 16));

            _weaponText = HUDManager.CreateText(_panel.transform, "WeaponText", "就绪", 12, mgr.textColor);
            _weaponText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _weaponText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _weaponText.rectTransform.anchoredPosition = new Vector2(25, -y);
            _weaponText.rectTransform.sizeDelta = new Vector2(-30, 16);

            // Shield on/off indicator dot (next to status dot)
            _shieldDot = HUDManager.CreateImage(_panel.transform, "ShieldDot",
                new Color(0.3f, 0.6f, 1f, 0.9f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18, -y - 6), new Vector2(-8, -y - 16));
            y += 22f;

            // Hull bar
            _hullBar = CreateBar(_panel.transform, "Hull", new Color(0.8f, 0.2f, 0.2f, 0.8f), y);
            _hullText = HUDManager.CreateText(_panel.transform, "HullText", "船体 100%", 11, mgr.textColor);
            SetPos(_hullText.rectTransform, y + 14);
            y += 36f;

            // Shield bar
            _shieldBar = CreateBar(_panel.transform, "Shield", new Color(0.2f, 0.5f, 1f, 0.8f), y);
            _shieldText = HUDManager.CreateText(_panel.transform, "ShieldText", "护盾 100%", 11, mgr.textColor);
            SetPos(_shieldText.rectTransform, y + 14);
        }

        private Image CreateBar(Transform parent, string name, Color fillColor, float yOffset)
        {
            var bg = HUDManager.CreateImage(parent, name + "_BG", new Color(0.1f, 0.1f, 0.1f, 0.6f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8, -yOffset - 8), new Vector2(-8, -yOffset));

            var fill = HUDManager.CreateImage(parent, name + "_Fill", fillColor,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            fill.transform.SetParent(bg.transform, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            return fill;
        }

        private static void SetPos(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(10, -y);
            rt.sizeDelta = new Vector2(-20, 14);
        }

        public void UpdateHUD()
        {
            // Refresh references (may change when switching ships)
            _weapons = _mgr.weapons;
            _health = _mgr.health;

            if (_weapons == null || _health == null) return;

            // Status dot: green=ready, orange=recharging/firing, red=locked
            bool isFiring = _weapons.IsPhaserFiring;
            bool isRecharging = _weapons.IsPhaserRecharging;
            bool locked = _weapons.weaponsLocked;

            if (locked)
            {
                _statusDot.color = _mgr.warningColor;
                _weaponText.text = "被干扰";
                _weaponText.color = _mgr.warningColor;
            }
            else if (isFiring)
            {
                _statusDot.color = new Color(1f, 0.6f, 0.2f, 1f);
                _weaponText.text = $"发射中 {_weapons.PhaserFireProgress * 100f:F0}%";
                _weaponText.color = _mgr.textColor;
            }
            else if (isRecharging)
            {
                _statusDot.color = new Color(1f, 0.6f, 0.2f, 1f);
                _weaponText.text = $"充能中 {_weapons.PhaserRechargeProgress * 100f:F0}%";
                _weaponText.color = _mgr.warningColor;
            }
            else
            {
                _statusDot.color = _mgr.goodColor;
                _weaponText.text = "就绪";
                _weaponText.color = _mgr.goodColor;
            }

            // Hull/shield bars
            float hp = _health.HullPercent;
            float sp = _health.ShieldPercent;
            _hullBar.fillAmount = hp;
            _shieldBar.fillAmount = sp;
            _hullText.text = $"船体 {hp * 100f:F0}%";

            // Shield on/off dot: blue=on, gray=off
            bool shieldOn = _health.isShieldOn && _health.IsShieldActive;
            _shieldDot.color = shieldOn
                ? new Color(0.3f, 0.6f, 1f, 0.9f)
                : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            _shieldText.text = $"护盾 {sp * 100f:F0}%";
            _hullText.color = hp < 0.25f ? _mgr.warningColor : _mgr.textColor;
            _shieldText.color = sp < 0.25f && sp > 0f ? _mgr.warningColor : _mgr.textColor;
        }
    }
}
