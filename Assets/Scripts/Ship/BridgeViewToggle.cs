using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// K键切换舰桥室内视角 / 第三人称外部视角。
    /// 舰桥模型和摄像机锚点可手动在Inspector调整。
    /// </summary>
    public class BridgeViewToggle : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("舰桥室内模型 (bridge.glb)")]
        public GameObject bridgeModel;

        [Tooltip("摄像机系统")]
        public CameraRig cameraRig;

        private bool _bridgeView = false;

        void Start()
        {
            if (cameraRig == null)
                cameraRig = FindObjectOfType<CameraRig>();
            if (bridgeModel != null)
                bridgeModel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                // 只在摄像机看本船时生效
                if (cameraRig == null || cameraRig.target == transform)
                {
                    _bridgeView = !_bridgeView;
                    ToggleBridgeView();
                }
            }
        }

        void ToggleBridgeView()
        {
            // 动态查找 ShipModel (可能在运行时被 ShipModelSwapper 替换)
            var shipModel = transform.Find("ShipModel");

            if (_bridgeView)
            {
                if (shipModel != null) shipModel.gameObject.SetActive(false);
                if (bridgeModel != null) bridgeModel.SetActive(true);
                if (cameraRig != null) cameraRig.SetMode(CameraMode.Bridge);
            }
            else
            {
                if (shipModel != null) shipModel.gameObject.SetActive(true);
                if (bridgeModel != null) bridgeModel.SetActive(false);
                if (cameraRig != null) cameraRig.SetMode(CameraMode.ThirdPerson);
            }
        }
    }
}
