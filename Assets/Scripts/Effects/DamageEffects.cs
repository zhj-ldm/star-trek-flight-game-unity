using UnityEngine;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Visual damage effects — uses Explosion3D for destruction.
    /// Damage-level smoke/fire/energy replaced with simple light glow (no particle squares).
    /// </summary>
    public class DamageEffects : MonoBehaviour
    {
        [Header("Effect Settings")]
        public float smokeEmissionLight = 5f;
        public float smokeEmissionHeavy = 20f;
        public float fireEmissionHeavy = 10f;
        public float energyLeakEmission = 8f;

        [Header("Destruction")]
        public float destructionExplosionRadius = 10f;
        public int destructionDebrisCount = 20;
        public float destructionDebrisSpeed = 10f;
        public float destructionDuration = 2f;

        [Header("Colors")]
        public Color smokeColor = new Color(0.2f, 0.15f, 0.1f, 0.5f);
        public Color fireColor = new Color(1f, 0.2f, 0.05f, 0.8f);
        public Color energyColor = new Color(1f, 0.3f, 0.1f, 0.7f);

        private Dictionary<ModuleType, GameObject> _effectObjects = new Dictionary<ModuleType, GameObject>();
        private bool _isDestroyed;

        /// <summary>Called by ModularDamageSystem when a module's damage level changes.</summary>
        public void OnModuleDamaged(ShipModule module, int damageLevel)
        {
            if (_isDestroyed) return;

            // Get or create light glow for this module
            if (!_effectObjects.ContainsKey(module.moduleType))
            {
                var obj = CreateEffectObject(module);
                if (obj != null)
                    _effectObjects[module.moduleType] = obj;
            }

            GameObject effectObj;
            if (!_effectObjects.TryGetValue(module.moduleType, out effectObj) || effectObj == null) return;

            UpdateEffectForLevel(effectObj, module, damageLevel);
        }

        private GameObject CreateEffectObject(ShipModule module)
        {
            Vector3 pos = module.anchor != null ? module.anchor.position : transform.position;

            var obj = new GameObject($"DamageEffect_{module.moduleType}");
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = module.anchor != null
                ? transform.InverseTransformPoint(pos)
                : Vector3.zero;
            obj.SetActive(false);

            // Simple light glow — no particle system (no purple squares)
            // No light — red point light inside ship creates visible glow through hull

            return obj;
        }

        private void UpdateEffectForLevel(GameObject effectObj, ShipModule module, int damageLevel)
        {
            if (effectObj == null) return;

            if (damageLevel == 0)
            {
                effectObj.SetActive(false);
                return;
            }

            // Damage level indicated by object being active — no light to avoid red glow on hull
            effectObj.SetActive(true);
        }

        /// <summary>Trigger full ship destruction — 3D explosion, hide ship.</summary>
        public void TriggerDestruction()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            Vector3 center = transform.position;

            // Disable all damage effects
            foreach (var kvp in _effectObjects)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            // Hide ship renderers
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.transform.parent != null && !_effectObjects.ContainsValue(r.transform.parent.gameObject))
                    r.enabled = false;
            }

            // Spawn 3D explosion effect
            float scale = Mathf.Clamp(destructionExplosionRadius / 10f, 0.8f, 3f);
            Explosion3D.Spawn(center, scale);

            // Play ship destruction sound
            var sfx = GetComponent<ShipSFX>();
            if (sfx != null) sfx.PlayDestroy();

            // Clean up damage effect objects
            Destroy(gameObject, destructionDuration + 2f);
        }
    }
}
