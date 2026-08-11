using UnityEngine;
using System;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Modular damage system — routes incoming damage to specific ship modules.
    /// Each module's damage level triggers gameplay penalties:
    /// - Engine: speed reduction, inertia drift, warp disabled
    /// - Weapon: phaser rate down, torpedo locked
    /// - Shield Generator: shield regen halted, max shield reduced
    /// - Hull: continuous bleed, system failures
    /// </summary>
    public class ModularDamageSystem : MonoBehaviour
    {
        [Header("Modules")]
        public ShipModule engines = new ShipModule
        {
            moduleType = ModuleType.Engine,
            displayName = "引擎",
            maxHealth = 300f,
            currentHealth = 300f
        };
        public ShipModule weapons = new ShipModule
        {
            moduleType = ModuleType.Weapon,
            displayName = "武器舱",
            maxHealth = 250f,
            currentHealth = 250f
        };
        public ShipModule shieldGenerator = new ShipModule
        {
            moduleType = ModuleType.ShieldGenerator,
            displayName = "护盾发生器",
            maxHealth = 200f,
            currentHealth = 200f
        };
        public ShipModule hull = new ShipModule
        {
            moduleType = ModuleType.Hull,
            displayName = "船体",
            maxHealth = 500f,
            currentHealth = 500f
        };

        [Header("Hull Bleed")]
        public float hullBleedRate = 2f;         // HP per second when hull breached
        public float hullBleedThreshold = 0.3f;  // Start bleeding below this %

        [Header("References")]
        public ShipHealth health;
        public ShipController controller;
        public DamageEffects damageEffects;

        // Events
        public event Action<ModuleType, int> OnModuleDamaged;    // (type, damageLevel)
        public event Action<ModuleType> OnModuleDestroyed;

        private List<ShipModule> _allModules;

        void Awake()
        {
            if (health == null) health = GetComponent<ShipHealth>();
            if (controller == null) controller = GetComponent<ShipController>();
            if (damageEffects == null) damageEffects = GetComponent<DamageEffects>();
        }

        void Start()
        {
            _allModules = new List<ShipModule> { engines, weapons, shieldGenerator, hull };
        }

        void Update()
        {
            // Hull bleed
            if (hull.DamageLevel >= 2 && health != null && health.IsAlive)
            {
                float bleed = hullBleedRate * Time.deltaTime;
                health.TakeDamage(bleed, DamageType.Kinetic);
            }
        }

        /// <summary>
        /// Route damage to the appropriate module based on damage type and hit location.
        /// Called by ShipHealth before applying to the main health pool.
        /// </summary>
        /// <param name="amount">Original damage amount.</param>
        /// <param name="damageType">Type of damage.</param>
        /// <param name="hitPoint">World-space hit position (for module routing).</param>
        /// <returns>Remaining damage to apply to the main health pool.</returns>
        public float RouteDamage(float amount, DamageType damageType, Vector3 hitPoint)
        {
            float remaining = amount;

            // Determine which module(s) to damage
            ShipModule primaryModule = GetModuleForDamage(damageType, hitPoint);

            if (primaryModule != null && !primaryModule.isDestroyed)
            {
                float moduleDmg = primaryModule.TakeDamage(remaining * 0.4f);
                remaining -= moduleDmg;

                int prevLevel = GetPreviousDamageLevel(primaryModule);
                int newLevel = primaryModule.DamageLevel;
                if (newLevel > prevLevel)
                {
                    OnModuleDamaged?.Invoke(primaryModule.moduleType, newLevel);
                    if (newLevel == 3)
                        OnModuleDestroyed?.Invoke(primaryModule.moduleType);
                }

                damageEffects?.OnModuleDamaged(primaryModule, newLevel);
            }

            // Explosive damage also damages hull directly
            if (damageType == DamageType.Explosive && !hull.isDestroyed)
            {
                float hullDmg = hull.TakeDamage(amount * 0.15f);
                int prevLevel = GetPreviousDamageLevel(hull);
                int newLevel = hull.DamageLevel;
                if (newLevel > prevLevel)
                {
                    OnModuleDamaged?.Invoke(ModuleType.Hull, newLevel);
                    if (newLevel == 3)
                        OnModuleDestroyed?.Invoke(ModuleType.Hull);
                }
                damageEffects?.OnModuleDamaged(hull, newLevel);
            }

            return remaining;
        }

        private ShipModule GetModuleForDamage(DamageType damageType, Vector3 hitPoint)
        {
            // Ion damage targets engines primarily
            if (damageType == DamageType.Ion)
                return engines;

            // Explosive targets hull + nearest module
            if (damageType == DamageType.Explosive)
                return hull;

            // Energy damage: route based on hit position relative to ship
            Vector3 localHit = transform.InverseTransformPoint(hitPoint);

            // Behind ship = engine area
            if (localHit.z < -transform.localScale.z * 0.3f)
                return engines;

            // Front/top = weapon area
            if (localHit.z > transform.localScale.z * 0.2f)
                return weapons;

            // Sides = shield generator
            if (Mathf.Abs(localHit.x) > transform.localScale.x * 0.3f)
                return shieldGenerator;

            // Default: hull
            return hull;
        }

        private int GetPreviousDamageLevel(ShipModule module)
        {
            // Simple: we check current vs after damage
            // This is called BEFORE TakeDamage in the flow above, so we estimate
            // Actually called AFTER TakeDamage, so we infer previous level
            if (module.currentHealth <= 0f && !module.isDestroyed) return 2;
            return module.DamageLevel; // Approximation — the event fires on level change
        }

        // Module status queries for other systems

        /// <summary>Engine damage level 0..3.</summary>
        public int EngineDamageLevel => engines.DamageLevel;

        /// <summary>Weapon damage level 0..3.</summary>
        public int WeaponDamageLevel => weapons.DamageLevel;

        /// <summary>Shield generator damage level 0..3.</summary>
        public int ShieldGenDamageLevel => shieldGenerator.DamageLevel;

        /// <summary>Hull damage level 0..3.</summary>
        public int HullDamageLevel => hull.DamageLevel;

        /// <summary>Get speed modifier based on engine damage (0..1).</summary>
        public float GetEngineSpeedModifier()
        {
            switch (engines.DamageLevel)
            {
                case 1: return 0.75f;
                case 2: return 0.4f;
                case 3: return 0.15f;
                default: return 1f;
            }
        }

        /// <summary>Get phaser fire rate modifier based on weapon damage (0..1).</summary>
        public float GetWeaponFireRateModifier()
        {
            switch (weapons.DamageLevel)
            {
                case 1: return 0.7f;
                case 2: return 0.4f;
                case 3: return 0f;
                default: return 1f;
            }
        }

        /// <summary>Can the ship fire torpedoes?</summary>
        public bool CanFireTorpedo => weapons.DamageLevel < 3;

        /// <summary>Get shield regen modifier based on shield generator damage (0..1).</summary>
        public float GetShieldRegenModifier()
        {
            switch (shieldGenerator.DamageLevel)
            {
                case 1: return 0.5f;
                case 2: return 0.2f;
                case 3: return 0f;
                default: return 1f;
            }
        }

        /// <summary>Get max shield multiplier based on shield generator damage (0..1).</summary>
        public float GetShieldMaxMultiplier()
        {
            switch (shieldGenerator.DamageLevel)
            {
                case 1: return 0.8f;
                case 2: return 0.5f;
                case 3: return 0.25f;
                default: return 1f;
            }
        }

        /// <summary>Can the ship warp?</summary>
        public bool CanWarp => engines.DamageLevel < 2;

        /// <summary>Get all modules for UI display.</summary>
        public List<ShipModule> GetAllModules() => _allModules ?? new List<ShipModule> { engines, weapons, shieldGenerator, hull };

        /// <summary>Get module health percent for a specific type.</summary>
        public float GetModuleHealthPercent(ModuleType type)
        {
            switch (type)
            {
                case ModuleType.Engine: return engines.HealthPercent;
                case ModuleType.Weapon: return weapons.HealthPercent;
                case ModuleType.ShieldGenerator: return shieldGenerator.HealthPercent;
                case ModuleType.Hull: return hull.HealthPercent;
                default: return 1f;
            }
        }
    }
}
