using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Ion pulse weapon — control weapon that disrupts enemy systems.
    /// Fires a fast ion projectile toward the primary target.
    /// On hit: applies IonEffect (slows engines, locks weapons) for a duration.
    /// Creates a blue-purple shockwave on impact.
    /// </summary>
    public class IonPulseWeapon : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ShipHealth health;
        public TargetingSystem targeting;
        public Transform[] firePoints;
        private Transform _phaserRing;
        private float _ringRadius;

        [Header("State (read-only)")]
        public float cooldownTimer;

        [Header("Visual")]
        public Color pulseColor = new Color(1f, 0.3f, 0.1f, 0.8f);
        public Color waveColor = new Color(1f, 0.2f, 0.05f, 0.6f);

        void Start()
        {
            if (firePoints == null || firePoints.Length == 0)
                firePoints = new Transform[] { transform };

            // Find PhaserRing for ring-based firing
            var hp = transform.Find("WeaponHardpoints");
            if (hp != null)
            {
                _phaserRing = hp.Find("PhaserRing");
                if (_phaserRing != null)
                {
                    var mf = _phaserRing.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        _ringRadius = mf.sharedMesh.bounds.extents.x;
                }
            }
        }

        void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;
        }

        /// <summary>Fire the ion pulse toward the primary target.</summary>
        public bool Fire()
        {
            if (cooldownTimer > 0f) return false;
            if (health != null && !health.SpendEnergy(stats.ionPulseEnergyCost)) return false;

            Transform target = targeting != null ? targeting.GetPrimaryTarget() : null;

            // Fire from ring point closest to enemy
            Vector3 firePos;
            Vector3 fireDir;
            if (_phaserRing != null && target != null)
            {
                Vector3 targetCenter = TargetingSystem.GetModelCenter(target);
                Vector3 targetDir = (targetCenter - _phaserRing.position).normalized;
                float angle = GetRingAngleForDirection(targetDir);
                firePos = GetRingWorldPosition(angle);
                fireDir = (targetCenter - firePos).normalized;
            }
            else
            {
                firePos = firePoints[0] != null ? firePoints[0].position : transform.position;
                fireDir = target != null ? (TargetingSystem.GetModelCenter(target) - firePos).normalized : transform.forward;
            }

            SpawnIonPulse(firePos, fireDir, target);

            cooldownTimer = stats.ionPulseCooldown;
            return true;
        }

        private float GetRingAngleForDirection(Vector3 worldDir)
        {
            Vector3 localDir = _phaserRing.InverseTransformDirection(worldDir);
            Vector2 planeDir = new Vector2(localDir.x, localDir.y);
            if (planeDir.sqrMagnitude < 0.0001f) planeDir = Vector2.up;
            return Mathf.Atan2(planeDir.y, planeDir.x);
        }

        private Vector3 GetRingWorldPosition(float angle)
        {
            Vector3 localPos = new Vector3(Mathf.Cos(angle) * _ringRadius, Mathf.Sin(angle) * _ringRadius, 0);
            return _phaserRing.TransformPoint(localPos);
        }

        private void SpawnIonPulse(Vector3 position, Vector3 forward, Transform target)
        {
            var pulseObj = new GameObject("IonPulse");
            pulseObj.transform.position = position;

            // Direction: toward target if available, otherwise forward
            Vector3 dir = forward;
            if (target != null)
                dir = (TargetingSystem.GetModelCenter(target) - position).normalized;
            pulseObj.transform.forward = dir;

            // Kinematic Rigidbody for trigger detection (no physics simulation)
            var rb = pulseObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Collider (trigger)
            var col = pulseObj.AddComponent<SphereCollider>();
            col.radius = 0.8f;
            col.isTrigger = true;

            // Visual: glowing sphere
            var mr = pulseObj.AddComponent<MeshRenderer>();
            var mf = pulseObj.AddComponent<MeshFilter>();
            mf.mesh = CreateSphereMesh();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetColor("_Color", pulseColor);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pulseObj.transform.localScale = Vector3.one * 0.8f;

            // Light
            var light = pulseObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = pulseColor;
            light.range = 10f;
            light.intensity = 2f;

            // Trail
            var trail = pulseObj.AddComponent<TrailRenderer>();
            trail.startWidth = 0.5f;
            trail.endWidth = 0.1f;
            trail.time = 0.3f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.material.SetColor("_Color", waveColor);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Projectile script
            var proj = pulseObj.AddComponent<IonPulseProjectile>();
            proj.target = target;
            proj.speed = 1667f;
            proj.slowDuration = stats.ionPulseSlowDuration;
            proj.slowFactor = stats.ionPulseSlowFactor;
            proj.damage = stats.ionPulseDamage;
            proj.launcherTag = gameObject.tag;
            proj.waveColor = waveColor;
        }

        private static Mesh _sphereMesh;
        private static Mesh CreateSphereMesh()
        {
            if (_sphereMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
            }
            return _sphereMesh;
        }

        /// <summary>Cooldown progress 0..1 (1 = ready).</summary>
        public float CooldownProgress => stats != null ? 1f - Mathf.Clamp01(cooldownTimer / stats.ionPulseCooldown) : 1f;
    }

    /// <summary>
    /// Ion pulse projectile — fast, applies IonEffect on hit.
    /// No Rigidbody — manual Transform movement.
    /// </summary>
    public class IonPulseProjectile : MonoBehaviour
    {
        public Transform target;
        public float speed = 1667f;
        public float slowDuration = 4f;
        public float slowFactor = 0.5f;
        public float damage = 20f;
        public float lifetime = 5f;
        public string launcherTag = "Player";
        public Color waveColor;

        private Vector3 _velocity;
        private float _lifeTimer;
        private bool _hit;

        void Start()
        {
            _lifeTimer = lifetime;
            _velocity = transform.forward * speed;
        }

        void Update()
        {
            if (_hit) return;

            float dt = Time.deltaTime;
            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Slight homing
            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector3 toTarget = (TargetingSystem.GetModelCenter(target) - transform.position).normalized;
                Vector3 currentDir = _velocity.normalized;
                if (currentDir.sqrMagnitude > 0.001f)
                {
                    float maxAngle = 20f * Mathf.Deg2Rad * dt;
                    Vector3 newDir = Vector3.RotateTowards(currentDir, toTarget, maxAngle, 0f);
                    _velocity = newDir * speed;
                    transform.rotation = Quaternion.LookRotation(newDir);
                }
            }

            // Manual movement
            transform.position += _velocity * dt;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_hit) return;
            if (other.isTrigger && !other.GetComponent<ShipHealth>()) return;
            if (other.gameObject.CompareTag(launcherTag)) return;

            var health = other.GetComponent<ShipHealth>();
            if (health == null)
                health = other.GetComponentInParent<ShipHealth>();

            if (health != null && !health.gameObject.CompareTag(launcherTag))
            {
                _hit = true;

                // Apply damage
                health.TakeDamage(damage, DamageType.Ion);

                // Apply ion effect
                var ion = health.GetComponent<IonEffect>();
                if (ion == null)
                    ion = health.gameObject.AddComponent<IonEffect>();
                ion.Apply(slowDuration, slowFactor);

                // Create wave VFX
                CreateWaveEffect(transform.position);
            }

            Destroy(gameObject);
        }

        private void CreateWaveEffect(Vector3 position)
        {
            // Use Explosion3D for ion wave (small scale, red)
            Explosion3D.Spawn(position, 0.3f);

            // Light flash
            var lightObj = new GameObject("IonFlash");
            lightObj.transform.position = position;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.3f, 0.05f, 1f);
            light.range = 15f;
            light.intensity = 5f;

            Destroy(lightObj, 0.5f);
        }
    }
}
