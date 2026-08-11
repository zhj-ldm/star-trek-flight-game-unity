using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Reads player input — Orbiter 2016 style control scheme.
    /// 
    /// RCS ROT mode (default):
    ///   W/S = pitch (nose up/down)
    ///   Q/E = roll (left/right)
    ///   A/D = yaw (left/right)
    ///   Rotation has inertia — ship keeps spinning when keys released.
    ///   R = auto-stabilize toggle (damp angular velocity to zero within 2s)
    ///   F = full stop toggle (zero all velocity + angular velocity, ship frozen relative to world)
    /// 
    /// Main Engine:
    ///   P = forward power increase (clamps at 0 from below)
    ///   L = reverse power increase (clamps at 0 from above)
    ///   Pressing P while negative only brings throttle up to 0, not into positive.
    ///   Pressing L while positive only brings throttle down to 0, not into negative.
    /// 
    /// Combat (unchanged):
    ///   Space = phaser, M = torpedo, N = pulse cannon
    ///   K = shield toggle
    /// 
    /// Camera:
    ///   / = reset camera
    ///   Scroll = zoom
    ///   Mouse held = free-look
    /// </summary>
    public static class ShipInput
    {
        public struct InputData
        {
            // ROT mode — angular inputs (-1..1)
            public float pitch;            // W (nose up) / S (nose down)
            public float yaw;             // A (left) / D (right)
            public float roll;            // Q (left) / E (right)

            // Auto-stabilize
            public bool autoStabilize;     // R — toggle auto-stabilize on/off
            public bool autoStabilizeHeld; // R — held (for RCS sustain sound)

            // Full stop
            public bool fullStop;          // F — toggle full stop (zero all velocity)

            // Dock action — instant stop near a space station
            public bool stationStop;        // O — stop instantly if within 200m of a station

            // Main engine
            public bool engineForward;     // P — increase forward power
            public bool engineReverse;     // L — increase reverse power

            // Combat
            public bool firePhaser;        // Space held
            public bool fireTorpedo;       // M pressed
            public bool fireJammer;        // N pressed (pulse cannon)
            public bool toggleShield;      // B pressed
            public bool toggleBridgeView; // K pressed
            public bool increaseWeaponEnergy;  // unused
            public bool toggleLocking;     // unused

            // Targeting
            public bool breakLock;         // BackQuote
            public bool switchLockMode;    // Tab

            // Navigation
            public bool warpToDestination; // Z — galaxy warp

            // Camera
            public bool switchCamera;      // C — (unused) now toggleTranslateMode
            public bool toggleTranslateMode; // C — toggle ROT<->LIN
            public bool resetCamera;       // /
            public bool isFreeLook;        // Any mouse button held
            public float freeLookX;        // Mouse X delta
            public float freeLookY;        // Mouse Y delta

            // Legacy compat (kept for AI / ShipWeaponManager)
            public bool toggleRCSMode;     // unused — RCS toggle removed
            public bool engineOff;          // unused — engine off removed
            public bool attitudeLock;      // unused — F is now full stop
            public bool boost;            // Left Shift
            public bool pulseBoost;        // unused
            public bool warpToggle;        // old T key
            public bool autoBrake;        // old 0 key
            public bool throttleIncrease;  // mapped to engineForward (P)
            public bool throttleDecrease;  // mapped to engineReverse (L)
            public bool toggleAutoAim;    // unused
            public bool increaseShieldEnergy; // unused

            // LIN mode — translate axes driving ApplyTranslationRCS.
            public float translateUp;       // W (+1) / S (-1) — up/down
            public float translateRight;   // D (+1) / A (-1) — right/left
            public float translateForward; // P (+1) / L (-1) — forward/back

            // Realistic mode inputs
            public bool cmdEngineForward;    // Cmd+P held — smooth ramp up toward max
            public bool cmdEngineReverse;    // Cmd+L held — smooth ramp down toward 0
            public bool instantZeroThrust;   // [ key — instant zero engine thrust
            public bool toggleAttitudeLock;  // R in realistic mode — toggle attitude lock
            public bool toggleHUD;           // H key — toggle velocity HUD overlay
        }

        private static InputData _lastInput;
        public static InputData LastInput => _lastInput;

        /// <summary>When true, all ship input is suppressed (e.g. galaxy map open).</summary>
        public static bool SuppressInput;

        public static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static InputData ReadInput()
        {
            if (SuppressInput)
            {
                _lastInput = new InputData();
                return _lastInput;
            }

            bool anyMouse = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);

            // Option modifier (macOS) — for realistic mode fine throttle control
            bool cmdHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool pHeld = Input.GetKey(KeyCode.P);
            bool lHeld = Input.GetKey(KeyCode.L);

            // P/L without Alt = instant full thrust (realistic) or ramp (simple)
            // P/L with Alt = smooth ramp (realistic only)
            bool engineFwd = pHeld && !cmdHeld;
            bool engineRev = lHeld && !cmdHeld;
            bool cmdEngineFwd = pHeld && cmdHeld;
            bool cmdEngineRev = lHeld && cmdHeld;

            var d = new InputData
            {
                // ROT mode attitude
                pitch = (Input.GetKey(KeyCode.S) ? 1f : 0f) - (Input.GetKey(KeyCode.W) ? 1f : 0f),
                roll  = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                yaw   = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),

                // Auto-stabilize — R key (simple mode: one-shot; realistic mode: toggle handled by controller)
                autoStabilize = Input.GetKeyDown(KeyCode.R),
                autoStabilizeHeld = Input.GetKey(KeyCode.R),

                // Full stop — F key toggles on/off
                fullStop = Input.GetKeyDown(KeyCode.F),

                // Dock action — O: stop instantly (only near a station, checked in controller)
                stationStop = Input.GetKeyDown(KeyCode.O),

                // Main engine — P=forward, L=reverse
                engineForward = engineFwd,
                engineReverse = engineRev,

                // Combat
                firePhaser   = Input.GetKey(KeyCode.Space),
                fireTorpedo  = Input.GetKeyDown(KeyCode.M),
                fireJammer   = Input.GetKeyDown(KeyCode.N),
                toggleShield = Input.GetKeyDown(KeyCode.K),
                toggleBridgeView = Input.GetKeyDown(KeyCode.I),
                increaseWeaponEnergy = false,
                toggleLocking = false,
                switchLockMode = Input.GetKeyDown(KeyCode.Tab),

                // Targeting
                breakLock = Input.GetKeyDown(KeyCode.BackQuote),

                // Navigation
                warpToDestination = Input.GetKeyDown(KeyCode.Z),

                // Camera
                switchCamera = Input.GetKeyDown(KeyCode.C),
                toggleTranslateMode = Input.GetKeyDown(KeyCode.C),
                resetCamera  = Input.GetKeyDown(KeyCode.Slash),
                isFreeLook   = anyMouse,
                freeLookX    = anyMouse ? Input.GetAxis("Mouse X") : 0f,
                freeLookY    = anyMouse ? Input.GetAxis("Mouse Y") : 0f,

                // Legacy compat
                toggleRCSMode = false,
                engineOff = false,
                attitudeLock = false,
                boost = Input.GetKey(KeyCode.LeftShift),
                pulseBoost = false,
                warpToggle = Input.GetKeyDown(KeyCode.T),
                autoBrake = Input.GetKey(KeyCode.Alpha0) || Input.GetKey(KeyCode.Keypad0),
                throttleIncrease = engineFwd,
                throttleDecrease = engineRev,
                toggleAutoAim = false,
                increaseShieldEnergy = false,

                // LIN mode axes
                translateUp      = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f),
                translateRight   = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                translateForward = (Input.GetKey(KeyCode.P) ? 1f : 0f) - (Input.GetKey(KeyCode.L) ? 1f : 0f),

                // Realistic mode inputs
                cmdEngineForward = cmdEngineFwd,
                cmdEngineReverse = cmdEngineRev,
                instantZeroThrust = Input.GetKeyDown(KeyCode.LeftBracket),
                toggleAttitudeLock = Input.GetKeyDown(KeyCode.R),
                toggleHUD = Input.GetKeyDown(KeyCode.H),
            };

            _lastInput = d;
            return d;
        }
    }
}
