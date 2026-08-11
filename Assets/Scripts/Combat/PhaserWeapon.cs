using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Ring-based phaser weapon.
    /// On fire: 2 yellow dots appear on PhaserRing, converge to the point
    /// closest to the enemy, then fire a single beam from that point.
    /// </summary>
    public class PhaserWeapon : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ShipHealth health;
        public TargetingSystem targeting;

        [Header("Fire Cycle")]
        public float fireDuration = 6f;
        public float rechargeTime = 2f;
        public float damageMultiplier = 1f;

        [Header("Visual")]
        public Color beamColor = new Color(1f, 0.85f, 0.1f, 0.9f);
        public float beamWidth = 0.4f;
        public float beamEndWidth = 0.15f;

        [Header("Ring Convergence")]
        [Tooltip("Time for 2 dots to converge (seconds).")]
        public float convergeDuration = 1.5f;
        [Tooltip("Initial separation as fraction of ship model width (1/3 = 0.33).")]
        public float initialSeparationFraction = 0.33f;
        [Tooltip("Yellow dot size.")]
        public float dotSize = 0.075f;

        [Header("State (read-only)")]
        public PhaserState phaserState = PhaserState.Ready;
        public bool isFiring => phaserState == PhaserState.Firing;

        // Internal
        private Transform _phaserRing;
        private float _ringRadius;
        private float _modelWidth;
        private LineRenderer _beam;
        private Material _beamMat;

        // Convergence dots
        private GameObject _dotA;
        private GameObject _dotB;
        private float _convergeTimer;
        private float _convergeStartAngleA;
        private float _convergeStartAngleB;
        private float _convergeEndAngle;

        private float _stateTimer;

        private ShipSFX _sfx;
        private float _originalFireDuration;

        // Hit VFX
        private GameObject _hitExplosionObj;
        private ParticleSystem _hitExplosionPS;
        private Light _hitExplosionLight;

        public enum PhaserState { Ready, Converging, Firing, Recharging }

        void Start()
        {
            // Auto-get stats/health/targeting from parent if not set (enemy ships)
            if (stats == null)
            {
                var ctrl = GetComponentInParent<ShipController>();
                if (ctrl != null) stats = ctrl.stats;
            }
            if (health == null)
                health = GetComponentInParent<ShipHealth>();
            if (targeting == null)
                targeting = GetComponentInParent<TargetingSystem>();

            // Find PhaserRing under WeaponHardpoints
            var hp = transform.Find("WeaponHardpoints");
            if (hp != null)
            {
                _phaserRing = hp.Find("PhaserRing");
            }
            // If not in WeaponHardpoints, deep-search ShipModel subtree for PhaserRing
            // (GLB ships like Voyager/Defiant keep ring under ShipModel/XxxModel)
            if (_phaserRing == null)
            {
                var sm = transform.Find("ShipModel");
                if (sm != null)
                    _phaserRing = DeepFind(sm, "PhaserRing");
            }
            // If still no PhaserRing found (enemy ships), fire from ship center
            if (_phaserRing == null)
                _phaserRing = null;

            // Sync fire duration to phaser audio clip length so beam stops when sound ends
            _sfx = GetComponentInParent<ShipSFX>();
            _originalFireDuration = fireDuration;
            if (_sfx != null && _sfx.PhaserClipLength > 0f)
                fireDuration = _sfx.PhaserClipLength;

            // Hide PhaserRing mesh in play mode
            if (_phaserRing != null)
            {
                var ringRenderer = _phaserRing.GetComponent<MeshRenderer>();
                if (ringRenderer != null)
                    ringRenderer.enabled = false;

                // Get ring radius from mesh bounds (local space, unscaled)
                var mf = _phaserRing.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    _ringRadius = mf.sharedMesh.bounds.extents.x;
                }
            }
            else
            {
                // No PhaserRing (enemy ships) — use default radius
                _ringRadius = 3f;
            }

            // Get ship model width
            var shipModel = transform.Find("ShipModel");
            if (shipModel != null)
            {
                var renderers = shipModel.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);
                    _modelWidth = bounds.size.x;
                }
            }
            if (_modelWidth < 0.1f) _modelWidth = 8f;

            // Create beam LineRenderer
            var beamObj = new GameObject("PhaserBeam");
            beamObj.transform.SetParent(transform, false); // Parent to ship, not ring
            _beam = beamObj.AddComponent<LineRenderer>();
            _beamMat = new Material(Shader.Find("Sprites/Default"));
            _beamMat.SetColor("_Color", beamColor);
            _beamMat.renderQueue = 3001;
            _beam.startWidth = beamWidth;
            _beam.endWidth = beamEndWidth;
            _beam.material = _beamMat;
            _beam.startColor = beamColor;
            _beam.endColor = beamColor;
            _beam.enabled = false;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;

            // Create convergence dots (hidden initially)
            _dotA = CreateDot("DotA");
            _dotB = CreateDot("DotB");
            _dotA.SetActive(false);
            _dotB.SetActive(false);
        }

        private GameObject CreateDot(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false); // Parent to ship, not ring

            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempSphere);

            var mf = obj.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = obj.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.SetColor("_Color", new Color(1f, 0.8f, 0f, 1f));
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            obj.transform.SetParent(transform, false); // Parent to ship, not ring, to avoid scale distortion
            obj.transform.localScale = Vector3.one * dotSize;
            return obj;
        }

        /// <summary>Recursively find a child by name.</summary>
        static Transform DeepFind(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = DeepFind(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Get the angle (radians) on the ring for a world direction.</summary>
        private float GetRingAngleForDirection(Vector3 worldDir)
        {
            if (_phaserRing == null) return 0f;
            // Ring is in local X-Y plane. Transform direction to ring local space.
            Vector3 localDir = _phaserRing.InverseTransformDirection(worldDir);
            // Project onto X-Y plane (ring plane, normal = Z)
            Vector2 planeDir = new Vector2(localDir.x, localDir.y);
            if (planeDir.sqrMagnitude < 0.0001f) planeDir = Vector2.up;
            return Mathf.Atan2(planeDir.y, planeDir.x);
        }

        /// <summary>Get world position on ring at given angle (on the ring itself).</summary>
        private Vector3 GetRingWorldPosition(float angle)
        {
            // No PhaserRing (enemy ships) — fire from ship center
            if (_phaserRing == null)
                return transform.position;

            Vector3 localPos = new Vector3(Mathf.Cos(angle) * _ringRadius, Mathf.Sin(angle) * _ringRadius, 0);
            return _phaserRing.TransformPoint(localPos);
        }

        /// <summary>Get ring position projected onto model upper surface (along ring's up direction).</summary>
        private Vector3 GetRingPosition(float angle)
        {
            Vector3 ringPos = GetRingWorldPosition(angle);

            // No PhaserRing (enemy ships) — just return center position
            if (_phaserRing == null)
                return ringPos;

            // Project upward (ring's local up) to model surface
            Vector3 upDir = _phaserRing.up;

            var shipModel = transform.Find("ShipModel");
            if (shipModel != null)
            {
                var renderers = shipModel.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    // Ray from ringPos upward to find model top surface
                    float t = RayBoxIntersection(ringPos, upDir, bounds);
                    if (t > 0f)
                        return ringPos + upDir * t;
                }
            }

            return ringPos;
        }

        /// <summary>Ray-box intersection, returns nearest positive distance.</summary>
        private float RayBoxIntersection(Vector3 origin, Vector3 dir, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            float tmin = float.MinValue;
            float tmax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float o = origin[i];
                float d = dir[i];
                float mn = min[i];
                float mx = max[i];

                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < mn || o > mx) return -1f;
                }
                else
                {
                    float t1 = (mn - o) / d;
                    float t2 = (mx - o) / d;
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                    tmin = Mathf.Max(tmin, t1);
                    tmax = Mathf.Min(tmax, t2);
                    if (tmin > tmax) return -1f;
                }
            }

            // Return nearest positive intersection
            if (tmin > 0f) return tmin;
            if (tmax > 0f) return tmax;
            return -1f;
        }

        public void StartFire()
        {
            if (phaserState == PhaserState.Ready)
            {
                if (health != null && health.currentEnergy < 5f) return;

                // No PhaserRing (enemy ships) — skip convergence, fire immediately
                if (_phaserRing == null)
                {
                    phaserState = PhaserState.Firing;
                    _stateTimer = 0f;
                    if (_sfx != null) _sfx.PlayPhaserFire();
                    return;
                }

                // Determine convergence point = closest point on ring to enemy direction
                Transform primaryTarget = GetTarget();
                Vector3 targetDir;
                if (primaryTarget != null)
                {
                    targetDir = (TargetingSystem.GetModelCenter(primaryTarget) - _phaserRing.position).normalized;
                }
                else
                    targetDir = transform.forward;

                _convergeEndAngle = GetRingAngleForDirection(targetDir);

                // Initial separation = modelWidth * 1/3, converted to angle on ring
                float arcLength = _modelWidth * initialSeparationFraction;
                float halfAngle = arcLength / Mathf.Max(_ringRadius, 0.01f);
                _convergeStartAngleA = _convergeEndAngle + halfAngle;
                _convergeStartAngleB = _convergeEndAngle - halfAngle;

                // Show dots at initial positions on the ring
                _dotA.SetActive(true);
                _dotB.SetActive(true);
                _dotA.transform.position = GetRingWorldPosition(_convergeStartAngleA);
                _dotB.transform.position = GetRingWorldPosition(_convergeStartAngleB);

                _convergeTimer = 0f;
                phaserState = PhaserState.Converging;
                _stateTimer = 0f;
            }
        }

        /// <summary>Get the current target: primary locked target, or AI's currentTarget as fallback.</summary>
        private Transform GetTarget()
        {
            Transform primaryTarget = targeting != null ? targeting.GetPrimaryTarget() : null;
            if (primaryTarget == null)
            {
                var ai = GetComponentInParent<ShipAI>();
                if (ai != null) primaryTarget = ai.currentTarget;
            }
            return primaryTarget;
        }

        public void StopFire()
        {
            if (phaserState == PhaserState.Firing || phaserState == PhaserState.Converging)
            {
                phaserState = PhaserState.Recharging;
                _stateTimer = 0f;
            }

            if (_sfx != null) _sfx.StopPhaserFire();
            if (_beam != null) _beam.enabled = false;
            _dotA.SetActive(false);
            _dotB.SetActive(false);
            ClearHitExplosion();
        }

        void Update()
        {
            _stateTimer += Time.deltaTime;

            switch (phaserState)
            {
                case PhaserState.Converging:
                    UpdateConverging();
                    break;
                case PhaserState.Firing:
                    UpdateFiring();
                    break;
                case PhaserState.Recharging:
                    UpdateRecharging();
                    break;
                case PhaserState.Ready:
                    if (_beam != null) _beam.enabled = false;
                    ClearHitExplosion();
                    break;
            }
        }

        private void UpdateConverging()
        {
            _convergeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_convergeTimer / convergeDuration);

            float angleA = Mathf.LerpAngle(_convergeStartAngleA * Mathf.Rad2Deg, _convergeEndAngle * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
            float angleB = Mathf.LerpAngle(_convergeStartAngleB * Mathf.Rad2Deg, _convergeEndAngle * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;

            _dotA.transform.position = GetRingWorldPosition(angleA);
            _dotB.transform.position = GetRingWorldPosition(angleB);

            if (t >= 1f)
            {
                phaserState = PhaserState.Firing;
                _stateTimer = 0f;
                _dotA.SetActive(false);
                _dotB.SetActive(false);
                if (_sfx != null) _sfx.PlayPhaserFire();
            }
        }

        private void UpdateFiring()
        {
            if (health != null && !health.SpendEnergy(stats.phaserEnergyCost * Time.deltaTime))
            {
                StopFire();
                return;
            }

            if (_stateTimer >= fireDuration)
            {
                StopFire();
                return;
            }

            FireBeam();
        }

        private void FireBeam()
        {
            Transform primaryTarget = GetTarget();

            // Update convergence point each frame to track enemy
            Vector3 targetDir;
            if (primaryTarget != null)
            {
                Vector3 ringPos = _phaserRing != null ? _phaserRing.position : transform.position;
                targetDir = (TargetingSystem.GetModelCenter(primaryTarget) - ringPos).normalized;
            }
            else
                targetDir = transform.forward;

            float angle = GetRingAngleForDirection(targetDir);
            // Beam fires directly from ring
            Vector3 origin = GetRingWorldPosition(angle);

            Vector3 direction;
            if (primaryTarget != null)
                direction = (TargetingSystem.GetModelCenter(primaryTarget) - origin).normalized;
            else
                direction = transform.forward;

            // Enemy ships fire from center — offset forward to clear own collider
            if (_phaserRing == null)
                origin += direction * 3f;

            float totalDps = stats.phaserDamage * damageMultiplier;

            Vector3 endPoint;

            // RaycastAll + skip self: enemy ships fire from inside own collider bounds
            var allHits = Physics.RaycastAll(origin, direction, stats.phaserRange);
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit validHit = default;
            bool hasValidHit = false;
            foreach (var h in allHits)
            {
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform))
                    continue;
                validHit = h;
                hasValidHit = true;
                break;
            }

            if (hasValidHit)
            {
                endPoint = validHit.point;

                var targetHealth = validHit.collider.GetComponent<ShipHealth>();
                if (targetHealth == null)
                    targetHealth = validHit.collider.GetComponentInParent<ShipHealth>();

                if (targetHealth != null && !targetHealth.gameObject.CompareTag(gameObject.tag))
                {
                    targetHealth.TakeDamage(totalDps * Time.deltaTime, DamageType.Energy);
                    var shieldVis = targetHealth.GetComponent<ShieldVisualizer>();
                    if (shieldVis != null) shieldVis.RegisterHit(endPoint);
                }

                SpawnHitEffect(endPoint);
            }
            else
            {
                endPoint = origin + direction * stats.phaserRange;
            }

            _beam.enabled = true;
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, endPoint);
        }

        private void UpdateRecharging()
        {
            float fireRatio = _originalFireDuration > 0f ? fireDuration / _originalFireDuration : 1f;
            float effectiveRecharge = rechargeTime * fireRatio;
            if (health != null)
                effectiveRecharge *= health.GetWeaponRechargeMultiplier();

            if (_stateTimer >= effectiveRecharge)
            {
                phaserState = PhaserState.Ready;
                _stateTimer = 0f;
            }

            if (_beam != null) _beam.enabled = false;
        }

        private static Texture2D _sparkTex;
        private static Texture2D CreateSparkTexture()
        {
            if (_sparkTex != null) return _sparkTex;
            int sz = 32;
            _sparkTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _sparkTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    px[y * sz + x] = new Color(1f, 0.95f, 0.8f, a);
                }
            _sparkTex.SetPixels(px);
            _sparkTex.Apply();
            return _sparkTex;
        }

        private void SpawnHitEffect(Vector3 position)
        {
            if (_hitExplosionObj == null)
            {
                _hitExplosionObj = new GameObject("PhaserHitExplosion");
                _hitExplosionObj.transform.position = position;

                _hitExplosionPS = _hitExplosionObj.AddComponent<ParticleSystem>();
                var main = _hitExplosionPS.main;
                main.playOnAwake = false;
                main.startLifetime = 0.4f;
                main.startSpeed = 8f;
                main.startSize = 2f;
                main.startColor = new Color(1f, 0.6f, 0.15f, 0.8f);
                main.maxParticles = 50;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.loop = true;

                var emission = _hitExplosionPS.emission;
                emission.rateOverTime = 60f;

                var shape = _hitExplosionPS.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 1f;

                var col = _hitExplosionPS.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.7f, 0.2f, 0.8f),
                    new Color(0.5f, 0.05f, 0f, 0f)
                );

                var sol = _hitExplosionPS.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.5f, 1f),
                    new Keyframe(1f, 0.1f)
                ));

                var psr = _hitExplosionPS.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    mat.SetTexture("_MainTex", CreateSparkTexture());
                    mat.color = new Color(1f, 0.6f, 0.15f, 0.8f);
                    psr.material = mat;
                    psr.sortingFudge = -1;
                }

                _hitExplosionPS.Play();
            }

            if (_hitExplosionObj != null)
                _hitExplosionObj.transform.position = position;
        }

        private void ClearHitExplosion()
        {
            if (_hitExplosionObj != null)
            {
                Destroy(_hitExplosionObj);
                _hitExplosionObj = null;
                _hitExplosionPS = null;
                _hitExplosionLight = null;
            }
        }

        public bool CanFire() => phaserState == PhaserState.Ready;
        public float FireProgress => phaserState == PhaserState.Firing ? _stateTimer / fireDuration : 0f;

        public float RechargeProgress
        {
            get
            {
                if (phaserState != PhaserState.Recharging) return 1f;
                float fireRatio = _originalFireDuration > 0f ? fireDuration / _originalFireDuration : 1f;
                float effectiveRecharge = rechargeTime * fireRatio;
                if (health != null)
                    effectiveRecharge *= health.GetWeaponRechargeMultiplier();
                return Mathf.Clamp01(_stateTimer / effectiveRecharge);
            }
        }

        public bool isRecharging => phaserState == PhaserState.Recharging;
        public bool isOverheated => false;
        public float currentHeat => 0f;
        public float HeatPercent => 0f;
    }
}
