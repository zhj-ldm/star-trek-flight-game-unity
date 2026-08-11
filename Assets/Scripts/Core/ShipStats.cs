using UnityEngine;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// ScriptableObject defining all stats for a ship class.
    /// Configurable per ship type (Scout, Cruiser, BattleCruiser, Carrier).
    /// </summary>
    [CreateAssetMenu(fileName = "NewShipStats", menuName = "StarTrek/Ship Stats")]
    public class ShipStats : ScriptableObject
    {
        [Header("Identity")]
        public string shipName = "USS Enterprise";
        public ShipClass shipClass = ShipClass.Cruiser;
        public Faction defaultFaction = Faction.Player;

        [Header("Hull & Armor")]
        public float maxHull = 1000f;
        public float maxArmor = 500f;
        public float maxShield = 800f;

        [Header("Shield")]
        public float shieldRegenRate = 20f;     // per second
        public float shieldRegenDelay = 3f;     // seconds after last hit
        public float emergencyShieldCooldown = 60f; // shield overload cooldown

        [Header("Energy")]
        public float maxEnergy = 100f;
        public float energyRegen = 15f;          // per second

        [Header("Movement — Orbiter 2016 Inertia Physics")]
        [Tooltip("Main engine thrust force (m/s²). Engine power 0..1 scales this.")]
        public float mainEngineThrust = 15f;
        [Tooltip("How fast engine power changes per second (0..1 range).")]
        public float enginePowerRate = 0.3f;
        [Tooltip("RCS rotation torque — angular acceleration in deg/s² for pitch.")]
        public float rcsRotPitch = 8f;
        [Tooltip("RCS rotation torque — angular acceleration in deg/s² for yaw.")]
        public float rcsRotYaw = 8f;
        [Tooltip("RCS rotation torque — angular acceleration in deg/s² for roll.")]
        public float rcsRotRoll = 10f;
        [Tooltip("RCS translation acceleration in m/s² (LIN mode).")]
        public float rcsLinAccel = 1f;
        [Tooltip("Auto-stabilize angular damping rate (deg/s² per second). 0 = off.")]
        public float autoStabilizeRate = 12f;
        [Tooltip("Maximum angular velocity cap in deg/s — prevents infinite spin buildup.")]
        public float maxAngularVelocity = 45f;
        [Tooltip("Maximum linear speed cap in m/s — very high, only prevents physics blowup.")]
        public float maxSpeed = 5000f;
        [Tooltip("Old acceleration field kept for AI backward compat — mapped to mainEngineThrust.")]
        public float acceleration = 30f;
        [Tooltip("Old strafeSpeed kept for AI backward compat — mapped to rcsLinAccel.")]
        public float strafeSpeed = 25f;
        [Tooltip("Old turnSpeedPitch kept for AI backward compat — mapped to rcsRotPitch.")]
        public float turnSpeedPitch = 60f;
        [Tooltip("Old turnSpeedYaw kept for AI backward compat — mapped to rcsRotYaw.")]
        public float turnSpeedYaw = 60f;
        [Tooltip("Old turnSpeedRoll kept for AI backward compat — mapped to rcsRotRoll.")]
        public float turnSpeedRoll = 40f;
        [Tooltip("Old brakeDeceleration kept for AI backward compat.")]
        public float brakeDeceleration = 50f;
        [Tooltip("Old angularDamping kept for AI backward compat.")]
        public float angularDamping = 2f;

        [Header("Warp Drive")]
        public float warpSpeed = 500f;            // m/s during warp
        public float warpEnergyCost = 5f;         // energy per second
        public float warpChargeTime = 2f;         // seconds to charge before warp
        public float pulseImpulse = 200f;         // instant velocity boost
        public float pulseCooldown = 8f;           // seconds
        public float pulseEnergyCost = 25f;

        [Header("Weapon Hardpoints")]
        public List<Vector3> phaserHardpoints = new List<Vector3>
        {
            new Vector3(0f, 0f, 2f),
            new Vector3(-1f, 0f, 1f),
            new Vector3(1f, 0f, 1f)
        };
        public List<Vector3> torpedoTubes = new List<Vector3>
        {
            new Vector3(0f, -0.5f, 2f)
        };

        [Header("Engine Positions")]
        public List<Vector3> enginePositions = new List<Vector3>
        {
            new Vector3(0f, 0f, -2f)
        };

        [Header("Weapon Config")]
        public float phaserDamage = 20f;            // per second (continuous beam, per hardpoint)
        public float phaserEnergyCost = 3f;        // per second
        public float phaserRange = 300f;
        public float phaserOverheatThreshold = 5f; // seconds of continuous fire before overheat
        public float phaserCooldownTime = 2f;      // overheat cooldown

        public float torpedoDamage = 150f;
        public float torpedoSpeed = 100f;
        public int torpedoMaxAmmo = 100;
        public float torpedoCooldown = 3f;        // per torpedo
        public float torpedoExplosionRadius = 30f;
        public float torpedoEnergyCost = 10f;

        public float ionPulseDamage = 20f;
        public float ionPulseSlowDuration = 4f;   // seconds of slow effect
        public float ionPulseSlowFactor = 0.5f;    // multiply target speed
        public float ionPulseCooldown = 15f;
        public float ionPulseRange = 200f;
        public float ionPulseEnergyCost = 30f;

        [Header("Input")]
        public float mousePitchSensitivity = 3f;
        public float freeLookSensitivity = 3f;
        public float torpedoChargeTime = 2f;          // seconds to fully charge

        [Header("Scale")]
        public float modelScale = 1f;

        /// <summary>Get max speed for a given flight mode.</summary>
        public float GetMaxSpeed(FlightMode mode)
        {
            switch (mode)
            {
                case FlightMode.Combat: return maxSpeed * 0.5f; // Combat = half max for AI compat
                case FlightMode.Warp: return warpSpeed;
                default: return maxSpeed;
            }
        }
    }
}
