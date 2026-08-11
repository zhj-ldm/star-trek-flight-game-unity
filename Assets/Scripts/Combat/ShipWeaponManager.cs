using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Central coordinator for all ship weapons and combat abilities.
    /// Routes: Space=phaser, M=torpedo, N=pulse cannon, K=shield toggle.
    /// </summary>
    public class ShipWeaponManager : MonoBehaviour
    {
        [Header("Weapon References")]
        public PhaserWeapon phaser;
        public TorpedoWeapon torpedo;
        public IonPulseWeapon jammer;
        public PulseCannon pulseCannon;
        public TargetingSystem targeting;
        public ShipHealth health;
        public ShipController controller;

        [Header("State (read-only)")]
        public bool weaponsLocked;

        private IonEffect _ionEffect;

        // Player vs AI control — only player ship processes keyboard input
        private bool _isPlayerControlled = true;

        /// <summary>Dynamically set player control.</summary>
        public void SetPlayerControlled(bool value) { _isPlayerControlled = value; }

        void Awake()
        {
            if (health == null) health = GetComponent<ShipHealth>();
            if (controller == null) controller = GetComponent<ShipController>();
            if (targeting == null) targeting = GetComponent<TargetingSystem>();
            _ionEffect = GetComponent<IonEffect>();

            // Auto-detect: ships tagged Enemy are not player-controlled
            if (CompareTag("Enemy"))
                _isPlayerControlled = false;
        }

        void Start()
        {
            if (phaser == null) phaser = GetComponentInChildren<PhaserWeapon>();
            if (torpedo == null) torpedo = GetComponentInChildren<TorpedoWeapon>();
            if (jammer == null) jammer = GetComponentInChildren<IonPulseWeapon>();
            if (pulseCannon == null) pulseCannon = GetComponent<PulseCannon>();

            // Auto-add missing weapons for ALL ships (player + enemy)
            if (phaser == null)
            {
                phaser = gameObject.AddComponent<PhaserWeapon>();
                phaser.stats = controller != null ? controller.stats : null;
                phaser.health = health;
                phaser.targeting = targeting;
            }
            if (torpedo == null)
            {
                torpedo = gameObject.AddComponent<TorpedoWeapon>();
                torpedo.stats = controller != null ? controller.stats : null;
                torpedo.health = health;
                torpedo.targeting = targeting;
            }
            if (pulseCannon == null && _isPlayerControlled)
            {
                pulseCannon = gameObject.AddComponent<PulseCannon>();
                pulseCannon.stats = controller.stats;
                pulseCannon.health = health;
                pulseCannon.targeting = targeting;
            }
        }

        void Update()
        {
            // Only the player ship processes keyboard input.
            // AI ships are controlled by ShipAI.FireWeapons().
            if (!_isPlayerControlled)
            {
                // Still handle ion effect refresh for AI ships
                if (_ionEffect == null)
                    _ionEffect = GetComponent<IonEffect>();
                weaponsLocked = _ionEffect != null && _ionEffect.IsWeaponDisabled;
                if (weaponsLocked)
                {
                    if (phaser != null) phaser.StopFire();
                    if (torpedo != null) torpedo.CancelCharge();
                }
                return;
            }

            var input = ShipInput.LastInput;

            if (_ionEffect == null)
                _ionEffect = GetComponent<IonEffect>();

            weaponsLocked = _ionEffect != null && _ionEffect.IsWeaponDisabled;

            if (weaponsLocked)
            {
                if (phaser != null) phaser.StopFire();
                if (torpedo != null) torpedo.CancelCharge();
                return;
            }

            // Phaser (Space)
            if (phaser != null)
            {
                if (input.firePhaser)
                    phaser.StartFire();
                else
                    phaser.StopFire();
            }

            // Torpedo (M)
            if (torpedo != null && input.fireTorpedo)
            {
                torpedo.StartCharge();
                torpedo.Fire();
            }

            // Pulse Cannon (N) — replaces jammer
            if (pulseCannon != null && input.fireJammer)
                pulseCannon.Fire();

            // Shield toggle (K)
            if (input.toggleShield && health != null)
                health.ToggleShield();

            // Energy allocation (1/2)
            if (input.increaseWeaponEnergy && health != null)
                health.IncreaseWeaponAllocation();
            if (input.increaseShieldEnergy && health != null)
                health.IncreaseShieldAllocation();

            // Lock mode toggle
            if (input.switchLockMode && targeting != null)
                targeting.ToggleLockMode();
        }

        public bool IsPhaserFiring => phaser != null && phaser.isFiring;
        public bool IsPhaserRecharging => phaser != null && phaser.isRecharging;
        public float PhaserRechargeProgress => phaser != null ? phaser.RechargeProgress : 1f;
        public float PhaserFireProgress => phaser != null ? phaser.FireProgress : 0f;

        public int TorpedoAmmo => torpedo != null ? torpedo.currentAmmo : 0;
        public int TorpedoMaxAmmo => controller != null && controller.stats != null ? controller.stats.torpedoMaxAmmo : 0;
        public float TorpedoCharge => torpedo != null ? torpedo.ChargeProgress : 0f;
        public float TorpedoCooldown => torpedo != null ? torpedo.CooldownProgress : 1f;

        public float JammerCooldown => pulseCannon != null ? pulseCannon.CooldownProgress : 1f;
        public float IonPulseCooldown => JammerCooldown; // backward compat
        public float PhaserHeat => 0f; // backward compat — old overheat system removed

        public LockMode CurrentLockMode => targeting != null ? targeting.lockMode : LockMode.WideArea;

        public void RefreshIonEffect()
        {
            _ionEffect = GetComponent<IonEffect>();
        }
    }
}
