using UnityEngine;
using System;

namespace StarTrekCombat
{
    /// <summary>
    /// Represents a single ship module (engine, weapon, shield generator, hull section).
    /// Each module has independent health and triggers effects when damaged.
    /// </summary>
    [Serializable]
    public class ShipModule
    {
        public ModuleType moduleType;
        public string displayName;
        public float maxHealth;
        public float currentHealth;
        public Transform anchor;       // Visual anchor for effects
        public bool isDestroyed;

        /// <summary>Health percentage 0..1.</summary>
        public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        /// <summary>Damage level: 0=normal, 1=light, 2=heavy, 3=destroyed.</summary>
        public int DamageLevel
        {
            get
            {
                if (isDestroyed || currentHealth <= 0f) return 3;
                if (HealthPercent < 0.25f) return 2;
                if (HealthPercent < 0.5f) return 1;
                return 0;
            }
        }

        /// <summary>Apply damage to this module. Returns actual damage dealt.</summary>
        public float TakeDamage(float amount)
        {
            if (isDestroyed) return 0f;
            float dmg = Mathf.Min(currentHealth, amount);
            currentHealth -= dmg;
            if (currentHealth <= 0.01f)
            {
                currentHealth = 0f;
                isDestroyed = true;
            }
            return dmg;
        }

        /// <summary>Reset module to full health.</summary>
        public void Reset()
        {
            currentHealth = maxHealth;
            isDestroyed = false;
        }
    }
}
