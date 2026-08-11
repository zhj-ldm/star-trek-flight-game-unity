using UnityEngine;
using System;

namespace StarTrekCombat
{
    /// <summary>
    /// Manages hull, armor, and shield values.
    /// Damage flows: Shield → Armor → Hull.
    /// Integrates with ModularDamageSystem for module-based damage routing.
    /// </summary>
    public class ShipHealth : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ModularDamageSystem modularDamage;
        public DamageEffects damageEffects;

        [Header("Current Values (runtime)")]
        public float currentHull;
        public float currentArmor;
        public float currentShield;
        public float currentEnergy;

        [Header("Energy Allocation")]
        [Range(0f, 1f)]
        public float weaponEnergyAllocation = 0.5f;   // 1 = full to weapons, 0 = full to shields
        public float shieldActive;  // >0 = shield on, drains energy. Toggle with K.
        public bool isShieldOn = false;

        [Header("Shield Multiplier")]
        [Tooltip("Multiplier applied to max shield. Player = 2.0 for 200%, enemies = 1.0.")]
        public float shieldMultiplier = 1f;

        // Shield regen state
        private float _lastHitTime;
        private float _shieldOverloadTimer;
        private float _shieldOverloadCooldown;
        private bool _shieldDisabled;

        // Events
        public event Action<float, DamageType> OnDamaged;      // (amount, type)
        public event Action<Vector3, float, DamageType> OnDamagedAt; // (hitPos, amount, type)
        public event Action OnShieldBroken;
        public event Action OnArmorBroken;
        public event Action OnShipDestroyed;
        public event Action OnShieldOverloadStart;
        public event Action OnShieldOverloadEnd;

        // Public properties
        public bool IsAlive => currentHull > 0f;
        public bool IsShieldActive => isShieldOn && currentShield > 0f && !_shieldDisabled;
        public bool IsInvulnerable => _shieldOverloadTimer > 0f;
        public float ShieldPercent => stats != null ? currentShield / GetEffectiveMaxShield() : 0f;
        public float ArmorPercent => stats != null ? currentArmor / stats.maxArmor : 0f;
        public float HullPercent => stats != null ? currentHull / stats.maxHull : 0f;
        public float EnergyPercent => stats != null ? currentEnergy / stats.maxEnergy : 0f;

        /// <summary>Shield allocation (1 - weaponEnergyAllocation).</summary>
        public float ShieldEnergyAllocation => 1f - weaponEnergyAllocation;

        /// <summary>Effective shield max, scaled by shield energy allocation.</summary>
        public float GetAllocationMaxShield()
        {
            float baseShield = stats != null ? stats.maxShield * shieldMultiplier : 800f;
            // Shield durability scales with allocation: 0% alloc = 40% max, 100% alloc = 120% max
            return baseShield * (0.4f + ShieldEnergyAllocation * 0.8f);
        }

        /// <summary>Effective phaser recharge multiplier (higher allocation = faster recharge).</summary>
        public float GetWeaponRechargeMultiplier()
        {
            // 0% weapon alloc = 2x recharge time, 100% = 0.7x
            return 2f - weaponEnergyAllocation * 1.3f;
        }

        void Start()
        {
            if (stats == null) return;
            currentHull   = stats.maxHull;
            currentArmor  = stats.maxArmor;
            currentShield = GetEffectiveMaxShield();
            currentEnergy = stats.maxEnergy;

            if (modularDamage == null) modularDamage = GetComponent<ModularDamageSystem>();
            if (damageEffects == null) damageEffects = GetComponent<DamageEffects>();

            // Enemy ships have shields on by default (player must press K to toggle)
            if (CompareTag("Enemy"))
                isShieldOn = true;
        }

        void Update()
        {
            if (stats == null || !IsAlive) return;

            RegenerateEnergy();
            RegenerateShield();
            UpdateOverloadTimers();
        }

        public float GetEffectiveMaxShield()
        {
            float max = stats.maxShield * shieldMultiplier;
            if (modularDamage != null)
                max *= modularDamage.GetShieldMaxMultiplier();
            // Also scale by shield energy allocation
            max *= (0.4f + ShieldEnergyAllocation * 0.8f);
            return max;
        }

        private void RegenerateEnergy()
        {
            if (currentEnergy < stats.maxEnergy)
                currentEnergy = Mathf.Min(stats.maxEnergy, currentEnergy + stats.energyRegen * Time.deltaTime);

            // Shield drains energy when active
            if (isShieldOn && currentShield > 0f)
            {
                float drain = 2f * Time.deltaTime;
                currentEnergy = Mathf.Max(0f, currentEnergy - drain);
                if (currentEnergy <= 0f)
                {
                    // Shield collapses when energy runs out
                    isShieldOn = false;
                    OnShieldBroken?.Invoke();
                }
            }
        }

        private void RegenerateShield()
        {
            if (_shieldDisabled) return;
            if (Time.time - _lastHitTime < stats.shieldRegenDelay) return;

            float regenRate = stats.shieldRegenRate;
            if (modularDamage != null)
                regenRate *= modularDamage.GetShieldRegenModifier();

            float maxShield = GetEffectiveMaxShield();
            if (currentShield < maxShield)
                currentShield = Mathf.Min(maxShield, currentShield + regenRate * Time.deltaTime);
        }

        private void UpdateOverloadTimers()
        {
            if (_shieldOverloadTimer > 0f)
            {
                _shieldOverloadTimer -= Time.deltaTime;
                if (_shieldOverloadTimer <= 0f)
                {
                    _shieldDisabled = true;
                    _shieldOverloadCooldown = stats.emergencyShieldCooldown;
                    OnShieldOverloadEnd?.Invoke();
                }
            }
            else if (_shieldDisabled)
            {
                _shieldOverloadCooldown -= Time.deltaTime;
                if (_shieldOverloadCooldown <= 0f)
                {
                    _shieldDisabled = false;
                    currentShield = Mathf.Max(currentShield, GetEffectiveMaxShield() * 0.25f);
                }
            }
        }

        /// <summary>Apply damage to the ship. Returns actual damage dealt.</summary>
        public float TakeDamage(float amount, DamageType damageType)
        {
            return TakeDamageAt(amount, damageType, transform.position);
        }

        /// <summary>Apply damage at a specific world position (for module routing).</summary>
        public float TakeDamageAt(float amount, DamageType damageType, Vector3 hitPoint)
        {
            if (!IsAlive) return 0f;
            if (IsInvulnerable) return 0f;

            _lastHitTime = Time.time;

            // Route to modular damage system first
            float remaining = amount;
            if (modularDamage != null)
            {
                remaining = modularDamage.RouteDamage(amount, damageType, hitPoint);
            }

            // 1. Shield absorbs first (only if shield is toggled on)
            if (isShieldOn && currentShield > 0f)
            {
                float shieldDmg = Mathf.Min(currentShield, remaining);
                currentShield -= shieldDmg;
                remaining -= shieldDmg;

                if (currentShield <= 0.01f)
                {
                    currentShield = 0f;
                    OnShieldBroken?.Invoke();
                }
            }

            if (remaining <= 0f)
            {
                OnDamaged?.Invoke(amount, damageType);
                OnDamagedAt?.Invoke(hitPoint, amount, damageType);
                return amount;
            }

            // 2. Armor absorbs second (thin layer)
            if (currentArmor > 0f)
            {
                float armorDmg = Mathf.Min(currentArmor, remaining);
                currentArmor -= armorDmg;
                remaining -= armorDmg;

                if (currentArmor <= 0.01f)
                {
                    currentArmor = 0f;
                    OnArmorBroken?.Invoke();
                }
            }

            // 3. Hull takes the rest — very fragile, 3x damage multiplier
            if (remaining > 0f)
            {
                // Hull is fragile — takes amplified damage
                float hullDamage = remaining * 3f;
                currentHull -= hullDamage;
                if (currentHull <= 0f)
                {
                    currentHull = 0f;
                    OnShipDestroyed?.Invoke();
                    damageEffects?.TriggerDestruction();
                }
            }

            OnDamaged?.Invoke(amount, damageType);
            OnDamagedAt?.Invoke(hitPoint, amount, damageType);
            return amount;
        }

        /// <summary>Spend energy. Returns true if successful.</summary>
        public bool SpendEnergy(float amount)
        {
            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                return true;
            }
            return false;
        }

        /// <summary>Toggle shield on/off (K key).</summary>
        public bool ToggleShield()
        {
            if (!IsAlive) return false;
            if (!isShieldOn)
            {
                if (currentEnergy < 5f) return false; // Need some energy to activate
                isShieldOn = true;
                if (currentShield <= 0f)
                    currentShield = GetEffectiveMaxShield() * 0.5f;
                return true;
            }
            else
            {
                isShieldOn = false;
                return false;
            }
        }

        /// <summary>Shift energy allocation toward weapons (1 key). +0.1 to weapon, -0.1 to shield.</summary>
        public void IncreaseWeaponAllocation()
        {
            weaponEnergyAllocation = Mathf.Min(1f, weaponEnergyAllocation + 0.1f);
        }

        /// <summary>Shift energy allocation toward shields (2 key). -0.1 from weapon, +0.1 to shield.</summary>
        public void IncreaseShieldAllocation()
        {
            weaponEnergyAllocation = Mathf.Max(0f, weaponEnergyAllocation - 0.1f);
        }

        /// <summary>Activate emergency shield overload (brief invulnerability).</summary>
        public bool ActivateShieldOverload()
        {
            if (_shieldOverloadTimer > 0f || _shieldDisabled) return false;
            if (!SpendEnergy(40f)) return false;

            _shieldOverloadTimer = 4f;
            currentShield = GetEffectiveMaxShield();
            OnShieldOverloadStart?.Invoke();
            return true;
        }
    }
}
