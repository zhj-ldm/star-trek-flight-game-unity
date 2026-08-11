using UnityEngine;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Orbiter 2016-style inertia physics controller — NO Rigidbody.
    /// 
    /// Core principles:
    /// - Space vacuum: NO drag, NO angular drag. Velocity persists forever.
    /// - Rotation and translation are fully independent.
    /// - RCS ROT mode: W/S=pitch, A/D=roll, Q/E=yaw — angular acceleration with inertia.
    /// - R = auto-stabilize toggle (damp angular velocity, still allows manual rotation).
    /// - F = full stop toggle (zero all velocity + angular velocity, ship frozen relative to world).
    /// - Main engine: P=forward, L=reverse (clamped at 0 — cannot cross from + to - or vice versa).
    /// - No PhysX — all movement via Transform. No jitter possible.
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        [Header("References")]
        public ShipStats stats;
        public ShipHealth health;
        public Transform cameraTransform;

        [Header("Runtime State (read-only)")]
        public FlightMode flightMode = FlightMode.Normal;
        public float currentSpeed;
        public Vector3 currentVelocity;

        // Manual physics state — replaces Rigidbody
        [HideInInspector] public Vector3 velocity;
        [HideInInspector] public Vector3 angularVelocity; // rad/s

        // RCS mode
        public RCSMode rcsMode = RCSMode.ROT;
        [Tooltip("Translation speed (m/s) in LIN mode — hold WASD/P/L to move, release to stop.")]
        public float linTranslateSpeed = 4f;
        [Tooltip("Seconds to come to rest after releasing translate keys in LIN mode. 0 = stop instantly.")]
        public float linStopTime = 0f;
        [Tooltip("Auto-stabilize active — damps angular velocity to zero.")]
        public bool autoStabilize;

        [Tooltip("Full stop active — zeros all velocity and angular velocity, ship frozen relative to world.")]
        public bool fullStop;

        // Flight control mode (Simple / Realistic) — loaded from PlayerPrefs in Start
        [HideInInspector]
        public ControlMode controlMode = ControlMode.Simple;

        // Realistic mode: attitude lock toggle (R key)
        [HideInInspector]
        public bool attitudeLock;
        private bool _rotInputHeld;       // any WASDQE held last frame
        private float _attitudeLockStabTime;  // calculated stabilize time (capped 5s)
        private float _attitudeLockTimer;

        // Engine power: -1 = full reverse, 0 = off, +1 = full forward
        [Range(-1f, 1f)]
        public float enginePower;
        public float enginePowerRate = 0.3f;

        // Throttle clamp tracking — prevents crossing 0 while holding key
        private bool _forwardHeld;
        private bool _forwardLockedAtZero;  // P started while negative → can only reach 0
        private bool _reverseHeld;
        private bool _reverseLockedAtZero;  // L started while positive → can only reach 0

        // Realistic mode engine state — tracks P/L release for instant zero
        private bool _pRealisticHeld;
        private bool _lRealisticHeld;

        // One-shot deceleration state (R = angular 2s, F = linear+angular 5s)
        private const float StabilizeDuration = 2f;
        private const float FullStopDuration = 5f;
        private float _stabilizeTimer;
        private Vector3 _stabilizeStartAngVel;
        private float _fullStopTimer;
        private Vector3 _fullStopStartVel;
        private Vector3 _fullStopStartAngVel;

        // Legacy throttle compat
        public float throttlePower => enginePower;
        public enum ThrottleState { Forward, StoppedForward, Reverse, StoppedReverse }
        public ThrottleState throttleState
        {
            get
            {
                if (enginePower > 0.001f) return ThrottleState.Forward;
                if (enginePower < -0.001f) return ThrottleState.Reverse;
                return ThrottleState.StoppedForward;
            }
        }

        // Internal state
        private ShipInput.InputData _input;

        // Ion / damage
        private IonEffect _ionEffect;
        private ModularDamageSystem _modularDamage;
        private ShipSFX _sfx;
        private TargetingSystem _targeting;

        // Player vs AI control
        [HideInInspector]
        [SerializeField]
        private bool _isPlayerControlled = true;
        public bool isPlayerControlled => _isPlayerControlled;

        // Warp streaks VFX — simple stretched cubes with 3-phase animation
        private GameObject _warpStreaksObj;
        private List<Transform> _streakTransforms;
        private List<float> _streakBaseZScale;
        private List<float> _streakBaseSpeed;
        private float _warpStreakSpeed;     // current speed multiplier 0→1
        private float _warpStreakLength;    // current length multiplier 0→1
        private bool _warpStreaksInit;

        // Auto-orbit
        [HideInInspector]
        public bool autoOrbit;
        private AutoOrbitController _autoOrbitController;

        // Frozen
        private bool _frozen;
        public bool IsFrozen => _frozen;

        // Auto warp navigation target — checked BEFORE position integration
        private Transform _autoWarpTarget;
        public Transform AutoWarpTarget => _autoWarpTarget;
        public void SetAutoWarpTarget(Transform t) { _autoWarpTarget = t; _prevAutoWarpDist = float.MaxValue; }
        public void ClearAutoWarpTarget() { _autoWarpTarget = null; }
        private float _autoWarpLogTimer;
        private float _prevAutoWarpDist = float.MaxValue;

        // Galaxy warp
        private bool _isGalaxyWarping;
        private float _galaxyWarpTimer;
        private const float GalaxyWarpBaseSpeed = 2000f;
        private const int GalaxyWarpMaxLevel = 9;
        [HideInInspector]
        public int _galaxyWarpLevel = 1;
        /// <summary>Current galaxy warp speed (m/s). Level 1=2000, each level doubles.</summary>
        public float GalaxyWarpSpeed => GalaxyWarpBaseSpeed * Mathf.Pow(2f, _galaxyWarpLevel - 1);
        public int GalaxyWarpLevel => _galaxyWarpLevel;
        public void SetGalaxyWarpLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, GalaxyWarpMaxLevel);
            if (clamped != _galaxyWarpLevel)
            {
                _galaxyWarpLevel = clamped;
                Debug.Log($"[GalaxyWarp] Level set to {_galaxyWarpLevel}, speed={GalaxyWarpSpeed} m/s");
            }
        }

        // Warp zoom (3s FOV transition in/out)
        private bool _warpZooming;
        private float _warpZoomTimer;
        private bool _warpExitZooming;
        private float _warpExitZoomTimer;
        private const float WarpZoomDuration = 3f;

        // Engine sound tracking
        private bool _engineWasOn;

        // RCS thruster sustain sound — active while any attitude key (WASDQE) is held,
        // or during the 2s auto-stabilize window after R. Single looped source, no stacking.
        private float _rcsRWindow;

        // Regular warp
        private bool _isWarping;
        private bool _warpCharging;
        private float _warpChargeTimer;
        private float _pulseCooldownTimer;

        public void SetPlayerControlled(bool value)
        {
            _isPlayerControlled = value;
            if (!value) enginePower = 0f;
        }

        public void SetFrozen(bool value) { _frozen = value; }

        // Properties
        public bool IsWarping => _isWarping || _warpCharging;
        public bool IsGalaxyWarping => _isGalaxyWarping;
        public bool IsWarpZooming => _warpZooming;
        public float WarpZoomProgress => Mathf.Clamp01(_warpZoomTimer / WarpZoomDuration);
        public bool IsWarpExitZooming => _warpExitZooming;
        public float WarpExitZoomProgress => Mathf.Clamp01(_warpExitZoomTimer / WarpZoomDuration);
        public bool IsAttitudeLocked => controlMode == ControlMode.Realistic ? attitudeLock : autoStabilize;
        public bool IsFullStop => fullStop;
        public float PulseCooldownRemaining => Mathf.Max(0f, _pulseCooldownTimer);
        public float WarpChargeProgress => _warpCharging ? _warpChargeTimer / stats.warpChargeTime : (_isWarping ? 1f : 0f);
        public bool IsEngineDisabled => (_ionEffect != null && _ionEffect.IsEngineDisabled);
        public bool IsWeaponDisabled => (_ionEffect != null && _ionEffect.IsWeaponDisabled);
        public float SpeedModifier
        {
            get
            {
                float mod = 1f;
                if (_ionEffect != null) mod *= _ionEffect.GetSpeedModifier();
                if (_modularDamage != null) mod *= _modularDamage.GetEngineSpeedModifier();
                return mod;
            }
        }

        // Legacy compat
        public bool IsReversing => enginePower < -0.001f;
        public float ThrottleDisplay => Mathf.Abs(enginePower);
        public float GetThrustInput() => enginePower;
        public bool IsBoosting() => false;

        void Awake()
        {
            if (health == null) health = GetComponent<ShipHealth>();
            _ionEffect = GetComponent<IonEffect>();
            _modularDamage = GetComponent<ModularDamageSystem>();
            _targeting = GetComponent<TargetingSystem>();
            _sfx = GetComponent<ShipSFX>();

            if (CompareTag("Enemy"))
                _isPlayerControlled = false;

            _autoOrbitController = GetComponent<AutoOrbitController>();
            InitWarpStreaks();
        }

        void Start()
        {
            if (stats == null)
            {
                Debug.LogError($"[{name}] ShipStats not assigned!");
                return;
            }
            enginePowerRate = stats.enginePowerRate;

            // Load flight control mode from PlayerPrefs
            if (isPlayerControlled)
            {
                controlMode = (ControlMode)PlayerPrefs.GetInt("FlightControlMode", 0);
                Debug.Log($"[ShipController] ControlMode = {controlMode}");
            }

            // Ensure a kinematic Rigidbody exists — needed for collider trigger detection.
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            // Auto-fit collider to match visible ship model bounds
            FitColliderToModel();

            // Disable all emission on ship materials to remove internal red glow
            DisableEmission();

            // Enable emission on specific child renderers (objects 17, 18, 4)
            EnableEmissionOnObjects();

            // Use Skybox ambient mode so the panoramic starfield provides environment
            // reflections (IBL) for Standard PBR shader. In the editor Unity auto-generates
            // these from the skybox, but builds with Flat ambient get NO reflections,
            // causing glossy/metallic surfaces to appear dark.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.5f;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.reflectionBounces = 1;

            // Enable dithering to reduce color banding on 8-bit render targets
            // (Camera with high far/near ratio produces banded gradients on Standard shader)

            // Turn on SunLight (scene directional light), disable shadows
            var sunLight = GameObject.Find("SunLight");
            if (sunLight != null)
            {
                var sl = sunLight.GetComponent<Light>();
                if (sl != null)
                {
                    sl.intensity = 0.6f;
                    sl.shadows = LightShadows.None;
                }
            }

            // Disable shadows globally via QualitySettings
            QualitySettings.shadows = ShadowQuality.Disable;

            // Bajor_Sun point light — also disable shadows
            var sunObj = GameObject.Find("Bajor_Sun");
            if (sunObj != null)
            {
                var sunPL = sunObj.GetComponent<Light>();
                if (sunPL != null) sunPL.shadows = LightShadows.None;
            }
        }

        private void DisableEmission()
        {
            var shipModel = transform.Find("ShipModel");
            if (shipModel == null) return;

            foreach (var rend in shipModel.GetComponentsInChildren<Renderer>())
            {
                if (rend == null || rend.materials == null) continue;
                var mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    // Clamp metallic/glossiness — GLB-imported materials may have high
                    // metallic values that make surfaces pitch-black without env reflections
                    if (mats[i].HasProperty("_Metallic"))
                        mats[i].SetFloat("_Metallic", 0f);
                    if (mats[i].HasProperty("_Glossiness"))
                        mats[i].SetFloat("_Glossiness", Mathf.Min(mats[i].GetFloat("_Glossiness"), 0.3f));
                    mats[i].DisableKeyword("_EMISSION");
                    mats[i].SetColor("_EmissionColor", Color.black);
                    mats[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                rend.materials = mats;
            }
        }

        private void EnableEmissionOnObjects()
        {
            var shipModel = transform.Find("ShipModel");
            if (shipModel == null) return;

            string[] targetNames = { "4", "17", "18", "47", "48", "66", "67", "68", "69" };

            // Recursive search through all descendants
            foreach (var child in shipModel.GetComponentsInChildren<Transform>(true))
            {
                if (child == shipModel) continue;
                if (child.name != "4" && child.name != "17" && child.name != "18" &&
                    child.name != "47" && child.name != "48" && child.name != "66" &&
                    child.name != "67" && child.name != "68" && child.name != "69") continue;

                var rend = child.GetComponent<Renderer>();
                if (rend == null)
                {
                    // Try MeshRenderer specifically
                    rend = child.GetComponent<MeshRenderer>();
                }
                if (rend == null)
                {
                    // Try child renderers
                    rend = child.GetComponentInChildren<Renderer>();
                }
                if (rend == null)
                {
                    Debug.Log($"[Emission] '{child.name}' — NO Renderer found");
                    continue;
                }

                var mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var tex = mats[i].HasProperty("_MainTex") ? mats[i].GetTexture("_MainTex") : null;
                    mats[i].EnableKeyword("_EMISSION");
                    // EmissionColor = white so _EmissionMap (texture) shows at full color
                    mats[i].SetColor("_EmissionColor", Color.white);
                    if (tex != null) mats[i].SetTexture("_EmissionMap", tex);
                    mats[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                rend.materials = mats;
                Debug.Log($"[Emission] Enabled on '{child.name}' path={GetPath(child, shipModel)} matCount={mats.Length}");
            }
        }

        private string GetPath(Transform t, Transform root)
        {
            string path = t.name;
            var p = t.parent;
            while (p != null && p != root)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }

        /// <summary>Adjust the ship's BoxCollider to match the ShipModel's renderer bounds.</summary>
        private void FitColliderToModel()
        {
            var shipModel = transform.Find("ShipModel");
            if (shipModel == null) return;

            var renderers = shipModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();

            // Convert world-space bounds center to local space
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = bounds.size;
            // Account for parent scale
            localSize.x /= Mathf.Max(0.0001f, transform.lossyScale.x);
            localSize.y /= Mathf.Max(0.0001f, transform.lossyScale.y);
            localSize.z /= Mathf.Max(0.0001f, transform.lossyScale.z);

            col.center = localCenter;
            col.size = localSize;
        }

        void Update()
        {
            if (stats == null) return;

            float dt = Time.deltaTime;

            if (isPlayerControlled)
                _input = ShipInput.ReadInput();
            else
                _input = new ShipInput.InputData();

            // Toggle ROT (roll) / LIN (translate) mode with C.
            if (_input.toggleTranslateMode && isPlayerControlled)
            {
                bool nowLin = (rcsMode == RCSMode.ROT);
                rcsMode = nowLin ? RCSMode.LIN : RCSMode.ROT;
                angularVelocity = Vector3.zero;
                attitudeLock = false;
                // Do NOT zero velocity — preserve current speed for RCS translation on top
                Debug.Log($"[Control] Mode -> {(rcsMode == RCSMode.ROT ? "滚转 ROT" : "平移 LIN")}");
            }

            // Auto-orbit — skip normal control, AutoOrbitController handles everything
            if (autoOrbit && _autoOrbitController != null)
            {
                bool hasInput = Mathf.Abs(_input.pitch) > 0.01f || Mathf.Abs(_input.yaw) > 0.01f ||
                               Mathf.Abs(_input.roll) > 0.01f || _input.engineForward || _input.engineReverse ||
                               _input.warpToDestination;
                if (hasInput)
                {
                    autoOrbit = false;
                    _autoOrbitController.CancelOrbit();
                    // Fall through to normal control
                }
                else
                {
                    _autoOrbitController.UpdateOrbit(dt);
                    currentVelocity = velocity;
                    currentSpeed = velocity.magnitude;
                    ApplyFloatingOrigin();
                    UpdateWarpStreaks(dt, 3);
                    return;
                }
            }

            // Auto-stabilize / Attitude lock — R key
            if (_input.autoStabilize && isPlayerControlled)
            {
                if (controlMode == ControlMode.Realistic)
                {
                    // Realistic: R toggles attitude lock on/off
                    attitudeLock = !attitudeLock;
                    if (attitudeLock)
                    {
                        autoStabilize = false;
                        fullStop = false;
                    }
                    Debug.Log($"[Control] Attitude Lock = {attitudeLock}");
                }
                else
                {
                    // Simple: R = one-shot auto-stabilize (2s angular damp)
                    autoStabilize = true;
                    fullStop = false;
                    _stabilizeTimer = 0f;
                    _stabilizeStartAngVel = angularVelocity;
                }
            }

            // Full stop — one-shot (F): gradual linear+angular deceleration over 5s, cancels on input
            if (_input.fullStop)
            {
                fullStop = true;
                autoStabilize = false;
                attitudeLock = false;
                _fullStopTimer = 0f;
                _fullStopStartVel = velocity;
                _fullStopStartAngVel = angularVelocity;
                enginePower = 0f;
            }

            // O — instant stop, only usable when within 200m of a space station.
            if (_input.stationStop && isPlayerControlled)
            {
                if (IsNearSpaceStation(200f))
                {
                    velocity = Vector3.zero;
                    angularVelocity = Vector3.zero;
                    enginePower = 0f;
                    fullStop = false;
                    autoStabilize = false;
                    Debug.Log("[Starbase] O — ship stopped near station");
                }
                else
                {
                    Debug.Log("[Starbase] O ignored — no station within 200m");
                }
            }

            // Galaxy warp (Z)
            if (_input.warpToDestination && isPlayerControlled)
            {
                if (_isGalaxyWarping || _warpZooming)
                {
                    // Exit warp: stop ship first, then zoom out
                    _isGalaxyWarping = false;
                    _warpZooming = false;
                    velocity = Vector3.zero;
                    flightMode = FlightMode.Normal;
                    _warpExitZooming = true;
                    _warpExitZoomTimer = 0f;
                    // Don't hide streaks yet — let them decelerate during zoom-out
                    if (_sfx != null) _sfx.PlayWarpExit();
                }
                else if (!_warpExitZooming)
                {
                    // Enter warp: start zoom-in + play engage sound + streaks
                    _warpZooming = true;
                    _warpZoomTimer = 0f;
                    _isWarping = false;
                    _warpCharging = false;
                    InitWarpStreaks();
                    SetWarpStreaksActive(true);
                    if (_sfx != null) _sfx.PlayWarpEngage();
                }
            }

            // Warp level hotkeys: number keys 1-9
            if (isPlayerControlled)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                    SetGalaxyWarpLevel(1);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                    SetGalaxyWarpLevel(2);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                    SetGalaxyWarpLevel(3);
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                    SetGalaxyWarpLevel(4);
                else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                    SetGalaxyWarpLevel(5);
                else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
                    SetGalaxyWarpLevel(6);
                else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
                    SetGalaxyWarpLevel(7);
                else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
                    SetGalaxyWarpLevel(8);
                else if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
                    SetGalaxyWarpLevel(9);
            }

            if (_input.warpToggle)
                ToggleWarp();

            if (_pulseCooldownTimer > 0f)
                _pulseCooldownTimer -= dt;

            UpdateWarpCharge();
            UpdateEnginePower();

            // === Manual physics integration ===
            if (!_frozen)
            {
                if (fullStop)
                {
                    ApplyFullStop(dt);
                }
                else
                {
                    // Only apply RCS/engine for player-controlled ships.
                    // AI ships have their velocity set directly by ShipAI.
                    if (isPlayerControlled)
                    {
                        ApplyRCSControl(dt);
                        ApplyMainEngine(dt);
                        ApplyAutoStabilize(dt);
                    }
                    ApplyWarpMovement(dt);
                    ClampState();
                }

                // Auto warp stop check — must be BEFORE position integration
                if (_isGalaxyWarping && _autoWarpTarget != null)
                {
                    float autoDist = Vector3.Distance(transform.position, _autoWarpTarget.position);
                    float autoRadius = Mathf.Max(_autoWarpTarget.localScale.x, _autoWarpTarget.localScale.y, _autoWarpTarget.localScale.z) * 0.5f;
                    float surfaceDist = autoDist - autoRadius;

                    // Stop when arrived (close to surface) OR clearly flew past.
                    // Fly-past detection is guarded: only when reasonably close, with a real
                    // prior baseline, and a clear distance increase — float/velocity noise at
                    // high warp must NOT trigger an immediate stop on warp engage.
                    bool closeEnough = surfaceDist <= 100f;
                    bool hasBaseline = _prevAutoWarpDist < float.MaxValue;
                    bool withinFlyPastRange = surfaceDist < 5000f;
                    bool flewPast = withinFlyPastRange && hasBaseline &&
                                    surfaceDist > _prevAutoWarpDist + 0.5f;

                    if (closeEnough || flewPast)
                    {
                        Debug.Log($"[AutoWarp] STOP! closeEnough={closeEnough} flewPast={flewPast} dist={surfaceDist:F0}m prev={_prevAutoWarpDist:F0}m");
                        EndGalaxyWarpAuto();
                        _autoWarpTarget = null;
                        _prevAutoWarpDist = float.MaxValue;
                        var nav = GetComponent<AutoWarpNavigator>();
                        if (nav != null) nav.OnArrived();
                    }
                    else
                    {
                        _prevAutoWarpDist = surfaceDist;
                    }
                }
                else if (_autoWarpTarget != null)
                {
                    _autoWarpLogTimer += dt;
                    if (_autoWarpLogTimer >= 1f)
                    {
                        _autoWarpLogTimer = 0f;
                        Debug.Log($"[AutoWarp] Waiting... isGalaxyWarping={_isGalaxyWarping} isWarpZooming={_warpZooming} isExit={_warpExitZooming} target={_autoWarpTarget?.name}");
                    }
                }

                // Integrate position
                transform.position += velocity * dt;

                // Integrate rotation from angular velocity
                if (angularVelocity.sqrMagnitude > 0.000001f)
                {
                    Vector3 axis = angularVelocity.normalized;
                    float angle = angularVelocity.magnitude * Mathf.Rad2Deg * dt;
                    transform.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
                }
            }
            else
            {
                velocity = Vector3.Lerp(velocity, Vector3.zero, 5f * dt);
                angularVelocity = Vector3.Lerp(angularVelocity, Vector3.zero, 5f * dt);
            }

            currentVelocity = velocity;
            currentSpeed = velocity.magnitude;

            // RCS thruster sustain sound
            UpdateRcsSound(dt);

            // Floating Origin
            ApplyFloatingOrigin();

            // Warp streaks update — 3 phases synced with FOV zoom
            if (_warpZooming)
                UpdateWarpStreaks(dt, 0);        // zoom-in: accelerate + stretch
            else if (_isGalaxyWarping)
                UpdateWarpStreaks(dt, 1);        // warp: full speed
            else if (_warpExitZooming)
                UpdateWarpStreaks(dt, 2);        // zoom-out: decelerate + shrink
            else
                UpdateWarpStreaks(dt, 3);        // stopped
        }

        #region Warp Streaks

        private void InitWarpStreaks()
        {
            if (_warpStreaksInit) return;
            _warpStreaksInit = true;
            _warpStreakSpeed = 0f;
            _warpStreakLength = 0f;

            _warpStreaksObj = new GameObject("WarpStreaks");
            _warpStreaksObj.transform.SetParent(transform, false);
            _warpStreaksObj.transform.localPosition = Vector3.zero;

            var shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            var mat = new Material(shader);
            mat.color = new Color(0.7f, 0.85f, 1f, 0.8f);

            _streakTransforms = new List<Transform>();
            _streakBaseZScale = new List<float>();
            _streakBaseSpeed = new List<float>();

            for (int i = 0; i < 100; i++)
            {
                var streak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                streak.name = "Streak_" + i;
                streak.transform.SetParent(_warpStreaksObj.transform, false);

                float angle = Random.Range(0f, 360f);
                float radius = Random.Range(6f, 35f);
                float zPos = Random.Range(-30f, 250f);
                streak.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.6f,
                    zPos
                );
                streak.transform.localRotation = Quaternion.identity;

                float baseLen = Random.Range(8f, 20f);
                streak.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); // start tiny

                var mr = streak.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                var col = streak.GetComponent<Collider>();
                if (col != null) Destroy(col);

                _streakTransforms.Add(streak.transform);
                _streakBaseZScale.Add(baseLen);
                _streakBaseSpeed.Add(Random.Range(300f, 800f));
                streak.SetActive(false);
            }
        }

        private void SetWarpStreaksActive(bool active)
        {
            if (_warpStreaksObj == null) return;
            if (active)
            {
                _warpStreakSpeed = 0f;
                _warpStreakLength = 0f;
            }
            foreach (var t in _streakTransforms)
            {
                if (t != null) t.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Update streaks. phase: 0=zoom-in (accelerate+stretch), 1=warp (cruise speed), 2=zoom-out (decelerate+shrink), 3=stopped
        /// </summary>
        private void UpdateWarpStreaks(float dt, int phase)
        {
            if (_streakTransforms == null) return;

            // Cruise speed scales with warp level: level 1 = 0.4, +0.05 per level
            float cruiseSpeed = 0.4f + (_galaxyWarpLevel - 1) * 0.05f;
            const float entrySpeed = 0.1f;

            // Phase transitions
            if (phase == 0) // zoom-in: accelerate 0→entrySpeed over WarpZoomDuration
            {
                _warpStreakSpeed = Mathf.MoveTowards(_warpStreakSpeed, entrySpeed, dt / WarpZoomDuration);
                _warpStreakLength = Mathf.MoveTowards(_warpStreakLength, entrySpeed, dt / WarpZoomDuration);
            }
            else if (phase == 1) // warp: instantly jump to cruise speed
            {
                _warpStreakSpeed = cruiseSpeed;
                _warpStreakLength = cruiseSpeed;
            }
            else if (phase == 2) // zoom-out: smooth decelerate cruiseSpeed→0 over WarpZoomDuration
            {
                float t = Mathf.Clamp01(_warpExitZoomTimer / WarpZoomDuration);
                // Ease-out: starts fast, slows down gradually
                float eased = cruiseSpeed * (1f - Mathf.Pow(t, 2f));
                _warpStreakSpeed = eased;
                // Length shrinks slightly slower than speed
                _warpStreakLength = Mathf.Lerp(_warpStreakLength, 0f, 2f * dt);
            }
            else // stopped
            {
                _warpStreakSpeed = 0f;
                _warpStreakLength = 0f;
            }

            for (int i = 0; i < _streakTransforms.Count; i++)
            {
                if (_streakTransforms[i] == null) continue;
                var t = _streakTransforms[i];

                // Move
                var pos = t.localPosition;
                float speed = _streakBaseSpeed[i] * _warpStreakSpeed;
                pos.z -= speed * dt;

                // Reset to front when past camera
                if (pos.z < -80f)
                {
                    float angle = Random.Range(0f, 360f);
                    float radius = Random.Range(8f, 40f);
                    pos = new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius * 0.5f,
                        Random.Range(150f, 250f)
                    );
                }
                t.localPosition = pos;

                // Stretch length
                var scale = t.localScale;
                scale.z = _streakBaseZScale[i] * _warpStreakLength;
                t.localScale = scale;
            }
        }

        #endregion

        #region RCS Sound

        private void UpdateRcsSound(float dt)
        {
            if (_sfx == null) return;

            bool wasdqeHeld = Mathf.Abs(_input.pitch) > 0.01f ||
                              Mathf.Abs(_input.yaw) > 0.01f ||
                              Mathf.Abs(_input.roll) > 0.01f ||
                              Mathf.Abs(_input.translateUp) > 0.01f ||
                              Mathf.Abs(_input.translateRight) > 0.01f ||
                              Mathf.Abs(_input.translateForward) > 0.01f;

            // R — simple mode: one-shot 2s window; realistic mode: attitude lock stabilizing
            if (_input.autoStabilize)
                _rcsRWindow = 2f;

            // In realistic mode with attitude lock, RCS plays during stabilization
            if (controlMode == ControlMode.Realistic && attitudeLock && !_rotInputHeld &&
                angularVelocity.sqrMagnitude > 0.0001f)
                _rcsRWindow = 0.5f;  // keep playing during damp

            bool wantOn = wasdqeHeld || _rcsRWindow > 0f;
            if (wantOn)
                _sfx.PlayRCS();
            else
                _sfx.StopRCS();

            if (_rcsRWindow > 0f)
                _rcsRWindow -= dt;
        }

        #endregion

        #region Engine Power

        private void UpdateEnginePower()
        {
            if (_isWarping || _isGalaxyWarping || fullStop) return;

            // In LIN (translate) mode P/L drive forward/back translation, so disable engine power.
            if (rcsMode == RCSMode.LIN)
            {
                enginePower = 0f;
                _forwardHeld = false;
                _reverseHeld = false;
                if (_engineWasOn && _sfx != null) _sfx.StopEngine();
                _engineWasOn = false;
                return;
            }

            if (controlMode == ControlMode.Realistic)
            {
                UpdateEnginePowerRealistic();
            }
            else
            {
                UpdateEnginePowerSimple();
            }

            // Engine sound: play when engine on, stop when off
            bool engineOn = Mathf.Abs(enginePower) > 0.01f;
            if (engineOn && !_engineWasOn && _sfx != null)
                _sfx.PlayEngine();
            else if (!engineOn && _engineWasOn && _sfx != null)
                _sfx.StopEngine();
            _engineWasOn = engineOn;
        }

        /// <summary>Simple mode engine power — ramp up/down with throttle clamping.</summary>
        private void UpdateEnginePowerSimple()
        {
            float dt = Time.deltaTime;
            float rate = enginePowerRate * dt;

            // Track fresh press / release for P (forward)
            if (_input.engineForward && !_forwardHeld)
            {
                _forwardHeld = true;
                _forwardLockedAtZero = enginePower < -0.001f;
            }
            else if (!_input.engineForward)
            {
                _forwardHeld = false;
            }

            // Track fresh press / release for L (reverse)
            if (_input.engineReverse && !_reverseHeld)
            {
                _reverseHeld = true;
                _reverseLockedAtZero = enginePower > 0.001f;
            }
            else if (!_input.engineReverse)
            {
                _reverseHeld = false;
            }

            if (_input.engineForward)
            {
                if (_forwardLockedAtZero)
                    enginePower = Mathf.Min(0f, enginePower + rate);
                else
                    enginePower = Mathf.Min(1f, enginePower + rate);
            }

            if (_input.engineReverse)
            {
                if (_reverseLockedAtZero)
                    enginePower = Mathf.Max(0f, enginePower - rate);
                else
                    enginePower = Mathf.Max(-1f, enginePower - rate);
            }

            if (_input.autoBrake)
            {
                float brakeRate = enginePowerRate * 1.5f * dt;
                if (enginePower > 0f)
                    enginePower = Mathf.Max(0f, enginePower - brakeRate);
                else if (enginePower < 0f)
                    enginePower = Mathf.Min(0f, enginePower + brakeRate);
            }
        }

        /// <summary>
        /// Realistic mode engine power:
        /// P (no Alt): instant full forward, release = instant zero
        /// L (no Alt): instant full reverse, release = instant zero
        /// Alt+P: smooth ramp up toward max, release = maintain
        /// Alt+L: smooth ramp down toward 0, release = maintain
        /// [ key: instant zero thrust
        /// </summary>
        private void UpdateEnginePowerRealistic()
        {
            float dt = Time.deltaTime;
            float rate = enginePowerRate * dt;

            // [ key — instant zero
            if (_input.instantZeroThrust)
            {
                enginePower = 0f;
                return;
            }

            // P held (no Alt) — instant full forward
            if (_input.engineForward)
            {
                enginePower = 1f;
                _pRealisticHeld = true;
                return;
            }

            // L held (no Alt) — instant full reverse
            if (_input.engineReverse)
            {
                enginePower = -1f;
                _lRealisticHeld = true;
                return;
            }

            // P just released — instant zero
            if (_pRealisticHeld)
            {
                enginePower = 0f;
                _pRealisticHeld = false;
            }

            // L just released — instant zero
            if (_lRealisticHeld)
            {
                enginePower = 0f;
                _lRealisticHeld = false;
            }

            // Alt+P held — smooth ramp up toward +1
            if (_input.cmdEngineForward)
            {
                enginePower = Mathf.Min(1f, enginePower + rate);
            }

            // Alt+L held — smooth ramp down toward -1 (through 0 into reverse)
            if (_input.cmdEngineReverse)
            {
                enginePower = Mathf.Max(-1f, enginePower - rate);
            }
        }

        #endregion

        #region RCS Control

        private void ApplyRCSControl(float dt)
        {
            if (IsEngineDisabled) return;

            if (rcsMode == RCSMode.ROT)
            {
                // In realistic mode with attitude lock on, rotation is handled by ApplyAttitudeLock
                // (which calls ApplyRotationRCS when keys are held, and auto-stabilizes when released)
                if (controlMode == ControlMode.Realistic && attitudeLock)
                    return;
                ApplyRotationRCS(dt);
            }
            else
                ApplyTranslationRCS(dt);
        }

        private void ApplyRotationRCS(float dt)
        {
            float pitchTorque = stats.rcsRotPitch;
            float yawTorque = stats.rcsRotYaw;
            float rollTorque = stats.rcsRotRoll;

            if (_isWarping || _isGalaxyWarping)
            {
                pitchTorque *= 0.2f;
                yawTorque *= 0.2f;
                rollTorque *= 0.2f;
            }

            Vector3 localTorque = new Vector3(
                -_input.pitch * pitchTorque,
                _input.yaw * yawTorque,
                -_input.roll * rollTorque
            ) * Mathf.Deg2Rad;

            Vector3 worldTorque = transform.TransformDirection(localTorque);
            angularVelocity += worldTorque * dt;
        }

        private void ApplyTranslationRCS(float dt)
        {
            if (_isWarping || _isGalaxyWarping) return;

            // Local-space thrust: right/up/forward from WASD + P/L
            Vector3 localThrust = new Vector3(
                _input.translateRight,
                _input.translateUp,
                _input.translateForward
            );

            if (controlMode == ControlMode.Realistic)
            {
                ApplyTranslationRCSRealistic(dt, localThrust);
            }
            else
            {
                ApplyTranslationRCSSimple(dt, localThrust);
            }
        }

        /// <summary>Simple mode: direct velocity assignment, instant stop on release.</summary>
        private void ApplyTranslationRCSSimple(float dt, Vector3 localThrust)
        {
            float rcsSpeed = linTranslateSpeed;

            if (localThrust.sqrMagnitude > 0.001f)
            {
                localThrust.Normalize();
                Vector3 worldDir = transform.TransformDirection(localThrust);
                velocity = worldDir * rcsSpeed;
            }
            else
            {
                if (linStopTime <= 0.001f)
                    velocity = Vector3.zero;
                else
                {
                    float stopRate = 1f / linStopTime;
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-dt * stopRate));
                }
            }
        }

        /// <summary>
        /// Realistic mode: acceleration-based RCS thrust, no auto velocity cancel.
        /// Release = thrust stops, velocity persists (vacuum inertia).
        /// No attitude drift — translation does not affect orientation.
        /// </summary>
        private void ApplyTranslationRCSRealistic(float dt, Vector3 localThrust)
        {
            float rcsAccel = stats.rcsLinAccel;

            if (localThrust.sqrMagnitude > 0.001f)
            {
                localThrust.Normalize();
                Vector3 worldDir = transform.TransformDirection(localThrust);
                velocity += worldDir * rcsAccel * dt;
            }
            // No else — velocity persists when keys released (vacuum inertia)
            // No residual torque — attitude stays locked during translation
        }

        private void ApplyAutoStabilize(float dt)
        {
            if (controlMode == ControlMode.Realistic)
            {
                ApplyAttitudeLock(dt);
                return;
            }

            // Simple mode: one-shot angular damp over StabilizeDuration
            if (!autoStabilize) return;

            bool hasRotationInput = Mathf.Abs(_input.pitch) > 0.01f ||
                                    Mathf.Abs(_input.yaw) > 0.01f ||
                                    Mathf.Abs(_input.roll) > 0.01f;
            if (hasRotationInput)
            {
                autoStabilize = false;
                return;
            }

            _stabilizeTimer += dt;
            float t = Mathf.Clamp01(_stabilizeTimer / StabilizeDuration);
            angularVelocity = Vector3.Lerp(_stabilizeStartAngVel, Vector3.zero, t);

            if (t >= 1f)
            {
                angularVelocity = Vector3.zero;
                autoStabilize = false;
            }
        }

        /// <summary>
        /// Realistic attitude lock: when enabled and no rotation input held,
        /// auto-outputs reverse RCS torque to zero angular velocity.
        /// Stabilize time = realistic (angVel / rcsRate), capped at 5s.
        /// While rotation keys are held, normal rotation control applies.
        /// </summary>
        private void ApplyAttitudeLock(float dt)
        {
            if (!attitudeLock) return;

            bool hasRotationInput = Mathf.Abs(_input.pitch) > 0.01f ||
                                    Mathf.Abs(_input.yaw) > 0.01f ||
                                    Mathf.Abs(_input.roll) > 0.01f;

            if (hasRotationInput)
            {
                // Rotation keys held — apply normal rotation, reset stabilize timer
                _rotInputHeld = true;
                _attitudeLockTimer = 0f;
                ApplyRotationRCS(dt);
                return;
            }

            // Rotation keys just released — start stabilizing
            if (_rotInputHeld)
            {
                _rotInputHeld = false;
                _attitudeLockTimer = 0f;

                // Calculate realistic stabilize time, capped at 5s
                float maxRcsRate = Mathf.Max(stats.rcsRotPitch, stats.rcsRotYaw, stats.rcsRotRoll) * Mathf.Deg2Rad;
                if (maxRcsRate > 0f && angularVelocity.magnitude > 0.001f)
                {
                    float realTime = angularVelocity.magnitude / maxRcsRate;
                    _attitudeLockStabTime = Mathf.Min(realTime, 5f);
                }
                else
                {
                    _attitudeLockStabTime = 0f;
                }
                _stabilizeStartAngVel = angularVelocity;
            }

            // No rotation input — damp angular velocity to zero
            if (angularVelocity.sqrMagnitude < 0.000001f)
            {
                angularVelocity = Vector3.zero;
                return;
            }

            if (_attitudeLockStabTime > 0.001f)
            {
                _attitudeLockTimer += dt;
                float t = Mathf.Clamp01(_attitudeLockTimer / _attitudeLockStabTime);
                angularVelocity = Vector3.Lerp(_stabilizeStartAngVel, Vector3.zero, t);
            }
            else
            {
                angularVelocity = Vector3.zero;
            }
        }

        private void ApplyFullStop(float dt)
        {
            // 玩家有旋转或引擎输入 → 立即取消，不再干预
            bool hasRotationInput = Mathf.Abs(_input.pitch) > 0.01f ||
                                    Mathf.Abs(_input.yaw) > 0.01f ||
                                    Mathf.Abs(_input.roll) > 0.01f;
            bool hasEngineInput = _input.engineForward || _input.engineReverse;
            if (hasRotationInput || hasEngineInput)
            {
                fullStop = false;
                return;
            }

            // 5秒线性减速到零（线速度+角速度）
            _fullStopTimer += dt;
            float t = Mathf.Clamp01(_fullStopTimer / FullStopDuration);
            velocity = Vector3.Lerp(_fullStopStartVel, Vector3.zero, t);
            angularVelocity = Vector3.Lerp(_fullStopStartAngVel, Vector3.zero, t);

            if (t >= 1f)
            {
                velocity = Vector3.zero;
                angularVelocity = Vector3.zero;
                fullStop = false;
            }
        }

        /// <summary>True if the player is within range of any space station (StarbaseStation).</summary>
        private bool IsNearSpaceStation(float range)
        {
            var stations = FindObjectsOfType<StarbaseStation>();
            foreach (var s in stations)
            {
                if (s == null) continue;
                float dist = Vector3.Distance(transform.position, s.transform.position);
                if (dist <= range) return true;
            }
            return false;
        }

        #endregion

        #region Main Engine

        private void ApplyMainEngine(float dt)
        {
            if (_isWarping || _isGalaxyWarping) return;
            if (Mathf.Abs(enginePower) < 0.001f) return;

            float speedMod = SpeedModifier;
            float thrust = stats.mainEngineThrust * enginePower * speedMod;
            velocity += transform.forward * thrust * dt;
        }

        #endregion

        #region Clamp

        private void ClampState()
        {
            // Galaxy warp bypasses normal speed clamp — warp level sets actual speed
            if (_isGalaxyWarping) return;

            float maxSpd = stats.maxSpeed;
            if (velocity.sqrMagnitude > maxSpd * maxSpd)
                velocity = velocity.normalized * maxSpd;

            float maxAngVel = stats.maxAngularVelocity * Mathf.Deg2Rad;
            if (angularVelocity.sqrMagnitude > maxAngVel * maxAngVel)
                angularVelocity = angularVelocity.normalized * maxAngVel;
        }

        #endregion

        #region Warp

        private void ToggleWarp()
        {
            if (IsEngineDisabled) return;
            if (_modularDamage != null && !_modularDamage.CanWarp) return;

            if (_isWarping)
            {
                _isWarping = false;
                flightMode = FlightMode.Normal;
                velocity = Vector3.zero;
                if (_sfx != null) _sfx.StopWarp();
            }
            else if (!_warpCharging && health != null && health.currentEnergy > stats.warpEnergyCost * 3f)
            {
                _warpCharging = true;
                _warpChargeTimer = 0f;
            }
        }

        private void UpdateWarpCharge()
        {
            if (!_warpCharging) return;

            _warpChargeTimer += Time.deltaTime;
            if (_warpChargeTimer >= stats.warpChargeTime)
            {
                _warpCharging = false;
                _isWarping = true;
                flightMode = FlightMode.Warp;
                if (_sfx != null) _sfx.PlayWarp();
            }
        }

        private void ApplyWarpMovement(float dt)
        {
            // Warp zoom-in phase: 3s FOV transition, no movement yet
            if (_warpZooming)
            {
                _warpZoomTimer += dt;
                if (_warpZoomTimer >= WarpZoomDuration)
                {
                    _warpZooming = false;
                    _isGalaxyWarping = true;
                    _galaxyWarpTimer = 0f;
                    flightMode = FlightMode.Warp;
                }
                return;
            }

            // Warp zoom-out phase: 3s FOV transition back, ship stopped
            if (_warpExitZooming)
            {
                _warpExitZoomTimer += dt;
                if (_warpExitZoomTimer >= WarpZoomDuration)
                {
                    _warpExitZooming = false;
                    // Now hide streaks after deceleration complete
                    SetWarpStreaksActive(false);
                }
                return;
            }

            if (_isWarping)
            {
                if (health == null || !health.SpendEnergy(stats.warpEnergyCost * dt))
                {
                    _isWarping = false;
                    flightMode = FlightMode.Normal;
                    velocity = Vector3.zero;
                    if (_sfx != null) _sfx.StopWarp();
                    return;
                }

                Vector3 warpVel = transform.forward * stats.warpSpeed;
                velocity = Vector3.Lerp(velocity, warpVel, 3f * dt);
            }

            if (_isGalaxyWarping)
            {
                _galaxyWarpTimer += dt;
                // Straight-line flight, same as manual warp — velocity locked to ship forward.
                // No per-frame Slerp on direction (that caused camera jitter). Any heading
                // correction is handled at low frequency by AutoWarpNavigator via angularVelocity.
                velocity = transform.forward * GalaxyWarpSpeed;
            }
        }

        public void StartGalaxyWarp(Vector3 destination)
        {
            _isGalaxyWarping = true;
            _galaxyWarpTimer = 0f;
            flightMode = FlightMode.Warp;
            _isWarping = false;
            _warpCharging = false;
        }

        /// <summary>Begin galaxy warp with full 3s zoom-in, streaks, and sound (same as Z key).</summary>
        public void BeginGalaxyWarpAuto()
        {
            _warpZooming = true;
            _warpZoomTimer = 0f;
            _isWarping = false;
            _warpCharging = false;
            InitWarpStreaks();
            SetWarpStreaksActive(true);
            if (_sfx != null) _sfx.PlayWarpEngage();
        }

        /// <summary>End galaxy warp with full 3s zoom-out, streaks, and sound (same as Z key).</summary>
        public void EndGalaxyWarpAuto()
        {
            _isGalaxyWarping = false;
            _warpZooming = false;
            velocity = Vector3.zero;
            flightMode = FlightMode.Normal;
            _warpExitZooming = true;
            _warpExitZoomTimer = 0f;
            if (_sfx != null) _sfx.PlayWarpExit();
        }

        #endregion

        #region Trajectory Prediction

        public Vector3[] PredictTrajectory(int pointCount, float timeStep, float maxTime)
        {
            Vector3[] points = new Vector3[pointCount];
            Vector3 pos = transform.position;
            Vector3 vel = velocity;

            if (_isGalaxyWarping) vel = transform.forward * GalaxyWarpSpeed;  // property reflects current warp level
            else if (_isWarping) vel = transform.forward * stats.warpSpeed;

            float dt = Mathf.Min(timeStep, maxTime / pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (i + 1) * dt;
                points[i] = pos + vel * t;
            }
            return points;
        }

        public Vector3 VelocityDirection => currentVelocity.sqrMagnitude > 0.01f ? currentVelocity.normalized : transform.forward;

        public float VelocityAlignment
        {
            get
            {
                if (currentSpeed < 0.1f) return 0f;
                return Vector3.Angle(transform.forward, currentVelocity.normalized);
            }
        }

        #endregion

        #region Public API

        public void SetFlightMode(FlightMode mode)
        {
            if (_isWarping) return;
            flightMode = mode;
        }

        public void ToggleCombatMode()
        {
            if (_isWarping) return;
            flightMode = flightMode == FlightMode.Normal ? FlightMode.Combat : FlightMode.Normal;
        }

        public void RefreshIonEffect()
        {
            _ionEffect = GetComponent<IonEffect>();
            _modularDamage = GetComponent<ModularDamageSystem>();
        }

        #endregion

        #region Floating Origin

        [Header("Floating Origin")]
        [Tooltip("超过此距离时，将整个场景平移回原点。防止float32精度衰减导致抖动。")]
        public float floatingOriginThreshold = 50000f;

        private static float _originOffsetX;
        private static float _originOffsetY;
        private static float _originOffsetZ;

        /// <summary>世界偏移量（用于HUD/雷达显示真实坐标）。</summary>
        public static Vector3 OriginOffset => new Vector3(_originOffsetX, _originOffsetY, _originOffsetZ);

        /// <summary>执行Floating Origin：把所有根物体平移回原点附近。</summary>
        private void ApplyFloatingOrigin()
        {
            // 只由玩家船执行
            if (!isPlayerControlled) return;

            // 自动曲速导航时不执行 Floating Origin — 目标星球位置不变，
            // 距离检测才能正确工作。精度损失由飞行时间短来弥补。
            if (_autoWarpTarget != null) return;

            float dist = transform.position.magnitude;
            if (dist < floatingOriginThreshold) return;

            Vector3 offset = transform.position;

            // 平移所有根物体
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == null) continue;
                root.transform.position -= offset;
            }

            // 累计偏移
            _originOffsetX += offset.x;
            _originOffsetY += offset.y;
            _originOffsetZ += offset.z;
        }

        #endregion
    }
}
