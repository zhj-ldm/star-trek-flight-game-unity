using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Base AI controller for ships. State machine + movement utilities.
    /// Uses direct velocity/rotation (no Rigidbody).
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public abstract class ShipAI : MonoBehaviour
    {
        [Header("AI Configuration")]
        public AIDifficulty difficulty = AIDifficulty.Easy;
        public float decisionInterval = 0.5f;
        public float engageRange = 300f;
        public float optimalRange = 100f;
        public float retreatThreshold = 0.25f;
        public float regroupThreshold = 0.5f;

        [Header("Player Seek")]
        [Tooltip("在此距离内的敌舰可以感知到玩家")]
        public float playerSeekRange = 2000000f;

        [Header("AI Movement")]
        public float approachSpeed = 80f;
        public float combatMaxSpeed = 25f;
        public float patrolSpeed = 15f;
        public float approachTurnRate = 90f;
        public float combatTurnRate = 60f;

        [Header("AI Weapon Tuning")]
        public float phaserAimThreshold = 0.3f;
        public float torpedoAimThreshold = 0.6f;
        public float torpedoFireInterval = 5f;
        public float torpedoMaxRange = 800f;
        private float _torpedoFireTimer;

        [Header("State (read-only)")]
        public AIState currentState = AIState.Idle;
        public Transform currentTarget;

        protected ShipController _controller;
        protected ShipHealth _health;
        protected ShipWeaponManager _weapons;
        protected TargetingSystem _targeting;

        protected float _decisionTimer;
        protected float _stateTimer;
        protected Vector3 _strafeDir;
        protected float _strafeTimer;

        protected float _reactionTime => difficulty == AIDifficulty.Epic ? 0.2f : difficulty == AIDifficulty.Hard ? 0.35f : difficulty == AIDifficulty.Normal ? 0.5f : 0.8f;
        protected float _aimAccuracy => difficulty == AIDifficulty.Epic ? 0.95f : difficulty == AIDifficulty.Hard ? 0.8f : difficulty == AIDifficulty.Normal ? 0.6f : 0.35f;

        protected virtual void Awake()
        {
            _controller = GetComponent<ShipController>();
            _health = GetComponent<ShipHealth>();
            _weapons = GetComponent<ShipWeaponManager>();
            _targeting = GetComponent<TargetingSystem>();
        }

        protected virtual void Start()
        {
            _decisionTimer = Random.Range(0f, decisionInterval);
            _torpedoFireTimer = Random.Range(2f, torpedoFireInterval);
            PickNewStrafeDir();
        }

        protected virtual void Update()
        {
            if (_health == null || !_health.IsAlive) return;

            _decisionTimer -= Time.deltaTime;
            _stateTimer += Time.deltaTime;

            if (_decisionTimer <= 0f)
            {
                _decisionTimer = _reactionTime + Random.Range(0f, 0.2f);
                MakeDecision();
            }

            _strafeTimer -= Time.deltaTime;
            if (_strafeTimer <= 0f)
                PickNewStrafeDir();

            // Execute movement in Update — same frame as ShipController position integration
            if (currentState != AIState.Engage && TryPlanetAvoidance()) return;
            ExecuteState();
        }

        protected virtual void FixedUpdate()
        {
            // AI movement runs in Update to stay in sync with ShipController position integration.
        }

        protected abstract void MakeDecision();
        protected abstract void ExecuteState();

        // ═══════════════════════════════════════════
        //  Movement
        // ═══════════════════════════════════════════

        protected void MoveToward(Vector3 targetPos, bool maintainRange = false)
        {
            if (_controller == null) return;
            Vector3 toTarget = targetPos - transform.position;
            float dist = toTarget.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = toTarget.normalized;
            float lerpFactor = 3f * Time.deltaTime;

            if (maintainRange)
            {
                if (dist < optimalRange * 0.7f)
                {
                    FaceDirection(-dir, combatTurnRate);
                    _controller.velocity = Vector3.Lerp(_controller.velocity, -transform.forward * combatMaxSpeed, lerpFactor);
                }
                else if (dist > optimalRange * 1.3f)
                {
                    FaceDirection(dir, approachTurnRate);
                    if (Vector3.Dot(transform.forward, dir) > 0.3f)
                        _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * approachSpeed, lerpFactor);
                    else
                        _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, lerpFactor);
                }
                else
                {
                    // In range — strafe slowly, don't fly away
                    FaceDirection(dir, combatTurnRate);
                    Vector3 strafe = _strafeDir * combatMaxSpeed * 0.5f;
                    _controller.velocity = Vector3.Lerp(_controller.velocity, strafe, lerpFactor);
                }
            }
            else
            {
                FaceDirection(dir, approachTurnRate);
                if (dist > 10f && Vector3.Dot(transform.forward, dir) > 0.3f)
                    _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * approachSpeed, lerpFactor);
                else
                    _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, lerpFactor);
            }
        }

        /// <summary>Slow orbit around a center point at patrolSpeed.</summary>
        protected void OrbitAround(Vector3 center, float radius, float speed)
        {
            if (_controller == null) return;
            Vector3 toCenter = center - transform.position;
            float dist = toCenter.magnitude;
            Vector3 dir = toCenter.normalized;

            // Pick a perpendicular orbit direction (consistent per ship)
            Vector3 orbitDir = Vector3.Cross(dir, Vector3.up).normalized;
            if (orbitDir.sqrMagnitude < 0.01f)
                orbitDir = Vector3.right;

            float lerpFactor = 3f * Time.deltaTime;

            // Steer toward orbit tangent, adjust radius
            if (dist > radius * 1.2f)
            {
                FaceDirection(dir, approachTurnRate);
                _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * speed, lerpFactor);
            }
            else if (dist < radius * 0.8f)
            {
                FaceDirection(-dir, approachTurnRate);
                _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * speed, lerpFactor);
            }
            else
            {
                FaceDirection(orbitDir, approachTurnRate);
                _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * speed, lerpFactor);
            }
            _controller.angularVelocity = Vector3.zero;
        }

        protected void SteerToward(Vector3 dir)
        {
            FaceDirection(dir, approachTurnRate);
        }

        protected void FaceDirection(Vector3 dir, float turnRate)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnRate * Time.deltaTime);
            _controller.angularVelocity = Vector3.zero;
        }

        protected void ApplyThrust(float dir)
        {
            if (_controller == null) return;
            if (Mathf.Abs(dir) < 0.01f)
            {
                _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, 5f * Time.deltaTime);
                return;
            }
            float speed = approachSpeed * dir * _controller.SpeedModifier;
            _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * speed, 3f * Time.deltaTime);
        }

        protected void Brake()
        {
            if (_controller == null) return;
            _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, 10f * Time.deltaTime);
            _controller.angularVelocity = Vector3.zero;
        }

        // ═══════════════════════════════════════════
        //  Planet avoidance
        // ═══════════════════════════════════════════

        protected bool TryPlanetAvoidance()
        {
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            var renderers = GameObject.FindObjectsOfType<MeshRenderer>();

            Vector3 totalPush = Vector3.zero;
            bool danger = false;

            foreach (var mr in renderers)
            {
                if (mr == null || !mr.gameObject.name.Contains("Planet")) continue;
                if (!mr.gameObject.activeInHierarchy) continue;

                Vector3 planetPos = mr.transform.position;
                Vector3 toPlanet = planetPos - pos;
                float distToPlanet = toPlanet.magnitude;
                float planetRadius = mr.bounds.size.magnitude * 0.5f;
                float safeDist = planetRadius + 100f;

                if (distToPlanet > safeDist * 2f) continue;

                if (distToPlanet < safeDist)
                {
                    Vector3 pushDir = -toPlanet.normalized;
                    float urgency = 1f - (distToPlanet / safeDist);
                    totalPush += pushDir * urgency;
                    danger = true;
                }
                else
                {
                    float dot = Vector3.Dot(fwd, toPlanet.normalized);
                    if (dot > 0.3f)
                    {
                        float urgency = (1f - (distToPlanet / (safeDist * 2f))) * dot * 0.5f;
                        Vector3 avoidDir = Vector3.Cross(toPlanet.normalized, Vector3.up).normalized;
                        if (Vector3.Dot(avoidDir, fwd) < 0f) avoidDir = -avoidDir;
                        totalPush += avoidDir * urgency;
                        danger = true;
                    }
                }
            }

            if (danger)
            {
                Vector3 avoidDir = totalPush.normalized;
                if (avoidDir.sqrMagnitude > 0.01f)
                {
                    FaceDirection(avoidDir, approachTurnRate);
                    _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * approachSpeed, 3f * Time.deltaTime);
                }
                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════
        //  Weapons
        // ═══════════════════════════════════════════

        protected void FireWeapons()
        {
            if (_weapons == null) return;
            if (_torpedoFireTimer > 0f) _torpedoFireTimer -= Time.deltaTime;

            if (currentTarget != null)
            {
                Vector3 toTarget = (currentTarget.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toTarget);
                float dist = Vector3.Distance(transform.position, currentTarget.position);

                SteerToward(toTarget);

                bool isWeaponLocked = _weapons.weaponsLocked;
                float effectivePhaserThreshold = phaserAimThreshold * Mathf.Lerp(0.5f, 1f, _aimAccuracy);
                bool canFirePhaser = dot > effectivePhaserThreshold && dist < 600f;

                if (canFirePhaser && !isWeaponLocked)
                {
                    if (_weapons.phaser != null && _weapons.phaser.CanFire())
                        _weapons.phaser.StartFire();
                }
                else if (!canFirePhaser)
                {
                    if (_weapons.phaser != null) _weapons.phaser.StopFire();
                }

                float effectiveTorpedoThreshold = torpedoAimThreshold * Mathf.Lerp(0.7f, 1f, _aimAccuracy);
                if (dot > effectiveTorpedoThreshold && dist < torpedoMaxRange && _torpedoFireTimer <= 0f)
                {
                    if (_weapons.torpedo != null && _weapons.torpedo.currentAmmo > 0)
                    {
                        _weapons.torpedo.StartCharge();
                        _weapons.torpedo.chargeTimer = _controller.stats.torpedoChargeTime * 0.5f;
                        _weapons.torpedo.Fire();
                        _torpedoFireTimer = torpedoFireInterval;
                    }
                }
            }
            else
            {
                if (_weapons.phaser != null) _weapons.phaser.StopFire();
            }
        }

        protected void CeaseFire()
        {
            if (_weapons != null && _weapons.phaser != null)
                _weapons.phaser.StopFire();
        }

        // ═══════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════

        private void PickNewStrafeDir()
        {
            _strafeDir = Random.onUnitSphere;
            _strafeDir.y *= 0.3f;
            _strafeDir.Normalize();
            _strafeTimer = Random.Range(2f, 5f);
        }

        protected float HealthPercent => _health != null ? _health.HullPercent : 0f;
    }
}
