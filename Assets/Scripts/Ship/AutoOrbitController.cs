using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Auto-orbit controller — takes over ship control to enter circular orbit around nearest planet.
    /// Orbit altitude is height above planet surface.
    /// Phases: Aligning (5s rotate to tangent) → Inserting (55s move to orbit circle) → Orbiting (circular orbit).
    /// Total time budget: 60s guaranteed completion, breaking physics if necessary.
    /// </summary>
    public class AutoOrbitController : MonoBehaviour
    {
        private ShipController _controller;
        private Transform _planet;
        private float _planetRadius;
        private float _orbitRadius;
        private float _orbitSpeed;
        private float _orbitAngle;
        private float _orbitDirection = 1f;

        private bool _active;

        private enum OrbitPhase { None, Aligning, Inserting, Orbiting }
        private OrbitPhase _phase = OrbitPhase.None;

        private const float TotalTimeBudget = 60f;
        private const float AlignDuration = 5f;
        private const float InsertDuration = TotalTimeBudget - AlignDuration; // 55s
        private float _phaseTimer;
        private Quaternion _alignStartRot;
        private Quaternion _orbitTargetRot;

        private Vector3 _insertStartPos;
        private Vector3 _insertTargetPos;

        public bool IsActive => _active;
        public string StatusText => _phase switch
        {
            OrbitPhase.Aligning => "对准中",
            OrbitPhase.Inserting => "入轨中",
            OrbitPhase.Orbiting => "轨道运行",
            _ => ""
        };
        public string PlanetName => _planet != null ? _planet.name : "";

        /// <summary>Progress 0→1 within the 60s budget (Aligning + Inserting only).</summary>
        public float Progress
        {
            get
            {
                if (_phase == OrbitPhase.Aligning) return _phaseTimer / TotalTimeBudget;
                if (_phase == OrbitPhase.Inserting) return (AlignDuration + _phaseTimer) / TotalTimeBudget;
                return 1f;
            }
        }

        void Awake()
        {
            _controller = GetComponent<ShipController>();
        }

        /// <summary>Find the nearest planet by surface distance (dist to center - planet radius).</summary>
        public static bool FindNearestPlanet(Vector3 fromPos, out Transform planet, out float radius, out float surfaceDist)
        {
            planet = null;
            radius = 0f;
            surfaceDist = float.MaxValue;
            float minSurfaceDist = float.MaxValue;

            var renderers = FindObjectsOfType<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.gameObject.name.Contains("Planet")) continue;
                float dist = Vector3.Distance(fromPos, r.bounds.center);
                float rad = r.bounds.extents.magnitude;
                float surfDist = dist - rad;
                if (surfDist < minSurfaceDist)
                {
                    minSurfaceDist = surfDist;
                    planet = r.transform;
                    radius = rad;
                    surfaceDist = surfDist;
                }
            }
            return planet != null;
        }

        /// <summary>Start auto-orbit sequence at given altitude above surface.</summary>
        public bool StartOrbit(float altitudeAboveSurface)
        {
            if (!FindNearestPlanet(transform.position, out var planet, out var planetRadius, out _))
                return false;

            _planet = planet;
            _planetRadius = planetRadius;
            _orbitRadius = planetRadius + altitudeAboveSurface;
            _orbitSpeed = Mathf.Sqrt(2000000f / _orbitRadius);

            // Calculate orbit angle from current position (horizontal plane)
            Vector3 toShip = transform.position - planet.position;
            toShip.y = 0;
            if (toShip.sqrMagnitude < 0.01f)
                _orbitAngle = 0f;
            else
                _orbitAngle = Mathf.Atan2(toShip.z, toShip.x);

            // Determine orbit direction from current velocity
            Vector3 tangent = GetTangent(_orbitAngle);
            if (_controller != null && _controller.velocity.sqrMagnitude > 0.1f)
            {
                float velAlignment = Vector3.Dot(_controller.velocity.normalized, tangent);
                _orbitDirection = velAlignment >= 0 ? 1f : -1f;
            }
            else
            {
                _orbitDirection = 1f;
            }

            // Pre-compute target rotation (face orbit tangent) — stable, no jitter
            Vector3 tangentDir = GetTangent(_orbitAngle) * _orbitDirection;
            if (tangentDir.sqrMagnitude < 0.001f) tangentDir = Vector3.forward;
            _orbitTargetRot = Quaternion.LookRotation(tangentDir, Vector3.up);

            // Pre-compute insertion target — nearest point on orbit circle (horizontal plane)
            float currentDist = toShip.magnitude;
            if (currentDist > 0.01f)
            {
                Vector3 dir = toShip / currentDist;
                _insertTargetPos = planet.position + dir * _orbitRadius;
                _insertTargetPos.y = planet.position.y;
            }
            else
            {
                _insertTargetPos = GetOrbitPosition(_orbitAngle);
            }

            // Break physics: zero everything immediately
            _phase = OrbitPhase.Aligning;
            _phaseTimer = 0f;
            _active = true;
            _alignStartRot = transform.rotation;

            if (_controller != null)
            {
                _controller.velocity = Vector3.zero;
                _controller.angularVelocity = Vector3.zero;
                _controller.autoOrbit = true;
                _controller.autoStabilize = false;
                _controller.fullStop = false;
                _controller.enginePower = 0f;
            }

            return true;
        }

        private Vector3 GetTangent(float angle)
        {
            return new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
        }

        private Vector3 GetOrbitPosition(float angle)
        {
            return _planet.position + new Vector3(Mathf.Cos(angle) * _orbitRadius, 0, Mathf.Sin(angle) * _orbitRadius);
        }

        /// <summary>Called by ShipController.Update() when autoOrbit is true.</summary>
        public void UpdateOrbit(float dt)
        {
            if (!_active || _controller == null || _planet == null)
            {
                CancelOrbit();
                return;
            }

            switch (_phase)
            {
                case OrbitPhase.Aligning:
                    UpdateAligning(dt);
                    break;
                case OrbitPhase.Inserting:
                    UpdateInserting(dt);
                    break;
                case OrbitPhase.Orbiting:
                    UpdateOrbiting(dt);
                    break;
            }
        }

        private void UpdateAligning(float dt)
        {
            _phaseTimer += dt;
            float t = Mathf.Clamp01(_phaseTimer / AlignDuration);

            // Smooth rotation from current to orbit tangent — no jitter
            transform.rotation = Quaternion.Slerp(_alignStartRot, _orbitTargetRot, t);

            // Keep velocity zeroed (break physics)
            _controller.velocity = Vector3.zero;
            _controller.angularVelocity = Vector3.zero;

            if (t >= 1f)
            {
                _phase = OrbitPhase.Inserting;
                _phaseTimer = 0f;
                _insertStartPos = transform.position;
            }
        }

        private void UpdateInserting(float dt)
        {
            _phaseTimer += dt;
            float t = Mathf.Clamp01(_phaseTimer / InsertDuration);

            // Direct position Lerp — guaranteed to reach target within InsertDuration.
            // Breaks physics (ignores velocity/inertia) but ensures 60s completion.
            transform.position = Vector3.Lerp(_insertStartPos, _insertTargetPos, t);

            // Face tangent direction
            transform.rotation = _orbitTargetRot;

            _controller.velocity = Vector3.zero;
            _controller.angularVelocity = Vector3.zero;

            if (t >= 1f)
            {
                _phase = OrbitPhase.Orbiting;
                _phaseTimer = 0f;

                // Recalculate angle from final position
                Vector3 toShip = transform.position - _planet.position;
                toShip.y = 0;
                if (toShip.sqrMagnitude > 0.01f)
                    _orbitAngle = Mathf.Atan2(toShip.z, toShip.x);
            }
        }

        private void UpdateOrbiting(float dt)
        {
            float angularVel = _orbitSpeed * _orbitDirection / _orbitRadius;
            _orbitAngle += angularVel * dt;

            Vector3 targetPos = GetOrbitPosition(_orbitAngle);
            transform.position = targetPos;

            Vector3 tangent = GetTangent(_orbitAngle) * _orbitDirection;
            _controller.velocity = tangent * _orbitSpeed;
            _controller.angularVelocity = Vector3.zero;

            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        public void CancelOrbit()
        {
            _active = false;
            _phase = OrbitPhase.None;
            if (_controller != null)
                _controller.autoOrbit = false;
        }
    }
}
