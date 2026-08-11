using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// 敌舰AI — 单舰逐次交战系统。
    /// 每20秒重新检测，指派最近的敌舰接近玩家。
    /// 同时最多2艘处于交战状态。其余在星球轨道低速巡航。
    /// 
    /// 到场保证：如果指派时预计到场时间超过90秒（1.5分钟），
    /// 自动加速到场，确保玩家在任一星系1.5分钟内受到攻击。
    /// </summary>
    public class EnemyShipAI : ShipAI
    {
        [Header("Enemy Specific")]
        public float patrolRadius = 300f;
        public Vector3 patrolCenter;

        [Header("Engagement Limit")]
        [Tooltip("同时处于交战状态的最大数量")]
        public int maxConcurrentEngagers = 2;
        [Tooltip("未获交战资格的飞船在此距离环绕待命")]
        public float standoffDistance = 600f;

        [Tooltip("被指派后延迟开始接近的秒数")]
        public float approachDelay = 0f;

        [Header("Arrival Guarantee")]
        [Tooltip("到场时限（秒），超过则加速")]
        public float arrivalDeadline = 10f;
        [Tooltip("敌舰正常曲速速度")]
        public float warpSpeed = 333f;

        [Header("Bajor Exclusion Zone")]
        [Tooltip("Bajor 星系禁入半径（敌舰不可进入此范围内）")]
        public float bajorExclusionRadius = 100000f;
        private Transform _bajorSun;

        // ── 单舰逐次指派系统 ──
        private static EnemyShipAI _assignedApproacher;
        private bool _isAssigned;
        private float _approachDelayTimer;

        // 接近计时器 — 用于追踪到场时间
        private float _approachTimer;
        private bool _isWarping;

        // 加速到场：出发时计算的所需速度
        private float _boostSpeed = 0f;

        private float _evadeTimer;
        private bool _isEvading;
        private Vector3 _evadeDir;

        // Bajor 禁入区 flee 标记
        private bool _fleeingBajor;

        protected override void Start()
        {
            base.Start();
            if (patrolCenter == Vector3.zero)
                patrolCenter = transform.position;
            currentState = AIState.Patrol;

            var bajorSunGo = GameObject.Find("Bajor_Sun");
            if (bajorSunGo != null) _bajorSun = bajorSunGo.transform;
        }

        protected override void Update()
        {
            base.Update();
            if (_approachDelayTimer > 0f)
                _approachDelayTimer -= Time.deltaTime;

            // 到场计时 — 从指派时刻开始（含延迟时间）
            if (_isAssigned && currentState != AIState.Engage)
                _approachTimer += Time.deltaTime;
        }

        void OnDestroy()
        {
            if (_assignedApproacher == this)
                _assignedApproacher = null;
        }

        private int CountActiveEngagers()
        {
            int count = 0;
            var allAI = FindObjectsOfType<EnemyShipAI>();
            foreach (var ai in allAI)
            {
                if (ai == null) continue;
                if (!ai._health.IsAlive) continue;
                if (ai.currentState == AIState.Engage) count++;
            }
            return count;
        }

        /// <summary>
        /// 单舰逐次指派：被击毁后才指派下一艘最近的。
        /// </summary>
        private bool CanApproachPlayer()
        {
            if (currentState == AIState.Engage) return true;

            if (_assignedApproacher != null)
            {
                if (_assignedApproacher._health == null || !_assignedApproacher._health.IsAlive)
                    _assignedApproacher = null;
                else if (_assignedApproacher != this)
                    return false;
            }

            int engagers = CountActiveEngagers();
            if (engagers >= maxConcurrentEngagers)
                return false;

            if (_assignedApproacher == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null) return false;

                float myDist = Vector3.Distance(transform.position, playerObj.transform.position);

                var allAI = FindObjectsOfType<EnemyShipAI>();
                foreach (var ai in allAI)
                {
                    if (ai == null || ai == this) continue;
                    if (!ai._health.IsAlive) continue;
                    if (ai.currentState == AIState.Engage) continue;
                    if (ai._isAssigned) continue;

                    float otherDist = Vector3.Distance(ai.transform.position, playerObj.transform.position);
                    if (otherDist < myDist)
                        return false;
                }

                _assignedApproacher = this;
                _isAssigned = true;
                _approachDelayTimer = approachDelay;

                // 计算到场速度保证 — 从指派时刻开始计时（含延迟）
                float dist = Vector3.Distance(transform.position, playerObj.transform.position);
                float eta = dist / Mathf.Max(1f, warpSpeed);
                if (eta > arrivalDeadline)
                {
                    // 加速到场：速度 = 距离 / (到场时限 - 延迟)
                    float travelTime = arrivalDeadline - approachDelay;
                    if (travelTime < 10f) travelTime = 10f;
                    _boostSpeed = dist / travelTime;
                }
                else
                {
                    _boostSpeed = 0f; // 不需要加速，用正常曲速
                }

                return true;
            }

            return _isAssigned;
        }

        protected override void MakeDecision()
        {
            // ── Bajor 禁入区检查 ──
            if (_bajorSun != null)
            {
                float distToBajor = Vector3.Distance(transform.position, _bajorSun.position);
                if (distToBajor < bajorExclusionRadius)
                {
                    _fleeingBajor = true;
                    currentState = AIState.Patrol;
                    currentTarget = null;
                    CeaseFire();
                    return;
                }
                _fleeingBajor = false;
            }

            if (HealthPercent < retreatThreshold)
            {
                if (currentState != AIState.Retreat)
                {
                    currentState = AIState.Retreat;
                    _stateTimer = 0f;
                    CeaseFire();
                    if (_assignedApproacher == this)
                    {
                        _assignedApproacher = null;
                        _isAssigned = false;
                    }
                }
                return;
            }

            Transform player = null;
            float dist = float.MaxValue;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var ph = playerObj.GetComponent<ShipHealth>();
                if (ph != null && ph.IsAlive)
                {
                    dist = Vector3.Distance(transform.position, playerObj.transform.position);
                    player = playerObj.transform;
                }
            }

            if (player == null)
            {
                if (currentState != AIState.Patrol && currentState != AIState.Idle)
                {
                    currentState = AIState.Patrol;
                    _stateTimer = 0f;
                    CeaseFire();
                }
                currentTarget = null;
                return;
            }

            if (dist > playerSeekRange)
            {
                currentState = AIState.Patrol;
                currentTarget = null;
                return;
            }

            bool canApproach = CanApproachPlayer();

            if (_isAssigned && _approachDelayTimer > 0f)
            {
                currentState = AIState.Patrol;
                currentTarget = null;
                return;
            }

            if (canApproach)
            {
                if (dist < engageRange)
                {
                    if (currentState != AIState.Engage)
                    {
                        currentState = AIState.Engage;
                        _stateTimer = 0f;
                        _isWarping = false;
                        _approachTimer = 0f;
                    }
                    currentTarget = player;
                }
                else
                {
                    currentState = AIState.Patrol;
                    currentTarget = player;
                }
            }
            else
            {
                currentState = AIState.Patrol;
                currentTarget = null;
            }
        }

        protected override void ExecuteState()
        {
            switch (currentState)
            {
                case AIState.Idle:
                    Brake();
                    break;
                case AIState.Patrol:
                    ExecutePatrol();
                    break;
                case AIState.Engage:
                    ExecuteEngage();
                    break;
                case AIState.Retreat:
                    ExecuteRetreat();
                    break;
            }

            _evadeTimer -= Time.deltaTime;
            if (_evadeTimer <= 0f)
            {
                _evadeTimer = 0.5f;
                if (difficulty >= AIDifficulty.Hard)
                    CheckTorpedoEvade();
            }

            if (_isEvading)
                _controller.velocity = _evadeDir * combatMaxSpeed * 1.5f;
        }

        private void ExecutePatrol()
        {
            // Bajor 禁入区撤离
            if (_fleeingBajor && _bajorSun != null)
            {
                Vector3 away = (transform.position - _bajorSun.position).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                FaceDirection(away, approachTurnRate);
                if (Vector3.Dot(transform.forward, away) > 0.3f)
                    _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * warpSpeed, 2f * Time.deltaTime);
                else
                    _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, 2f * Time.deltaTime);
                CeaseFire();
                return;
            }

            if (currentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);

                if (dist > engageRange)
                {
                    // 决定飞行速度：加速到场 or 正常曲速
                    float effectiveSpeed = warpSpeed;
                    bool useBoost = false;

                    if (_boostSpeed > 0f)
                    {
                        // 持续更新加速速度（玩家可能在移动）
                        float remainingDist = dist;
                        float remainingTime = arrivalDeadline - _approachTimer;
                        if (remainingTime > 5f)
                        {
                            effectiveSpeed = remainingDist / remainingTime;
                            effectiveSpeed = Mathf.Max(effectiveSpeed, warpSpeed);
                        }
                        else
                        {
                            // 时间紧迫，全速
                            effectiveSpeed = _boostSpeed;
                        }
                        useBoost = true;
                    }
                    else if (_approachTimer >= 30f && !_isWarping)
                    {
                        _isWarping = true;
                    }

                    if (_isWarping || useBoost)
                    {
                        Vector3 dir = (currentTarget.position - transform.position).normalized;
                        FaceDirection(dir, approachTurnRate);
                        if (Vector3.Dot(transform.forward, dir) > 0.3f)
                            _controller.velocity = Vector3.Lerp(_controller.velocity, transform.forward * effectiveSpeed, 8f * Time.deltaTime);
                        else
                            _controller.velocity = Vector3.Lerp(_controller.velocity, Vector3.zero, 8f * Time.deltaTime);
                    }
                    else
                    {
                        MoveToward(currentTarget.position, false);
                    }
                    FireWeapons();
                    return;
                }

                // 到达交战范围 — 重置
                _isWarping = false;
                _approachTimer = 0f;
                _boostSpeed = 0f;
                Brake();
                CeaseFire();
                return;
            }

            // 无目标 — 在星球轨道低速巡航
            _isWarping = false;
            _approachTimer = 0f;
            _boostSpeed = 0f;
            OrbitAround(patrolCenter, patrolRadius, patrolSpeed);
            CeaseFire();
        }

        private void ExecuteEngage()
        {
            if (currentTarget == null)
            {
                currentState = AIState.Patrol;
                return;
            }

            Vector3 toTarget = currentTarget.position - transform.position;
            float dist = toTarget.magnitude;

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    if (dist > optimalRange * 1.2f)
                        MoveToward(currentTarget.position, false);
                    else if (dist < optimalRange * 0.8f)
                    {
                        FaceDirection(-toTarget.normalized, combatTurnRate);
                        _controller.velocity = Vector3.Lerp(_controller.velocity, -transform.forward * combatMaxSpeed, 3f * Time.deltaTime);
                    }
                    else
                    {
                        FaceDirection(toTarget.normalized, combatTurnRate);
                        Vector3 strafe = _strafeDir * combatMaxSpeed * 0.4f;
                        _controller.velocity = Vector3.Lerp(_controller.velocity, strafe, 3f * Time.deltaTime);
                    }
                    break;
                case AIDifficulty.Normal:
                    if (_stateTimer > 5f)
                    {
                        Vector3 detour = currentTarget.position + Random.onUnitSphere * 30f;
                        detour.y = currentTarget.position.y;
                        MoveToward(detour, false);
                        if (_stateTimer > 6f) _stateTimer = 0f;
                    }
                    else
                        MoveToward(currentTarget.position, true);
                    break;
                case AIDifficulty.Hard:
                    if (_stateTimer > 4f)
                    {
                        Vector3 pastTarget = currentTarget.position + toTarget.normalized * 80f;
                        MoveToward(pastTarget, false);
                        if (_stateTimer > 7f) _stateTimer = 0f;
                    }
                    else
                        MoveToward(currentTarget.position, true);
                    break;
                case AIDifficulty.Epic:
                    if (_stateTimer > 5f)
                    {
                        Vector3 reposition = currentTarget.position + Random.onUnitSphere * 60f;
                        reposition.y = currentTarget.position.y + Random.Range(-20f, 20f);
                        MoveToward(reposition, false);
                        if (_stateTimer > 8f) _stateTimer = 0f;
                    }
                    else
                        MoveToward(currentTarget.position, true);
                    break;
            }

            FireWeapons();

            if (difficulty == AIDifficulty.Epic)
                CheckTorpedoEvade();
        }

        private void ExecuteRetreat()
        {
            if (currentTarget == null)
            {
                currentState = AIState.Regroup;
                return;
            }

            Vector3 away = (transform.position - currentTarget.position).normalized;
            Vector3 retreatPos = transform.position + away * 200f;
            MoveToward(retreatPos, false);
            ApplyThrust(1f);

            CeaseFire();

            if (HealthPercent > regroupThreshold)
            {
                currentState = AIState.Engage;
                _stateTimer = 0f;
            }
        }

        private void CheckTorpedoEvade()
        {
            var torpedoes = FindObjectsOfType<TorpedoProjectile>();
            foreach (var torp in torpedoes)
            {
                if (torp.target == transform || Vector3.Distance(torp.transform.position, transform.position) < 100f)
                {
                    Vector3 toTorp = (torp.transform.position - transform.position).normalized;
                    _evadeDir = Vector3.Cross(toTorp, Vector3.up).normalized;
                    if (Random.value > 0.5f) _evadeDir = -_evadeDir;
                    _isEvading = true;
                    _evadeTimer = 1f;
                    return;
                }
            }
            _isEvading = false;
        }
    }
}
