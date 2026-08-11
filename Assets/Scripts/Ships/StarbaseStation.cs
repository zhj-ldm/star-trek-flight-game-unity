using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Space station docking & shield-recharge behavior.
    /// Requirements:
    ///  - When the player ship comes within dockRadius (100m), it is considered docked.
    ///  - Pressing O locks the player ship's position relative to the station
    ///    (position anchored to the station). Pressing aO again un-docks.
    ///  - While docked, shield regenerates by shieldRegenPercentPerSec % of max shield per
    ///    second; once full it stops recharging.
    /// Attach to the station root. The player is found by the "Player" tag.
    /// </summary>
    public class StarbaseStation : MonoBehaviour
    {
        [Header("Docking")]
        [Tooltip("Distance (m) at which the player is considered docked.")]
        public float dockRadius = 100f;
        [Tooltip("Local offset from the station where the docked player sits.")]
        public Vector3 dockOffset = new Vector3(0f, 0f, -60f);
        [Tooltip("Speed (m/s) at which the player eases onto the dock.")]
        public float dockSmooth = 4f;

        [Header("Shield Recharge")]
        [Tooltip("Percent of MAX shield restored per second (e.g. 0.5 = 0.5%/s).")]
        public float shieldRegenPercentPerSec = 0.5f;

        private Transform _player;
        private ShipController _playerController;
        private ShipHealth _playerHealth;
        private bool _locked;

        public bool IsLocked => _locked;

        void Start()
        {
            RefreshPlayerRefs();
        }

        private void RefreshPlayerRefs()
        {
            if (_player != null) return;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
            _playerController = p.GetComponent<ShipController>();
            _playerHealth = p.GetComponent<ShipHealth>();
        }

        /// <summary>Docked when inside the dock radius (accounts for station scale).</summary>
        public bool PlayerDocked
        {
            get
            {
                if (_player == null) return false;
                float surfaceDist = GetSurfaceDistance();
                return surfaceDist <= dockRadius;
            }
        }

        public float GetSurfaceDistance()
        {
            if (_player == null) return float.MaxValue;
            float dist = Vector3.Distance(transform.position, _player.position);
            return dist - transform.lossyScale.magnitude * 0.5f;
        }

        void Update()
        {
            RefreshPlayerRefs();

            bool docked = PlayerDocked;

            // While docked, recharge shield; once full it stops.
            if (docked && _playerHealth != null)
                RegenShield();

            // Sticky dock lock rebound to ',' because O is now the instant-stop key (ShipController).
            if (docked && Input.GetKeyDown(KeyCode.Comma))
                ToggleLock();

            // If docked but not locked, and player is close, optionally nothing extra.
            if (_locked && _player != null)
            {
                Vector3 targetWorld = transform.TransformPoint(dockOffset);
                float dist = Vector3.Distance(_player.position, targetWorld);
                if (dist > 0.05f)
                    _player.position = Vector3.MoveTowards(_player.position, targetWorld, dockSmooth * Time.deltaTime);
                else
                    _player.position = targetWorld;
                _player.rotation = transform.rotation;
            }

            // Auto-unlock if player somehow leaves the dock radius while locked.
            if (_locked && !docked)
                Unlock();
        }

        private void RegenShield()
        {
            float maxShield = _playerHealth.GetEffectiveMaxShield();
            if (_playerHealth.currentShield >= maxShield) return;
            float fraction = Mathf.Clamp01(shieldRegenPercentPerSec / 100f);
            _playerHealth.currentShield = Mathf.Min(maxShield, _playerHealth.currentShield + maxShield * fraction * Time.deltaTime);
        }

        private void ToggleLock()
        {
            if (_locked) Unlock();
            else Lock();
        }

        private void Lock()
        {
            _locked = true;
            if (_playerController != null) _playerController.SetFrozen(true);
            Debug.Log($"[Starbase] Docked & locked to {name}");
        }

        private void Unlock()
        {
            if (!_locked) return;
            _locked = false;
            if (_playerController != null) _playerController.SetFrozen(false);
            Debug.Log($"[Starbase] Undocked from {name}");
        }

        private void OnDestroy()
        {
            Unlock();
        }
    }
}