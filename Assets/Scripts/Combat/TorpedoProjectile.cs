using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Photon torpedo — homing projectile that tracks its target.
    /// Turns toward target with limited turn rate, explodes on impact.
    /// No Rigidbody — manual Transform movement.
    /// </summary>
    public class TorpedoProjectile : MonoBehaviour
    {
        [Header("Configuration")]
        public Transform target;
        public float speed = 24f;
        public float damage = 150f;
        public float explosionRadius = 25f;
        public float knockbackForce = 200f;
        public float lifetime = 8f;
        public DamageType damageType = DamageType.Explosive;
        public string launcherTag = "Player";
        public Transform launcherTransform; // Root transform of the ship that fired this

        [Header("Homing")]
        public float turnRate = 90f;       // deg/s — how fast torpedo can turn
        public float homingDelay = 0.3f;    // seconds before homing activates
        public float homingRange = 2000f;   // max distance to target for homing

        [Header("Arming")]
        [Tooltip("Collider disabled for this many seconds after launch to avoid hitting own ship.")]
        public float armDelay = 0.3f;

        private Vector3 _velocity;
        private float _lifeTimer;
        private bool _exploded;
        private Collider _collider;
        private float _armTimer;

        void Start()
        {
            _lifeTimer = lifetime;
            _velocity = transform.forward * speed;
            _collider = GetComponent<Collider>();
            _armTimer = armDelay;
            if (_collider != null) _collider.enabled = false;
        }

        void Update()
        {
            if (_exploded) return;

            float dt = Time.deltaTime;

            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Explode();
                return;
            }

            // Arming delay — enable collider after safe distance from launcher
            if (_armTimer > 0f)
            {
                _armTimer -= dt;
                if (_armTimer <= 0f && _collider != null)
                    _collider.enabled = true;
            }

            // Homing guidance — track target after initial delay
            if (target != null && _lifeTimer < lifetime - homingDelay)
            {
                var targetHealth = target.GetComponent<ShipHealth>();
                if (targetHealth == null) targetHealth = target.GetComponentInParent<ShipHealth>();
                if (targetHealth != null && !targetHealth.IsAlive)
                {
                    target = null;
                }
                else
                {
                    Vector3 targetCenter = TargetingSystem.GetModelCenter(target);
                    float distToTarget = Vector3.Distance(transform.position, targetCenter);
                    if (distToTarget <= homingRange)
                    {
                        Vector3 desiredDir = (targetCenter - transform.position).normalized;
                        Vector3 currentDir = _velocity.normalized;

                        Vector3 newDir = Vector3.RotateTowards(
                            currentDir,
                            desiredDir,
                            turnRate * Mathf.Deg2Rad * dt,
                            0f
                        );

                        _velocity = newDir * speed;
                        transform.rotation = Quaternion.LookRotation(newDir);
                    }
                }
            }

            // Maintain speed
            if (_velocity.sqrMagnitude < speed * speed * 0.5f)
                _velocity = transform.forward * speed;

            // Manual movement
            transform.position += _velocity * dt;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_exploded) return;
            // Still arming — don't explode
            if (_collider != null && !_collider.enabled) return;

            // Skip if hit is part of launcher ship (any child, any depth)
            if (launcherTransform != null && (other.transform == launcherTransform || other.transform.IsChildOf(launcherTransform)))
                return;
            if (!string.IsNullOrEmpty(launcherTag) && other.gameObject.CompareTag(launcherTag)) return;

            // Only explode on actual ships — ignore planets, sun, asteroids, etc.
            var targetHealth = other.GetComponent<ShipHealth>();
            if (targetHealth == null)
                targetHealth = other.GetComponentInParent<ShipHealth>();
            if (targetHealth == null) return;

            Explode();
        }

        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector3 center = transform.position;

            // AoE damage
            Collider[] hits = Physics.OverlapSphere(center, explosionRadius);
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<ShipHealth>();
                if (health == null)
                    health = hit.GetComponentInParent<ShipHealth>();

                if (health != null && !health.gameObject.CompareTag(launcherTag))
                {
                    // Also skip if collider is part of launcher ship
                    if (launcherTransform != null && (hit.transform == launcherTransform || hit.transform.IsChildOf(launcherTransform)))
                        continue;

                    float dist = Vector3.Distance(center, health.transform.position);
                    float falloff = 1f - Mathf.Clamp01(dist / explosionRadius) * 0.5f;
                    health.TakeDamage(damage * falloff, damageType);

                    // Trigger shield flash
                    var shieldVis = health.GetComponent<ShieldVisualizer>();
                    if (shieldVis != null) shieldVis.RegisterHit(center);
                }
            }

            // Spawn 3D explosion effect (smaller scale for torpedo)
            Explosion3D.Spawn(center, 0.5f);

            // Play torpedo hit sound
            var sfx = FindFirstObjectByType<ShipSFX>();
            if (sfx != null) sfx.PlayTorpedoHit();

            // Destroy torpedo immediately
            Destroy(gameObject);
        }
    }
}
