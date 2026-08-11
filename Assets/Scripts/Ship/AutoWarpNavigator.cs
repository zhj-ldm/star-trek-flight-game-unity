using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Auto warp navigation — passive helper. It does NOT start warp itself.
    /// Responsibilities:
    ///   1. Align the ship toward the target (one-time snap, player starts warp with Z).
    ///   2. Once the player engages warp (Z), ShipController's stop detection (driven by
    ///      _autoWarpTarget) cuts warp when within arrival distance.
    /// All warp enable/disable remains fully player/manual — identical to pressing Z,
    /// which eliminates any warp-induced jitter.
    /// </summary>
    public class AutoWarpNavigator : MonoBehaviour
    {
        private ShipController _controller;
        private Transform _target;

        private enum Phase { Idle, Aligning, Done }
        private Phase _phase = Phase.Idle;

        private float _alignTimer;
        private const float MaxAlignTime = 5f;
        private float _prevDist = float.MaxValue;

        public bool IsActive => _phase == Phase.Aligning;
        public string StatusText
        {
            get
            {
                switch (_phase)
                {
                    case Phase.Aligning:
                        float dist = GetSurfaceDistance();
                        return $"已对准 · 按Z曲速 ({dist/1000f:F1}km)";
                    case Phase.Done: return "已到达目标方向";
                    default: return "";
                }
            }
        }

        void Awake() { _controller = GetComponent<ShipController>(); }

        public void NavigateTo(Transform target, float planetRadius)
        {
            _target = target;
            _phase = Phase.Aligning;
            _alignTimer = 0f;
            _controller.angularVelocity = Vector3.zero;
            _prevDist = float.MaxValue;

            // Use the player's current warp level — do NOT auto-bump to warp 9.
            Debug.Log($"[AutoWarp] NavigateTo {target.name}, level={_controller.GalaxyWarpLevel}, dist={GetSurfaceDistance():F0}m");
        }

        public void Cancel()
        {
            _controller.ClearAutoWarpTarget();
            _controller.angularVelocity = Vector3.zero;
            _phase = Phase.Idle;
            _target = null;
        }

        /// <summary>Called by ShipController when auto-warp stop condition is met (arrival).</summary>
        public void OnArrived()
        {
            Debug.Log($"[AutoWarp] OnArrived — warp stopped by arrival");
            _controller.angularVelocity = Vector3.zero;
            _phase = Phase.Done;
            _target = null;
        }

        void Update()
        {
            if (_controller == null || _target == null) { _phase = Phase.Idle; return; }

            // After alignment is done, we only watch for arrival while the player warps.
            if (_phase == Phase.Done)
            {
                CheckArrival();
                return;
            }

            // Aligning phase — smooth rotation toward target via angularVelocity (same as
            // manual RCS). Once the player engages warp (Z), we stop aligning and begin
            // monitoring for arrival.
            if (_controller.IsGalaxyWarping || _controller.IsWarpZooming)
            {
                _controller.angularVelocity = Vector3.zero;
                _phase = Phase.Done;
                return;
            }

            if (_alignTimer >= MaxAlignTime)
            {
                // Timed out — leave the ship where it is facing and hand off to the player.
                _phase = Phase.Done;
                _controller.angularVelocity = Vector3.zero;
                return;
            }

            _alignTimer += Time.deltaTime;
            Vector3 toTarget = (_target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toTarget);

            // Once aligned, stop rotating and wait for the player to press Z.
            if (angle < 0.5f)
            {
                _controller.angularVelocity = Vector3.zero;
                Debug.Log($"[AutoWarp] Aligned within {angle:F1}° — press Z to warp. dist={GetSurfaceDistance():F0}m");
                _phase = Phase.Done;
                return;
            }

            // Align by rotating the transform directly (NOT angularVelocity — the controller's
            // auto-stabilize lerps angularVelocity toward zero and would cancel our turn).
            // RotateTowards is fast (up to 100°/s), exact, and because warp only starts when the
            // player presses Z (ship still), there's no camera jitter from this rotation.
            Vector3 axis = Vector3.Cross(transform.forward, toTarget).normalized;
            if (axis.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget, transform.up);
                float maxDeg = Mathf.Max(120f * Time.deltaTime, angle); // ensure we close in well before timeout
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDeg);
                _controller.angularVelocity = Vector3.zero;
            }
        }

        // ── Arrival detection — runs every frame while target is set. Only acts once the
        // player is actually galaxy-warping. Stops warp on arrive or clear fly-past. Note we
        // do NOT set ShipController._autoWarpTarget, so Floating Origin stays active during
        // warp (this is what prevents long-distance precision jitter).
        private void CheckArrival()
        {
            if (_target == null) return;
            if (!_controller.IsGalaxyWarping) { _prevDist = GetSurfaceDistance(); return; }

            float sd = GetSurfaceDistance();
            bool closeEnough = sd <= 100f;
            bool hasBaseline = _prevDist < float.MaxValue;
            bool withinRange = sd < 5000f;
            bool flewPast = withinRange && hasBaseline && sd > _prevDist + 0.5f;

            if (closeEnough || flewPast)
            {
                Debug.Log($"[AutoWarp] Arrived — closeEnough={closeEnough} flewPast={flewPast} sd={sd:F0}m");
                _controller.EndGalaxyWarpAuto();
                OnArrived();
            }
            else
            {
                _prevDist = sd;
            }
        }

        private float GetSurfaceDistance()
        {
            if (_target == null) return float.MaxValue;
            float dist = Vector3.Distance(transform.position, _target.position);
            float worldRadius = Mathf.Max(_target.localScale.x, _target.localScale.y, _target.localScale.z) * 0.5f;
            return dist - worldRadius;
        }
    }
}