using UnityEngine;
using System.Collections;

namespace StarTrekCombat
{
    /// <summary>
    /// Photon torpedo weapon — homing, chargeable, explosive.
    /// Hold right mouse to charge (damage and speed scale with charge), release to fire.
    /// Ammo-limited with per-shot cooldown. Spawns TorpedoProjectile that tracks the primary target.
    /// </summary>
    public class TorpedoWeapon : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ShipHealth health;
        public TargetingSystem targeting;
        public Transform[] torpedoTubes;
        private Transform _phaserRing;
        private float _ringRadius;

        [Header("State (read-only)")]
        public int currentAmmo;
        public float cooldownTimer;
        public float chargeTimer;
        public bool isCharging;

        [Header("Torpedo Visual")]
        public Color torpedoColor = new Color(1f, 0.8f, 0.3f, 1f);
        public Color trailColor = new Color(1f, 0.6f, 0.2f, 0.6f);

        [Header("Dual Fire")]
        public int torpedoesPerVolley = 2;
        public float spreadAngle = 8f; // degrees of spread between torpedoes

        void Start()
        {
            if (stats == null) return;
            currentAmmo = stats.torpedoMaxAmmo;

            // Auto-set torpedo/trail colors based on ship tag/name
            SetFactionColors();

            if (torpedoTubes == null || torpedoTubes.Length == 0)
            {
                torpedoTubes = new Transform[] { transform };
            }

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

            if (isCharging)
            {
                chargeTimer += Time.deltaTime;
                if (chargeTimer > stats.torpedoChargeTime)
                    chargeTimer = stats.torpedoChargeTime;
            }
        }

        /// <summary>Set torpedo and trail colors based on ship faction.</summary>
        private void SetFactionColors()
        {
            string shipName = gameObject.name;
            // Check parent name too (weapon may be child of ship)
            if (transform.parent != null)
                shipName = transform.parent.name;

            bool isJem = shipName.Contains("jem") || shipName.Contains("Jem") || shipName.Contains("EnemyShip_1") || shipName.Contains("EnemyShip_3");
            bool isGalor = shipName.Contains("galor") || shipName.Contains("Galor") || shipName.Contains("EnemyShip_2");
            bool isGreen = shipName.Contains("romulan") || shipName.Contains("Romulan") ||
                           shipName.Contains("vorcha") || shipName.Contains("Vorcha") ||
                           shipName.Contains("vor_cha") || shipName.Contains("ktinga") ||
                           shipName.Contains("Ktinga") || shipName.Contains("EnemyShip_Green");

            if (isJem)
            {
                torpedoColor = new Color(0.6f, 0.2f, 0.9f, 1f);
                trailColor = new Color(0.5f, 0.15f, 0.8f, 0.6f);
            }
            else if (isGalor)
            {
                torpedoColor = new Color(1f, 0.2f, 0.2f, 1f);
                trailColor = new Color(0.9f, 0.15f, 0.15f, 0.6f);
            }
            else if (isGreen)
            {
                torpedoColor = new Color(0.2f, 0.8f, 0.3f, 1f);
                trailColor = new Color(0.15f, 0.6f, 0.2f, 0.6f);
            }
            else
            {
                // Player — blue
                torpedoColor = new Color(0.2f, 0.5f, 1f, 1f);
                trailColor = new Color(0.15f, 0.4f, 0.9f, 0.6f);
            }
        }

        /// <summary>Start charging the torpedo.</summary>
        public void StartCharge()
        {
            if (currentAmmo <= 0) return;
            if (cooldownTimer > 0f) return;
            isCharging = true;
            chargeTimer = 0f;
        }

        /// <summary>Fire the torpedo (release after charging).</summary>
        public void Fire()
        {
            if (!isCharging) return;
            isCharging = false;

            if (currentAmmo <= 0 || cooldownTimer > 0f)
            {
                chargeTimer = 0f;
                return;
            }

            if (health != null && !health.SpendEnergy(stats.torpedoEnergyCost))
            {
                chargeTimer = 0f;
                return;
            }

            float chargeRatio = Mathf.Clamp01(chargeTimer / stats.torpedoChargeTime);
            chargeTimer = 0f;

            // Get target — fall back to AI's currentTarget for enemy ships
            Transform target = targeting != null ? targeting.GetPrimaryTarget() : null;
            if (target == null)
            {
                var ai = GetComponentInParent<ShipAI>();
                if (ai != null) target = ai.currentTarget;
            }

            // Determine fire position: ring point closest to enemy, or ship center
            Vector3 firePos;
            Vector3 baseDir;
            if (_phaserRing != null && target != null)
            {
                Vector3 targetCenter = TargetingSystem.GetModelCenter(target);
                baseDir = (targetCenter - _phaserRing.position).normalized;
                float angle = GetRingAngleForDirection(baseDir);
                firePos = GetRingWorldPosition(angle);
            }
            else if (target != null)
            {
                // No PhaserRing (enemy ships) — fire from ship center
                firePos = transform.position;
                baseDir = (TargetingSystem.GetModelCenter(target) - firePos).normalized;
            }
            else if (torpedoTubes.Length > 0 && torpedoTubes[0] != null)
            {
                firePos = torpedoTubes[0].position;
                baseDir = transform.forward;
            }
            else
            {
                firePos = transform.position;
                baseDir = transform.forward;
            }

            // Fire sequentially from same tube (not parallel)
            int volleyCount = Mathf.Max(1, torpedoesPerVolley);
            StartCoroutine(SpawnTorpedoSequential(firePos, target, chargeRatio, volleyCount));

            currentAmmo -= volleyCount;
            if (currentAmmo < 0) currentAmmo = 0;
            cooldownTimer = stats.torpedoCooldown;

            // Play torpedo launch sound — only for player ship
            var controller = GetComponentInParent<ShipController>();
            if (controller != null && controller.isPlayerControlled)
            {
                var sfx = GetComponentInParent<ShipSFX>();
                if (sfx != null) sfx.PlayTorpedoLaunch();
            }
        }

        /// <summary>Cancel charging without firing.</summary>
        public void CancelCharge()
        {
            isCharging = false;
            chargeTimer = 0f;
        }

        private System.Collections.IEnumerator SpawnTorpedoSequential(Vector3 firePos, Transform target, float chargeRatio, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 fireDir;
                if (target != null)
                    fireDir = (TargetingSystem.GetModelCenter(target) - firePos).normalized;
                else
                    fireDir = transform.forward;

                SpawnTorpedo(firePos, fireDir, target, chargeRatio);

                if (i < count - 1)
                    yield return new WaitForSeconds(0.1f);
            }
        }

        private void SpawnTorpedo(Vector3 position, Vector3 forward, Transform target, float chargeRatio)
        {
            var torpedoObj = new GameObject("PhotonTorpedo");
            torpedoObj.transform.position = position;
            torpedoObj.transform.forward = forward;

            // Kinematic Rigidbody — needed for OnTriggerEnter (no physics simulation, no jitter)
            var rb = torpedoObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Collider (trigger for hit detection) — disabled on spawn, armed after delay
            var col = torpedoObj.AddComponent<SphereCollider>();
            col.radius = 2f;
            col.isTrigger = true;
            col.enabled = false; // Disabled until TorpedoProjectile arms it

            // Ignore collision with launcher ship's collider entirely
            // This prevents self-detonation regardless of speed/position
            var launcherColliders = GetComponentsInParent<Collider>();
            foreach (var lc in launcherColliders)
            {
                if (lc != null)
                    Physics.IgnoreCollision(col, lc);
            }
            var ownCol = GetComponent<Collider>();
            if (ownCol != null)
                Physics.IgnoreCollision(col, ownCol);

            // Visual: octahedron prism with self-emission (no trail)
            var mr = torpedoObj.AddComponent<MeshRenderer>();
            var mf = torpedoObj.AddComponent<MeshFilter>();
            mf.mesh = CreateOctahedronMesh();
            var mat = new Material(Shader.Find("Standard"));
            mat.SetColor("_Color", torpedoColor);
            mat.SetColor("_EmissionColor", torpedoColor);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            torpedoObj.transform.localScale = Vector3.one * 0.3f;

            // Light
            var light = torpedoObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = torpedoColor;
            light.range = 8f;
            light.intensity = 2f;

            // No trail — prism shape only

            // Projectile script
            var projectile = torpedoObj.AddComponent<TorpedoProjectile>();
            projectile.target = target;

            // Base speed from charge
            float baseSpeed = stats.torpedoSpeed * (0.5f + chargeRatio * 0.5f);

            // Add ship velocity for warp speed stacking
            var controller = GetComponentInParent<ShipController>();
            if (controller != null)
                baseSpeed += controller.currentSpeed;

            projectile.speed = baseSpeed;
            projectile.damage = stats.torpedoDamage * (0.5f + chargeRatio * 0.5f);
            projectile.explosionRadius = stats.torpedoExplosionRadius;
            projectile.knockbackForce = 300f * (0.5f + chargeRatio);
            projectile.launcherTag = gameObject.tag;
            // Store launcher root so torpedo can ignore ALL colliders on its own ship
            projectile.launcherTransform = transform.root;
        }

        private static Mesh _octahedronMesh;
        private static Mesh CreateOctahedronMesh()
        {
            if (_octahedronMesh != null) return _octahedronMesh;

            var mesh = new Mesh();
            // 6 vertices: top, bottom, and 4 around the equator
            Vector3[] verts = new Vector3[]
            {
                new Vector3(0, 1, 0),    // 0: top
                new Vector3(0, -1, 0),   // 1: bottom
                new Vector3(1, 0, 0),    // 2: +X
                new Vector3(0, 0, 1),    // 3: +Z
                new Vector3(-1, 0, 0),   // 4: -X
                new Vector3(0, 0, -1),   // 5: -Z
            };

            // 8 triangular faces (top 4 + bottom 4)
            int[] tris = new int[]
            {
                0, 2, 3,  // top +X +Z
                0, 3, 4,  // top +Z -X
                0, 4, 5,  // top -X -Z
                0, 5, 2,  // top -Z +X
                1, 3, 2,  // bottom +Z +X
                1, 4, 3,  // bottom -X +Z
                1, 5, 4,  // bottom -Z -X
                1, 2, 5,  // bottom +X -Z
            };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _octahedronMesh = mesh;
            return mesh;
        }

        /// <summary>Charge progress 0..1.</summary>
        public float ChargeProgress => isCharging ? Mathf.Clamp01(chargeTimer / stats.torpedoChargeTime) : 0f;

        /// <summary>Cooldown progress 0..1 (1 = ready).</summary>
        public float CooldownProgress => stats != null ? 1f - Mathf.Clamp01(cooldownTimer / stats.torpedoCooldown) : 1f;

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
    }
}
