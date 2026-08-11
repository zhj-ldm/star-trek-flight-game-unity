using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Camera system with two modes:
    /// - ThirdPerson: Fixed distance behind ship. Mouse drag orbits view (stays until / pressed).
    ///   Scroll wheel zooms in/out.
    ///   When a target is sticky-locked, camera auto-rotates to keep it in the scan circle.
    /// - Tactical: Top-down overview with scroll zoom.
    /// Uses LateUpdate to eliminate jitter.
    /// 
    /// Camera Follow Modes (toggled with ' key):
    /// - Rigid: camera rigidly follows ship rotation (Mode 1).
    /// - Soft: camera allows up to maxSoftAngle offset before following (Mode 2).
    ///   Within the threshold, you can see the ship pitch/roll relative to the camera.
    ///   Beyond the threshold, the camera follows just enough to cap the offset.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;
        public ShipController targetController;
        public TargetingSystem targeting;

        [Header("Mode Settings")]
        public CameraMode currentMode = CameraMode.ThirdPerson;
        public float transitionSpeed = 3f;

        [Header("Follow Mode (toggle with ' key)")]
        public FollowMode followMode = FollowMode.Rigid;
        [Tooltip("Max angular offset before camera starts following (Soft mode)")]
        public float maxSoftAngle = 20f;
        [Tooltip("How fast camera catches up once offset exceeds threshold")]
        public float softFollowSpeed = 5f;

        [Header("Third Person Settings")]
        [Tooltip("Base distance from ship to camera")]
        public float followDistance = 25f;
        [Tooltip("Min zoom distance")]
        public float minDistance = 0.1f;
        [Tooltip("Max zoom distance")]
        public float maxDistance = 100000f;
        [Tooltip("Zoom speed")]
        public float zoomSpeed = 0.3f;
        [Tooltip("Height offset above ship center")]
        public float followHeight = 3f;
        public float tpBaseFOV = 60f;
        public float tpBoostFOV = 75f;

        [Header("Free-Look Settings")]
        public float freeLookSensitivity = 3f;
        public float returnSpeed = 5f;
        public float minPitch = -70f;
        public float maxPitch = 70f;

        [Header("Target Tracking")]
        [Tooltip("How fast camera rotates to keep locked target in scan circle")]
        public float trackSpeed = 3f;
        [Tooltip("Screen-space margin (fraction of scan radius) before tracking kicks in")]
        public float trackMargin = 0.7f;

        [Header("Tactical Settings")]
        public float tacticalHeight = 200f;
        public float tacticalFOV = 50f;
        public float tacticalZoomMin = 50f;
        public float tacticalZoomMax = 500f;
        public float tacticalCurrentZoom = 200f;

        private Camera _cam;
        private float _transitionProgress = 1f;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private float _transitionStartFOV;

        // Free-look orbit angles (relative to ship's heading)
        private float _orbitYaw;
        private float _orbitPitch;

        // Soft-follow internal rotation (Mode 2)
        private Quaternion _softCameraRot;
        private bool _softInitialized;

        // Current actual zoom distance (smoothed)
        private float _currentDistance;
        private Light _viewLight;
        private Transform _bridgeAnchor;

        // Properties
        public bool IsTransitioning => _transitionProgress < 1f;

        void Start()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = gameObject.AddComponent<Camera>();

            // Large far clip so planets are always visible even at 50000+ distance
            _cam.nearClipPlane = 0.01f;
            _cam.farClipPlane = 500000f;
            _cam.allowHDR = true;
            _cam.allowMSAA = true;

            // Fixed-angle view light on a SEPARATE GameObject (not the camera)
            // Direction follows ship rotation, not camera rotation
            var lightGO = new GameObject("ViewLight");
            _viewLight = lightGO.AddComponent<Light>();
            _viewLight.type = LightType.Directional;
            _viewLight.color = new Color(0.85f, 0.88f, 0.95f, 1f);
            _viewLight.intensity = 0f; // Camera light OFF
            _viewLight.shadows = LightShadows.None;
            _viewLight.cullingMask = ~0;

            if (target != null && targetController == null)
                targetController = target.GetComponent<ShipController>();

            if (targeting == null && targetController != null)
                targeting = targetController.GetComponent<TargetingSystem>();

            // Find bridge camera anchor on target ship
            if (target != null)
            {
                var bridgeGO = target.Find("CameraAnchor_Bridge");
                if (bridgeGO != null)
                    _bridgeAnchor = bridgeGO;
            }

            _currentDistance = followDistance;
            SnapToMode(currentMode);
        }

        void Update()
        {
            // C is reserved for translate/roll ship-mode toggle — do NOT bind to camera.
            // Tactical (top-down) view on B key.
            if (Input.GetKeyDown(KeyCode.B) && currentMode != CameraMode.Bridge)
                CycleMode();

            // Toggle follow mode with ' key
            if (Input.GetKeyDown(KeyCode.Quote))
            {
                followMode = followMode == FollowMode.Rigid ? FollowMode.Soft : FollowMode.Rigid;
                if (followMode == FollowMode.Soft && target != null)
                {
                    _softCameraRot = target.rotation;
                    _softInitialized = true;
                }
            }

            var input = ShipInput.LastInput;

            if (input.resetCamera)
                ResetCamera();

            if (Input.GetKeyDown(KeyCode.Slash))
                ResetCamera();

            if (_transitionProgress < 1f)
                _transitionProgress = Mathf.Min(1f, _transitionProgress + Time.deltaTime * transitionSpeed);

            // Scroll wheel zoom — blocked when input suppressed (galaxy map open)
            float scroll = ShipInput.SuppressInput ? 0f : Input.GetAxis("Mouse ScrollWheel");
            if (currentMode == CameraMode.ThirdPerson)
            {
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    followDistance = Mathf.Clamp(followDistance * (1f - scroll * zoomSpeed), minDistance, maxDistance);
                }
            }

            // Debug: [ and ] keys to adjust view light intensity at runtime
            if (Input.GetKey(KeyCode.LeftBracket))
            {
                _viewLight.intensity = Mathf.Max(0f, _viewLight.intensity - 0.05f);
                Debug.Log($"[ViewLight] intensity = {_viewLight.intensity:F2}");
            }
            if (Input.GetKey(KeyCode.RightBracket))
            {
                _viewLight.intensity = Mathf.Min(8f, _viewLight.intensity + 0.05f);
                Debug.Log($"[ViewLight] intensity = {_viewLight.intensity:F2}");
            }
            else if (currentMode == CameraMode.Tactical)
            {
                tacticalCurrentZoom = Mathf.Clamp(tacticalCurrentZoom - scroll * 50f, tacticalZoomMin, tacticalZoomMax);
            }
        }

        /// <summary>Reset camera orbit to behind ship.</summary>
        private void ResetCamera()
        {
            _orbitYaw = 0f;
            _orbitPitch = 0f;
            if (followMode == FollowMode.Soft && target != null)
                _softCameraRot = target.rotation;
        }

        /// <summary>
        /// LateUpdate — runs after all physics + FixedUpdate.
        /// </summary>
        void LateUpdate()
        {
            if (target == null) return;

            // View light: fixed at default camera angle (behind ship, same as ResetCamera)
            // Rotation doesn't change — only position follows the ship
            if (_viewLight != null)
            {
                _viewLight.transform.position = target.position;
                _viewLight.transform.rotation = target.rotation;
            }

            // Smooth zoom — compute target distance (warp may override)
            float desiredDist = followDistance;
            if (targetController != null)
            {
                if (targetController.IsWarpZooming)
                    desiredDist = Mathf.Lerp(followDistance, followDistance * 0.8f, targetController.WarpZoomProgress);
                else if (targetController.IsGalaxyWarping)
                    desiredDist = followDistance * 0.8f;
                else if (targetController.IsWarpExitZooming)
                    desiredDist = Mathf.Lerp(followDistance * 0.8f, followDistance, targetController.WarpExitZoomProgress);
            }
            _currentDistance = Mathf.Lerp(_currentDistance, desiredDist, 15f * Time.deltaTime);

            if (IsTransitioning)
            {
                UpdateTransition();
            }
            else
            {
                switch (currentMode)
                {
                    case CameraMode.ThirdPerson:
                        UpdateThirdPerson();
                        break;
                    case CameraMode.Tactical:
                        UpdateTactical();
                        break;
                    case CameraMode.Bridge:
                        UpdateBridge();
                        break;
                }
            }
        }

        #region Third Person

        private void UpdateThirdPerson()
        {
            var input = ShipInput.LastInput;

            // Update free-look orbit — stays where user left it (no auto-return)
            if (input.isFreeLook)
            {
                _orbitYaw += input.freeLookX * freeLookSensitivity;
                _orbitPitch -= input.freeLookY * freeLookSensitivity;
                _orbitPitch = Mathf.Clamp(_orbitPitch, minPitch, maxPitch);
            }

            // Determine base rotation and up vector based on follow mode
            Quaternion baseRot;
            Vector3 upVector;

            if (followMode == FollowMode.Soft)
            {
                if (!_softInitialized && target != null)
                {
                    _softCameraRot = target.rotation;
                    _softInitialized = true;
                }

                // Soft follow: cap angular offset at maxSoftAngle.
                // Within the threshold, camera doesn't rotate — you can see the ship pitch/roll.
                // Beyond the threshold, camera follows just enough to maintain maxSoftAngle offset.
                float angleDiff = Quaternion.Angle(_softCameraRot, target.rotation);
                if (angleDiff > maxSoftAngle)
                {
                    float t = (angleDiff - maxSoftAngle) / angleDiff;
                    _softCameraRot = Quaternion.Slerp(_softCameraRot, target.rotation, t);
                }

                baseRot = _softCameraRot;
                upVector = _softCameraRot * Vector3.up;
            }
            else
            {
                baseRot = target.rotation;
                upVector = target.up;
            }

            Quaternion orbitRot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            Quaternion combinedRot = baseRot * orbitRot;

            // Camera positioned directly behind ship at same height — no height offset prevents flip
            Vector3 offset = combinedRot * new Vector3(0f, 0f, -_currentDistance);
            Vector3 desiredPos = target.position + offset;

            transform.position = desiredPos;

            // Look at ship center, using ship's up vector so pitch/roll is followed
            Vector3 lookDir = target.position - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir, upVector);

            // Dynamic FOV — warp zoom transitions + galaxy warp
            if (_cam != null)
            {
                float targetFOV;
                if (targetController != null && targetController.IsWarpZooming)
                {
                    targetFOV = Mathf.Lerp(tpBaseFOV, 95f, targetController.WarpZoomProgress);
                }
                else if (targetController != null && targetController.IsGalaxyWarping)
                {
                    targetFOV = 95f;
                }
                else if (targetController != null && targetController.IsWarpExitZooming)
                {
                    targetFOV = Mathf.Lerp(95f, tpBaseFOV, targetController.WarpExitZoomProgress);
                }
                else
                {
                    float speedRatio = (targetController != null && targetController.stats != null)
                        ? targetController.currentSpeed / Mathf.Max(1f, targetController.stats.maxSpeed)
                        : 0f;
                    targetFOV = Mathf.Lerp(tpBaseFOV, tpBoostFOV, speedRatio);
                }

                _cam.fieldOfView = targetFOV;
            }
        }

        // UpdateTargetTracking removed — no automatic camera rotation

        #endregion

        #region Tactical

        private void UpdateTactical()
        {
            if (_cam != null)
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, tacticalFOV, 5f * Time.deltaTime);

            Vector3 desiredPos = target.position + new Vector3(0, tacticalCurrentZoom, 0);
            transform.position = desiredPos;

            Quaternion downRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, downRot, 5f * Time.deltaTime);
        }

        #endregion

        #region Bridge

        private void UpdateBridge()
        {
            var input = ShipInput.LastInput;

            // Free-look with mouse — same orbit angles as third person
            if (input.isFreeLook)
            {
                _orbitYaw += input.freeLookX * freeLookSensitivity;
                _orbitPitch -= input.freeLookY * freeLookSensitivity;
                _orbitPitch = Mathf.Clamp(_orbitPitch, minPitch, maxPitch);
            }

            // Base position from anchor
            if (_bridgeAnchor != null)
                transform.position = _bridgeAnchor.position;
            else
                transform.position = target.position + target.up * 1.5f;

            // Arrow keys to move anchor position — world-space forward/right (not affected by ship rotation)
            // Option(Alt) + Up/Down = vertical move at half speed
            if (_bridgeAnchor != null)
            {
                float moveSpeed = 0.2f * Time.deltaTime;
                Vector3 move = Vector3.zero;
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (alt)
                {
                    if (Input.GetKey(KeyCode.UpArrow)) move += Vector3.up * moveSpeed * 0.5f;
                    if (Input.GetKey(KeyCode.DownArrow)) move -= Vector3.up * moveSpeed * 0.5f;
                }
                else
                {
                    if (Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward * moveSpeed;
                    if (Input.GetKey(KeyCode.DownArrow)) move -= Vector3.forward * moveSpeed;
                    if (Input.GetKey(KeyCode.RightArrow)) move += Vector3.right * moveSpeed;
                    if (Input.GetKey(KeyCode.LeftArrow)) move -= Vector3.right * moveSpeed;
                }
                _bridgeAnchor.localPosition += move;
            }

            // Base rotation from anchor, then apply free-look orbit
            Quaternion baseRot = (_bridgeAnchor != null) ? _bridgeAnchor.rotation : target.rotation;
            Quaternion orbitRot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            transform.rotation = baseRot * orbitRot;

            if (_cam != null) _cam.fieldOfView = tpBaseFOV;
        }

        #endregion

        #region Transitions

        private void CycleMode()
        {
            currentMode = currentMode == CameraMode.ThirdPerson ? CameraMode.Tactical : CameraMode.ThirdPerson;
            StartTransition();
        }

        private void StartTransition()
        {
            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionStartFOV = _cam != null ? _cam.fieldOfView : 60f;
            _transitionProgress = 0f;
        }

        private void UpdateTransition()
        {
            Vector3 targetPos;
            Quaternion targetRot;
            float targetFOV;

            switch (currentMode)
            {
                case CameraMode.ThirdPerson:
                    targetPos = target.position + target.rotation * new Vector3(0f, followHeight, -_currentDistance);
                    Vector3 lookDir = target.position - targetPos;
                    targetRot = lookDir.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(lookDir, target.up)
                        : target.rotation;
                    targetFOV = tpBaseFOV;
                    break;

                case CameraMode.Tactical:
                    targetPos = target.position + new Vector3(0, tacticalCurrentZoom, 0);
                    targetRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    targetFOV = tacticalFOV;
                    break;

                case CameraMode.Bridge:
                    if (_bridgeAnchor != null)
                    {
                        targetPos = _bridgeAnchor.position;
                        targetRot = _bridgeAnchor.rotation;
                    }
                    else
                    {
                        targetPos = target.position + target.up * 1.5f;
                        targetRot = target.rotation;
                    }
                    targetFOV = tpBaseFOV;
                    break;

                default:
                    targetPos = transform.position;
                    targetRot = transform.rotation;
                    targetFOV = _cam != null ? _cam.fieldOfView : 60f;
                    break;
            }

            float t = EaseInOutCubic(_transitionProgress);
            transform.position = Vector3.Lerp(_transitionStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(_transitionStartRot, targetRot, t);
            if (_cam != null)
                _cam.fieldOfView = Mathf.Lerp(_transitionStartFOV, targetFOV, t);
        }

        private void SnapToMode(CameraMode mode)
        {
            currentMode = mode;
            _transitionProgress = 1f;
            _currentDistance = followDistance;

            switch (mode)
            {
                case CameraMode.ThirdPerson:
                    transform.position = target.position + target.rotation * new Vector3(0f, followHeight, -_currentDistance);
                    Vector3 lookDir = target.position - transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(lookDir, target.up);
                    if (_cam != null) _cam.fieldOfView = tpBaseFOV;
                    break;

                case CameraMode.Tactical:
                    transform.position = target.position + new Vector3(0, tacticalCurrentZoom, 0);
                    transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    if (_cam != null) _cam.fieldOfView = tacticalFOV;
                    break;
            }
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        #endregion

        #region Public API

        public void SetMode(CameraMode mode)
        {
            if (mode == currentMode) return;
            currentMode = mode;
            StartTransition();
        }

        public CameraMode GetMode() => currentMode;

        public void SetViewLightIntensity(float v)
        {
            if (_viewLight != null) _viewLight.intensity = v;
            // Also control the scene's SunLight so slider has full control over ship brightness
            var sunLight = GameObject.Find("SunLight");
            if (sunLight != null)
            {
                var l = sunLight.GetComponent<Light>();
                if (l != null) l.intensity = v;
            }
        }

        #endregion
    }
}
