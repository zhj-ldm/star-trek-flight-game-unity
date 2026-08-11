using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Pulse cannon — sustained rapid-fire like a machine gun.
    /// Hold N to fire continuously for 5 seconds. Each shot fires two parallel
    /// blue oval projectiles. Very fast, high accuracy.
    /// </summary>
    public class PulseCannon : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ShipHealth health;
        public TargetingSystem targeting;
        public Transform firePoint;
        private Transform _phaserRing;
        private float _ringRadius;

        [Header("Firing")]
        public float damage = 15f;            // 15 per shot × 40 shots = 600 = 20% of 3000 shield
        public float speed = 417f;            // reduced to 1/4 of original 1667
        public float fireRate = 0.2f;       // doubled interval = half the shots per second
        public float sustainedDuration = 1f; // synced to phaserClip length in Start()
        public float rechargeTime = 4f;      // cooldown after sustained fire
        public float projectileLifetime = 3f;
        public float projectileScale = 0.5f;
        public float parallelOffset = 0.8f;   // distance between the two parallel shots

        [Header("Visual")]
        public Color projectileColor = Color.white;

        [Header("State")]
        public float cooldownTimer;
        private bool _isFiring;
        private float _fireTimer;
        private float _sustainedTimer;
        private AudioSource _audioSource;

        void Start()
        {
            if (firePoint == null)
                firePoint = transform;

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

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = false;  // play once, not looped — duration synced with clip
            _audioSource.spatialBlend = 0f;
            _audioSource.dopplerLevel = 0f;
            _audioSource.volume = 0.4f;

            // Use full phaser sound, synced duration
            var sfx = GetComponentInParent<ShipSFX>();
            if (sfx != null && sfx.phaserClip != null)
            {
                _audioSource.clip = sfx.phaserClip;
                sustainedDuration = sfx.phaserClip.length;  // sync fire duration to audio clip
            }
        }

        void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (_isFiring)
            {
                _sustainedTimer -= Time.deltaTime;
                _fireTimer -= Time.deltaTime;

                if (_fireTimer <= 0f)
                {
                    FireDualShot();
                    _fireTimer = fireRate;
                }

                // Stop when sustained duration ends or energy runs out
                if (_sustainedTimer <= 0f)
                {
                    StopFiring();
                }
            }
        }

        /// <summary>Start sustained rapid fire.</summary>
        public bool Fire()
        {
            if (cooldownTimer > 0f) return false;
            if (_isFiring) return false;
            if (health != null && health.currentEnergy < 10f) return false;

            _isFiring = true;
            _sustainedTimer = sustainedDuration;
            _fireTimer = 0f;

            // Start looping sound
            if (_audioSource != null && _audioSource.clip != null)
                _audioSource.Play();

            return true;
        }

        /// <summary>Stop firing (called automatically or when N released).</summary>
        public void StopFiring()
        {
            if (!_isFiring) return;
            _isFiring = false;
            cooldownTimer = rechargeTime;

            // Stop sound
            if (_audioSource != null)
                _audioSource.Stop();
        }

        private void FireDualShot()
        {
            if (firePoint == null) return;
            if (health != null && !health.SpendEnergy(1f))
            {
                StopFiring();
                return;
            }

            Transform target = targeting != null ? targeting.GetPrimaryTarget() : null;
            Vector3 dir;
            Vector3 firePos;

            // Use ring position if available
            if (_phaserRing != null && target != null)
            {
                Vector3 targetDir = (TargetingSystem.GetModelCenter(target) - _phaserRing.position).normalized;
                float angle = GetRingAngleForDirection(targetDir);
                firePos = GetRingWorldPosition(angle);
            }
            else
            {
                firePos = firePoint.position;
            }

            if (target != null)
            {
                Vector3 targetPos = TargetingSystem.GetModelCenter(target);
                Vector3 targetVel = Vector3.zero;
                var targetCtrl = target.GetComponent<ShipController>();
                if (targetCtrl != null)
                    targetVel = targetCtrl.velocity;

                float dist = Vector3.Distance(firePos, targetPos);
                float timeToHit = dist / speed;
                Vector3 predictedPos = targetPos + targetVel * timeToHit;
                dir = (predictedPos - firePos).normalized;
            }
            else
            {
                dir = transform.forward;
            }

            // Two parallel shots offset left and right
            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;

            SpawnProjectile(firePos + right * parallelOffset, dir);
            SpawnProjectile(firePos - right * parallelOffset, dir);
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

        private void SpawnProjectile(Vector3 pos, Vector3 dir)
        {
            var obj = new GameObject("PulseProjectile");
            obj.transform.position = pos;
            obj.transform.forward = dir;

            // Kinematic Rigidbody for trigger detection (no physics simulation)
            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = obj.AddComponent<SphereCollider>();
            col.radius = 1.2f;
            col.isTrigger = true;

            var mr = obj.AddComponent<MeshRenderer>();
            var mf = obj.AddComponent<MeshFilter>();
            mf.mesh = CreateSphereMesh();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetColor("_Color", projectileColor);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Oval shape: longer in flight direction
            obj.transform.localScale = new Vector3(projectileScale, projectileScale * 0.6f, projectileScale * 1.5f);

            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = projectileColor;
            light.range = 5f;
            light.intensity = 1.5f;

            var proj = obj.AddComponent<PulseProjectile>();
            proj.damage = damage;
            proj.lifetime = projectileLifetime;
            proj.speed = speed;  // CRITICAL: match projectile speed to lead-calculation speed
            proj.launcherTag = gameObject.tag;
        }

        public float CooldownProgress => cooldownTimer <= 0f ? 1f : 1f - Mathf.Clamp01(cooldownTimer / rechargeTime);
        public bool IsFiring => _isFiring;

        private static Mesh _sphereMesh;
        private static Mesh CreateSphereMesh()
        {
            if (_sphereMesh == null)
            {
                // Use Unity's built-in sphere mesh directly — avoids creating a GameObject
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphereMesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
                DestroyImmediate(temp);
            }
            return _sphereMesh;
        }
    }

    /// <summary>
    /// Pulse projectile — fast oval, damages on hit.
    /// No Rigidbody — manual Transform movement.
    /// </summary>
    public class PulseProjectile : MonoBehaviour
    {
        public float damage = 25f;
        public float lifetime = 2f;
        public float speed = 1667f;
        public string launcherTag = "Player";

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
            transform.position += _velocity * dt;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_hit) return;
            if (other.isTrigger && !other.GetComponent<ShipHealth>()) return;
            if (other.gameObject.CompareTag(launcherTag)) return;

            var shipHealth = other.GetComponent<ShipHealth>();
            if (shipHealth == null)
                shipHealth = other.GetComponentInParent<ShipHealth>();

            if (shipHealth != null && !shipHealth.gameObject.CompareTag(launcherTag))
            {
                _hit = true;
                shipHealth.TakeDamage(damage, DamageType.Energy);

                var shieldVis = shipHealth.GetComponent<ShieldVisualizer>();
                if (shieldVis != null) shieldVis.RegisterHit(transform.position);

                Explosion3D.Spawn(transform.position, 0.2f);
                Destroy(gameObject);
            }
        }
    }
}
