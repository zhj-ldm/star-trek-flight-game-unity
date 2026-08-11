using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// 精确量化抖动 — 每帧记录位置/旋转的帧间变化量。
    /// 挂在 PlayerShip 上，运行后看 Inspector 的数值或 Console 日志。
    /// 
    /// 指标：
    ///   posDelta — 帧间位置变化量(m)，正常应等于 velocity*dt，异常时会有额外噪声
    ///   rotDelta — 帧间旋转角度变化(°)，正常应等于 angVel*dt，异常时会有额外噪声
    ///   posJitter — 位置噪声 = 实际delta - 预期delta 的绝对值
    ///   rotJitter — 旋转噪声 = 实际rotDelta - 预期rotDelta 的绝对值
    ///   camPosDelta — 摄像机帧间位置变化
    ///   shipModelLocalPos — ShipModel的localPosition（应恒为0，如果变化说明有东西在动它）
    /// </summary>
    public class JitterMeter : MonoBehaviour
    {
        [Header("实时数值 (read-only)")]
        public float posDelta;           // 帧间位置变化(m)
        public float expectedPosDelta;   // 预期位置变化 = speed * dt
        public float posJitter;          // 位置抖动 = |实际 - 预期|
        public float rotDelta;           // 帧间旋转变化(°)
        public float expectedRotDelta;   // 预期旋转变化 = angVel * dt
        public float rotJitter;          // 旋转抖动 = |实际 - 预期|
        public float camPosDelta;        // 摄像机帧间位置变化
        public float camRotDelta;        // 摄像机帧间旋转变化
        public Vector3 shipModelLocalPos; // ShipModel.localPosition（应恒定）
        public Vector3 shipModelLocalRot; // ShipModel.localEulerAngles（应恒定）
        public float distFromOrigin;     // 距离原点距离 — 浮点精度参考
        
        [Header("最大记录值")]
        public float maxPosJitter;
        public float maxRotJitter;
        public float maxCamJitter;
        
        [Header("设置")]
        public bool logToConsole = true;
        public int logInterval = 10;
        
        private Vector3 _prevPos;
        private Quaternion _prevRot;
        private Vector3 _prevCamPos;
        private Quaternion _prevCamRot;
        private Camera _cam;
        private ShipController _ctrl;
        private Transform _shipModel;
        private int _frameCount;

        void Start()
        {
            _ctrl = GetComponent<ShipController>();
            _cam = Camera.main;
            _shipModel = transform.Find("ShipModel");
            _prevPos = transform.position;
            _prevRot = transform.rotation;
            if (_cam != null)
            {
                _prevCamPos = _cam.transform.position;
                _prevCamRot = _cam.transform.rotation;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            
            // 当前帧值
            Vector3 curPos = transform.position;
            Quaternion curRot = transform.rotation;
            
            // 位置变化
            posDelta = Vector3.Distance(curPos, _prevPos);
            expectedPosDelta = _ctrl != null ? _ctrl.currentSpeed * dt : 0f;
            posJitter = Mathf.Abs(posDelta - expectedPosDelta);
            
            // 旋转变化
            rotDelta = Quaternion.Angle(curRot, _prevRot);
            expectedRotDelta = _ctrl != null ? _ctrl.angularVelocity.magnitude * Mathf.Rad2Deg * dt : 0f;
            rotJitter = Mathf.Abs(rotDelta - expectedRotDelta);
            
            // 摄像机变化
            if (_cam != null)
            {
                Vector3 curCamPos = _cam.transform.position;
                Quaternion curCamRot = _cam.transform.rotation;
                camPosDelta = Vector3.Distance(curCamPos, _prevCamPos);
                camRotDelta = Quaternion.Angle(curCamRot, _prevCamRot);
                
                // 摄像机抖动 = 摄像机位置变化 - 预期跟随变化
                // 预期: 摄像机应跟随飞船，delta应与飞船delta一致
                float expectedCamDelta = posDelta; // 简化: 应该一样
                float camJitter = Mathf.Abs(camPosDelta - expectedCamDelta);
                if (camJitter > maxCamJitter) maxCamJitter = camJitter;
                
                _prevCamPos = curCamPos;
                _prevCamRot = curCamRot;
            }
            
            // ShipModel 本地值（应恒定）
            if (_shipModel != null)
            {
                shipModelLocalPos = _shipModel.localPosition;
                shipModelLocalRot = _shipModel.localEulerAngles;
            }
            
            // 距离原点
            distFromOrigin = curPos.magnitude;
            
            // 最大值记录
            if (posJitter > maxPosJitter) maxPosJitter = posJitter;
            if (rotJitter > maxRotJitter) maxRotJitter = rotJitter;
            
            // 更新上一帧
            _prevPos = curPos;
            _prevRot = curRot;
            
            // 日志
            if (logToConsole)
            {
                _frameCount++;
                if (_frameCount >= logInterval)
                {
                    _frameCount = 0;
                    Debug.Log($"[JitterMeter] posJitter={posJitter:F6}m | rotJitter={rotJitter:F6}° | camDelta={camPosDelta:F4}m camRot={camRotDelta:F4}° | dist={distFromOrigin:F0}m | posDelta={posDelta:F4} exp={expectedPosDelta:F4} | rotDelta={rotDelta:F4} exp={expectedRotDelta:F4} | modelLocalPos={shipModelLocalPos}");
                }
            }
        }
    }
}
