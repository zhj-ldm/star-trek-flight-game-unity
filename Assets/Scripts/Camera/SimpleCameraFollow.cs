using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Basic third-person camera that follows the ship from behind.
    /// This is a temporary implementation for Phase 2 testing.
    /// Will be replaced by the full CameraRig system in Phase 3.
    /// </summary>
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Position")]
        public Vector3 offset = new Vector3(0f, 3f, -25f);
        public float positionDamp = 6f;

        [Header("Rotation")]
        public float rotationDamp = 4f;
        public bool matchTargetRoll = true;

        [Header("FOV")]
        public float baseFOV = 60f;
        public float boostFOV = 75f;
        public float fovDamp = 3f;

        private Camera _cam;
        private ShipController _targetController;
        private Vector3 _velocityRef;

        void Start()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = gameObject.AddComponent<Camera>();
            _cam.fieldOfView = baseFOV;

            if (target != null)
                _targetController = target.GetComponent<ShipController>();
        }

        void FixedUpdate()
        {
            if (target == null) return;

            // Desired position in target's local space (behind and above)
            Vector3 desiredPos = target.position + target.TransformDirection(offset);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocityRef, 1f / positionDamp);

            // Desired rotation: look at target, optionally match roll
            Vector3 lookDir = target.position - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(lookDir, matchTargetRoll ? target.up : Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationDamp * Time.fixedDeltaTime);
            }

            // Dynamic FOV based on speed
            if (_targetController != null && _cam != null)
            {
                float speedRatio = _targetController.currentSpeed / Mathf.Max(1f, _targetController.stats.maxSpeed);
                float targetFOV = Mathf.Lerp(baseFOV, boostFOV, speedRatio);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, fovDamp * Time.fixedDeltaTime);
            }
        }
    }
}
