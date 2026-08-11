using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Shield film — tight-fitting transparent bubble around the ship.
    /// Hidden by default, flashes light blue for ~0.3s when hit, then fades back.
    /// Also provides shield-on/off status for HUD.
    /// </summary>
    [RequireComponent(typeof(ShipHealth))]
    public class ShieldVisualizer : MonoBehaviour
    {
        [Header("Shield Film")]
        [Tooltip("Multiplier on ship bounds to determine shield size")]
        public float shieldSizeMultiplier = 2.2f;
        public Vector3 shieldCenter = Vector3.zero;
        public Color hitColor = new Color(0.3f, 0.6f, 1f, 0.5f);

        [Header("Hit Flash")]
        public float hitFlashDuration = 0.5f;
        private bool _wasBeingHit;  // was the shield being hit last frame?

        [Header("Break Effect")]
        public float breakDuration = 1.5f;

        private ShipHealth _health;
        private GameObject _shieldMesh;
        private Material _shieldMat;
        private float _hitFlashTimer;
        private bool _beingHitThisFrame;
        private bool _wasHitLastFrame;

        void Awake()
        {
            _health = GetComponent<ShipHealth>();
        }

        void Start()
        {
            CreateShieldMesh();

            if (_health != null)
            {
                _health.OnDamaged += OnDamaged;
                _health.OnShieldBroken += OnShieldBroken;
            }
        }

        private void CreateShieldMesh()
        {
            // Calculate tight-fit scale and center from ship's renderer bounds
            var (shieldScale, shieldPos) = CalculateShieldBounds();

            _shieldMesh = new GameObject("ShieldFilm");
            _shieldMesh.transform.SetParent(transform, false);
            _shieldMesh.transform.localPosition = Vector3.zero;
            _shieldMesh.transform.localRotation = Quaternion.identity;
            _shieldMesh.transform.localScale = shieldScale;

            var mf = _shieldMesh.AddComponent<MeshFilter>();
            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempSphere);

            var mr = _shieldMesh.AddComponent<MeshRenderer>();
            // Use Sprites/Default — always included in WebGL builds, supports transparency
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            _shieldMat = new Material(shader);
            // Plain transparent film
            _shieldMat.SetColor("_Color", new Color(0.3f, 0.6f, 1f, 0f));
            _shieldMat.renderQueue = 3000;
            mr.material = _shieldMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Hidden by default
            _shieldMesh.SetActive(false);
        }

        private static Texture2D _gridTex;
        private static Texture2D CreateGridTexture()
        {
            if (_gridTex != null) return _gridTex;
            int sz = 256;
            _gridTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _gridTex.wrapMode = TextureWrapMode.Repeat;
            _gridTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            int lineWidth = 1;
            int cellSize = 8; // dense grid: small cells
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    bool onLine = (x % cellSize < lineWidth) || (y % cellSize < lineWidth);
                    px[y * sz + x] = onLine
                        ? new Color(0.4f, 0.7f, 1f, 0.8f)   // thin grid line
                        : new Color(0, 0, 0, 0);             // transparent between
                }
            _gridTex.SetPixels(px);
            _gridTex.Apply();
            return _gridTex;
        }

        /// <summary>Calculate non-uniform shield scale and center for ellipsoid that wraps ship bounds.</summary>
        private (Vector3 scale, Vector3 center) CalculateShieldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return (Vector3.one * 8f, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Convert world bounds center to local space
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);

            // Convert size to local space
            Vector3 localSize = new Vector3(
                bounds.size.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
                bounds.size.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
                bounds.size.z / Mathf.Max(transform.lossyScale.z, 0.0001f)
            );

            // Non-uniform scale: ellipsoid matching ship shape, enlarged to fully wrap
            // Y axis (height) gets extra multiplier for taller shield
            Vector3 scale = new Vector3(
                Mathf.Clamp(localSize.x * shieldSizeMultiplier, 0.1f, 50f),
                Mathf.Clamp(localSize.y * shieldSizeMultiplier * 1.8f, 0.1f, 50f),
                Mathf.Clamp(localSize.z * shieldSizeMultiplier, 0.1f, 50f)
            );

            return (scale, localCenter);
        }

        void Update()
        {
            if (_shieldMesh == null) return;

            // Don't show shield during galaxy warp
            var controller = GetComponent<ShipController>();
            if (controller != null && controller.IsGalaxyWarping)
            {
                _hitFlashTimer = 0f;
                _shieldMesh.SetActive(false);
                return;
            }

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_hitFlashTimer / hitFlashDuration);

                // Show shield film, fade out — more transparent
                _shieldMesh.SetActive(true);
                Color c = new Color(0.3f, 0.6f, 1f, 0.25f);
                c.a = 0.25f * t;
                _shieldMat.SetColor("_Color", c);
            }
            else
            {
                // Hidden when not being hit
                _shieldMesh.SetActive(false);
            }
        }

        void LateUpdate()
        {
            // Carry forward hit state: if hit this frame, next frame sees it as "was hit"
            // If not hit this frame, next frame sees "was not hit" → new beam contact will flash
            _wasHitLastFrame = _beingHitThisFrame;
            _beingHitThisFrame = false;
        }

        private void OnDamaged(float amount, DamageType damageType)
        {
            // Flash shield whenever ship is damaged and shield is on (even if shield just broke)
            if (_health == null || !_health.isShieldOn) return;
            _beingHitThisFrame = true;
            // Flash only on the transition from "not hit" to "hit" — start of beam contact
            if (!_wasHitLastFrame)
                _hitFlashTimer = hitFlashDuration;
        }

        /// <summary>Called by weapons to register a hit position for flash effect.</summary>
        public void RegisterHit(Vector3 worldHitPos)
        {
            _beingHitThisFrame = true;
            if (!_wasHitLastFrame)
                _hitFlashTimer = hitFlashDuration;
        }

        private void OnShieldBroken()
        {
            CreateShieldBreakEffect();
        }

        private void CreateShieldBreakEffect()
        {
            Vector3 center = transform.position;
            Explosion3D.Spawn(center, 0.4f);

            // Play red alert once when shield breaks
            var sfx = GetComponent<ShipSFX>();
            if (sfx != null) sfx.PlayRedAlert();
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDamaged -= OnDamaged;
                _health.OnShieldBroken -= OnShieldBroken;
            }
        }
    }
}
