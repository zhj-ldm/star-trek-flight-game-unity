using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Applied to a ship hit by Ion Pulse weapon.
    /// Disrupts engines (speed reduction) and weapons (fire lockdown) for a duration.
    /// </summary>
    public class IonEffect : MonoBehaviour
    {
        [Header("State (read-only)")]
        public float remainingDuration;
        public float slowFactor = 0.5f;
        public bool engineDisabled = true;
        public bool weaponDisabled = true;

        private float _totalDuration;

        /// <summary>Is the ion effect currently active?</summary>
        public bool IsActive => remainingDuration > 0f;

        /// <summary>Is engine disabled by ion?</summary>
        public bool IsEngineDisabled => IsActive && engineDisabled;

        /// <summary>Is weapon system disabled by ion?</summary>
        public bool IsWeaponDisabled => IsActive && weaponDisabled;

        /// <summary>Get the speed modifier (0..1). 1 = normal speed.</summary>
        public float GetSpeedModifier() => IsActive ? slowFactor : 1f;

        void Update()
        {
            if (remainingDuration > 0f)
            {
                remainingDuration -= Time.deltaTime;
                if (remainingDuration <= 0f)
                {
                    remainingDuration = 0f;
                    engineDisabled = false;
                    weaponDisabled = false;
                }
            }
        }

        /// <summary>Apply or refresh the ion disruption effect.</summary>
        /// <param name="duration">Total disruption time in seconds.</param>
        /// <param name="slow">Speed multiplier (0..1).</param>
        public void Apply(float duration, float slow = 0.5f)
        {
            remainingDuration = duration;
            _totalDuration = duration;
            slowFactor = slow;
            engineDisabled = true;
            weaponDisabled = true;
        }

        /// <summary>Progress 0..1 of the ion effect (for UI).</summary>
        public float Progress => _totalDuration > 0f ? remainingDuration / _totalDuration : 0f;
    }
}
