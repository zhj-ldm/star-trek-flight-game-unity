using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Global combat log: scrolling messages that appear then fade after 1.5s.
    /// Max 5 lines visible.
    /// </summary>
    public class CombatLogHUD
    {
        private HUDManager _mgr;
        private GameObject _panel;
        private RectTransform _textContainer;
        private Queue<GameObject> _lines = new Queue<GameObject>();
        private int _maxLines = 5;

        private struct PendingMessage
        {
            public string text;
            public Color color;
            public float time;
        }

        private List<PendingMessage> _pending = new List<PendingMessage>();

        // Message display duration before fade starts
        private const float _displayDuration = 1.5f;
        private const float _fadeDuration = 0.5f;

        public static CombatLogHUD Create(Transform parent, HUDManager mgr)
        {
            var hud = new CombatLogHUD();
            hud.Init(parent, mgr);
            return hud;
        }

        private void Init(Transform parent, HUDManager mgr)
        {
            _mgr = mgr;

            _panel = HUDManager.CreatePanel(parent, "CombatLogPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200, -30), new Vector2(200, -140));
            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = new Color(mgr.panelColor.r, mgr.panelColor.g, mgr.panelColor.b, 0.4f);

            var tcObj = new GameObject("TextContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            tcObj.transform.SetParent(_panel.transform, false);
            _textContainer = tcObj.GetComponent<RectTransform>();
            _textContainer.anchorMin = new Vector2(0f, 1f);
            _textContainer.anchorMax = new Vector2(1f, 1f);
            _textContainer.offsetMin = new Vector2(5, -110);
            _textContainer.offsetMax = new Vector2(-5, -2);

            var vlg = tcObj.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            if (mgr.health != null)
            {
                mgr.health.OnDamaged += OnDamaged;
                mgr.health.OnShipDestroyed += OnShipDestroyed;
                mgr.health.OnShieldBroken += OnShieldBroken;
            }
        }

        public void AddMessage(string msg, Color color)
        {
            _pending.Add(new PendingMessage { text = msg, color = color, time = Time.time });
        }

        private void OnDamaged(float amount, DamageType type)
        {
            string dmgType = type == DamageType.Energy ? "能量" : type == DamageType.Explosive ? "爆炸" : type == DamageType.Ion ? "离子" : "动能";
            AddMessage($"< 船体受损 -{amount:F0} [{dmgType}]", _mgr.warningColor);
        }

        private void OnShieldBroken()
        {
            AddMessage("!! 护盾破碎 !!", new Color(1f, 0.3f, 0.2f, 1f));
        }

        private void OnShipDestroyed()
        {
            AddMessage("!!! 飞船被摧毁 !!!", Color.red);
        }

        private Dictionary<GameObject, float> _lineCreationTime = new Dictionary<GameObject, float>();

        public void UpdateHUD()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time - _pending[i].time >= 0.05f)
                {
                    AddLine(_pending[i].text, _pending[i].color);
                    _pending.RemoveAt(i);
                }
            }

            // Fade out lines after displayDuration + fadeDuration
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                var line = _lines.ToArray()[i];
                if (line == null) continue;

                var text = line.GetComponent<Text>();
                if (text == null) continue;

                float age = Time.time - _lineCreationTime[line];

                if (age > _displayDuration)
                {
                    float fadeT = (age - _displayDuration) / _fadeDuration;
                    Color c = text.color;
                    c.a = Mathf.Max(0f, 1f - fadeT);
                    text.color = c;

                    if (fadeT >= 1f)
                    {
                        // Remove from queue and destroy
                        _lineCreationTime.Remove(line);
                        Object.Destroy(line);
                        // Rebuild queue without this item
                        var newQueue = new Queue<GameObject>();
                        foreach (var item in _lines)
                            if (item != line && item != null)
                                newQueue.Enqueue(item);
                        _lines = newQueue;
                    }
                }
            }
        }

        private void AddLine(string msg, Color color)
        {
            var text = HUDManager.CreateText(_textContainer, "LogLine", msg, 14, color, TextAnchor.UpperCenter, FontStyle.Bold);

            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 20;
            le.flexibleWidth = 1;

            _lines.Enqueue(text.gameObject);
            _lineCreationTime[text.gameObject] = Time.time;

            while (_lines.Count > _maxLines)
            {
                var old = _lines.Dequeue();
                if (old != null)
                {
                    _lineCreationTime.Remove(old);
                    Object.Destroy(old);
                }
            }
        }
    }
}
