using UnityEngine;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Scans for nearby ships and manages target locking.
    /// Uses screen-space targeting circle: enemies within the on-screen HUD circle are auto-locked.
    /// Once a target is fully locked (primary), it becomes "sticky" — camera tracks it to keep
    /// it in the scan circle, and new enemies entering the circle do NOT steal the lock.
    /// Lock breaks via · key, or when the target leaves the scan circle (pushed out by camera).
    /// Supports WideArea (large circle, many targets) and Precision (small circle, few targets).
    /// </summary>
    public class TargetingSystem : MonoBehaviour
    {
        [Header("Configuration")]
        public Faction ownerFaction = Faction.Player;
        public float scanRange = 1500f;
        public float wideAreaScreenRadius = 120f;   // pixels
        public float precisionScreenRadius = 50f;    // pixels
        public float scanInterval = 0.3f;

        [Header("Lock Mode")]
        public LockMode lockMode = LockMode.WideArea;
        public int wideAreaMaxLocks = 10;
        public int precisionMaxLocks = 2;

        [Header("Lock Progress")]
        public float lockTime = 2f;          // seconds to fully lock
        public float lockProgress;           // 0..1 (1 = locked)
        private float _lockTimer;
        private Transform _lastPrimaryTarget;

        [Header("Sticky Lock")]
        [Tooltip("Once locked, primary target stays locked even outside scan circle")]
        public bool stickyLockEnabled = true;
        [Tooltip("Grace period (seconds) before a locked target that left the circle is lost")]
        public float stickyGracePeriod = 3f;
        private float _stickyGraceTimer;

        [Header("State (read-only)")]
        public List<Transform> lockedTargets = new List<Transform>();
        public List<Transform> allTargets = new List<Transform>();
        public Transform primaryTarget;
        public bool isStickyLocked;       // true when primary target is sticky-tracked
        public bool lockingEnabled = true; // 2 key toggles — false = no locking at all

        private float _lastScanTime;
        private string[] _enemyTags;
        private Camera _cam;

        void Start()
        {
            _enemyTags = GetEnemyTags();
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;

            if (Time.time - _lastScanTime >= scanInterval)
            {
                _lastScanTime = Time.time;
                ScanForTargets();
            }

            // Handle manual lock-break via · key
            var input = ShipInput.LastInput;
            if (input.breakLock)
            {
                BreakLock();
            }

            // Toggle locking on/off via 2 key
            if (input.toggleLocking)
            {
                lockingEnabled = !lockingEnabled;
                if (!lockingEnabled)
                    BreakLock();
            }

            // Skip locking if disabled
            if (!lockingEnabled)
            {
                lockedTargets.Clear();
                primaryTarget = null;
                lockProgress = 0f;
                isStickyLocked = false;
                return;
            }

            // Update locks every frame (screen-space check is cheap)
            UpdateLocks();

            // Clean up destroyed targets
            lockedTargets.RemoveAll(t => t == null);
            allTargets.RemoveAll(t => t == null);
            if (primaryTarget == null && lockedTargets.Count > 0)
                primaryTarget = lockedTargets[0];

            // Update lock progress
            UpdateLockProgress();
        }

        private void UpdateLockProgress()
        {
            if (primaryTarget != null && primaryTarget == _lastPrimaryTarget)
            {
                _lockTimer += Time.deltaTime;
                lockProgress = Mathf.Clamp01(_lockTimer / lockTime);
            }
            else
            {
                _lockTimer = 0f;
                lockProgress = 0f;
                _lastPrimaryTarget = primaryTarget;
            }
        }

        /// <summary>Is the target fully locked?</summary>
        public bool IsLockComplete => lockProgress >= 1f;

        private string[] GetEnemyTags()
        {
            switch (ownerFaction)
            {
                case Faction.Player:
                case Faction.Ally:
                    return new[] { "Enemy" };
                case Faction.Enemy:
                    return new[] { "Player", "Ally" };
                default:
                    return new string[0];
            }
        }

        private void ScanForTargets()
        {
            allTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(transform.position, scanRange);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                bool isEnemy = false;
                foreach (var tag in _enemyTags)
                {
                    if (hit.CompareTag(tag))
                    {
                        isEnemy = true;
                        break;
                    }
                }

                if (isEnemy && !allTargets.Contains(hit.transform))
                {
                    // Skip destroyed ships
                    var targetHealth = hit.GetComponent<ShipHealth>();
                    if (targetHealth == null)
                        targetHealth = hit.GetComponentInParent<ShipHealth>();
                    if (targetHealth != null && !targetHealth.IsAlive)
                        continue;

                    allTargets.Add(hit.transform);
                }
            }
        }

        /// <summary>
        /// Check if a target is currently within the scan circle on screen.
        /// </summary>
        public bool IsInScanCircle(Transform target)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || target == null) return false;

            float screenRadius = EffectiveScreenRadius;
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            Vector3 worldCenter = GetModelCenter(target);
            Vector3 screenPos = _cam.WorldToScreenPoint(worldCenter);
            if (screenPos.z <= 0f) return false;

            float screenDist = Vector2.Distance(
                new Vector2(screenPos.x, screenPos.y),
                new Vector2(screenCenter.x, screenCenter.y)
            );

            return screenDist <= screenRadius;
        }

        private void UpdateLocks()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            float screenRadius = EffectiveScreenRadius;
            int maxLocks = lockMode == LockMode.WideArea ? wideAreaMaxLocks : precisionMaxLocks;

            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            // --- Sticky lock logic ---
            // If we have a fully-locked primary target, keep it sticky FOREVER.
            // Only breaks via · key (BreakLock) or target destroyed.
            // New enemies entering the circle do NOT replace the sticky primary.
            if (stickyLockEnabled && isStickyLocked && primaryTarget != null)
            {
                // Check if sticky target is still alive
                var th = primaryTarget.GetComponent<ShipHealth>();
                if (th == null) th = primaryTarget.GetComponentInParent<ShipHealth>();
                if (th != null && !th.IsAlive)
                {
                    // Target destroyed — release sticky lock
                    BreakLock();
                }
                // NO grace period, NO scan circle check — lock stays until · key or target destroyed

                // Rebuild lockedTargets: sticky primary first, then other enemies in circle
                lockedTargets.Clear();
                lockedTargets.Add(primaryTarget);

                var candidates = new List<(Transform t, float dist)>();
                foreach (var target in allTargets)
                {
                    if (target == null || target == primaryTarget) continue;

                    var th2 = target.GetComponent<ShipHealth>();
                    if (th2 == null) th2 = target.GetComponentInParent<ShipHealth>();
                    if (th2 != null && !th2.IsAlive) continue;

                    Vector3 screenPos = _cam.WorldToScreenPoint(GetModelCenter(target));
                    if (screenPos.z <= 0f) continue;

                    float screenDist = Vector2.Distance(
                        new Vector2(screenPos.x, screenPos.y),
                        new Vector2(screenCenter.x, screenCenter.y)
                    );

                    if (screenDist <= screenRadius)
                    {
                        float worldDist = Vector3.Distance(transform.position, target.position);
                        candidates.Add((target, worldDist));
                    }
                }

                candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
                for (int i = 0; i < candidates.Count && lockedTargets.Count < maxLocks; i++)
                {
                    lockedTargets.Add(candidates[i].t);
                }

                return;
            }

            // --- Normal (non-sticky) lock logic ---
            lockedTargets.Clear();

            var normalCandidates = new List<(Transform t, float dist)>();

            foreach (var target in allTargets)
            {
                if (target == null) continue;

                // Skip destroyed ships
                var th = target.GetComponent<ShipHealth>();
                if (th == null) th = target.GetComponentInParent<ShipHealth>();
                if (th != null && !th.IsAlive) continue;

                Vector3 screenPos = _cam.WorldToScreenPoint(GetModelCenter(target));

                // Skip if behind camera
                if (screenPos.z <= 0f) continue;

                float screenDist = Vector2.Distance(
                    new Vector2(screenPos.x, screenPos.y),
                    new Vector2(screenCenter.x, screenCenter.y)
                );

                if (screenDist <= screenRadius)
                {
                    float worldDist = Vector3.Distance(transform.position, target.position);
                    normalCandidates.Add((target, worldDist));
                }
            }

            // Sort by world distance (nearest first)
            normalCandidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            for (int i = 0; i < normalCandidates.Count && i < maxLocks; i++)
            {
                lockedTargets.Add(normalCandidates[i].t);
            }

            primaryTarget = lockedTargets.Count > 0 ? lockedTargets[0] : null;

            // When lock completes, activate sticky lock
            if (stickyLockEnabled && IsLockComplete && primaryTarget != null)
            {
                isStickyLocked = true;
                _stickyGraceTimer = 0f;
            }
        }

        /// <summary>Break the current sticky lock.</summary>
        public void BreakLock()
        {
            isStickyLocked = false;
            _stickyGraceTimer = 0f;
            _lockTimer = 0f;
            lockProgress = 0f;
            primaryTarget = null;
            _lastPrimaryTarget = null;
            lockedTargets.Clear();
        }

        /// <summary>Toggle between WideArea and Precision lock modes.</summary>
        public void ToggleLockMode()
        {
            lockMode = lockMode == LockMode.WideArea ? LockMode.Precision : LockMode.WideArea;
        }

        /// <summary>Get the current targeting circle radius in screen pixels.</summary>
        public float GetCurrentScreenRadius()
        {
            return lockMode == LockMode.WideArea ? wideAreaScreenRadius : precisionScreenRadius;
        }

        /// <summary>Canvas scale factor — converts canvas-space pixels to screen pixels.</summary>
        private float CanvasScaleFactor
        {
            get
            {
                // Reference resolution width from HUDManager (1920). matchWidthOrHeight=0 → width-based.
                return Screen.width / 1920f;
            }
        }

        /// <summary>Effective scan radius in actual screen pixels (accounting for canvas scaling).</summary>
        private float EffectiveScreenRadius
        {
            get
            {
                float baseRadius = lockMode == LockMode.WideArea ? wideAreaScreenRadius : precisionScreenRadius;
                return baseRadius * CanvasScaleFactor;
            }
        }

        /// <summary>Get the primary target for weapon aiming.</summary>
        public Transform GetPrimaryTarget() => primaryTarget;

        /// <summary>Get all locked targets.</summary>
        public List<Transform> GetLockedTargets() => lockedTargets;

        /// <summary>Get all detected targets (for radar).</summary>
        public List<Transform> GetAllTargets() => allTargets;

        /// <summary>Check if a target is currently locked.</summary>
        public bool IsLocked(Transform target) => lockedTargets.Contains(target);

        /// <summary>Get the world-space center of a ship's visible model (renderer bounds center).</summary>
        public static Vector3 GetModelCenter(Transform shipTransform)
        {
            var shipModel = shipTransform.Find("ShipModel");
            if (shipModel == null) return shipTransform.position;

            var renderers = shipModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return shipTransform.position;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        /// <summary>Manually set a specific target as primary.</summary>
        public void SetPrimaryTarget(Transform target)
        {
            if (target != null && !lockedTargets.Contains(target))
                lockedTargets.Add(target);
            primaryTarget = target;
        }
    }
}
