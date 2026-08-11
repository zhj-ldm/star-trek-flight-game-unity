using UnityEngine;
using UnityEditor;

namespace StarTrekCombat
{
    [InitializeOnLoad]
    public static class BridgeViewPreview
    {
        const string kPreviewKey = "BridgeViewPreview_Active";

        static BridgeViewPreview()
        {
            if (SessionState.GetBool(kPreviewKey, false))
            {
                EditorApplication.delayCall += RestoreFromRecompile;
            }
            EditorApplication.update += OnEditorUpdate;
        }

        static void RestoreFromRecompile()
        {
            var playerShip = GameObject.FindWithTag("Player");
            if (playerShip == null) { SessionState.SetBool(kPreviewKey, false); return; }

            var bridgeModel = playerShip.transform.Find("BridgeModel");
            var shipModel = playerShip.transform.Find("ShipModel");
            var cam = Camera.main;
            if (cam != null)
            {
                var rig = cam.GetComponent<CameraRig>();
                if (rig != null) rig.enabled = true;
            }

            if (shipModel != null) shipModel.gameObject.SetActive(true);
            if (bridgeModel != null) bridgeModel.gameObject.SetActive(false);

            SessionState.SetBool(kPreviewKey, false);
            Debug.Log("[BridgePreview] 重新编译后自动恢复正常状态");
        }

        static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(kPreviewKey, false) || Application.isPlaying) return;

            var playerShip = GameObject.FindWithTag("Player");
            if (playerShip == null) return;

            var anchor = playerShip.transform.Find("CameraAnchor_Bridge");
            var cam = Camera.main;
            if (cam == null || anchor == null) return;

            cam.transform.position = anchor.position;
            cam.transform.rotation = anchor.rotation;
        }

        [MenuItem("Tools/Star Trek/预览舰桥视角 (Preview Bridge View) %l", false, 200)]
        static void TogglePreview()
        {
            var playerShip = GameObject.FindWithTag("Player");
            if (playerShip == null)
            {
                Debug.LogWarning("PlayerShip not found.");
                return;
            }

            var bridgeModel = playerShip.transform.Find("BridgeModel");
            var shipModel = playerShip.transform.Find("ShipModel");
            var cam = Camera.main;
            if (cam == null) return;

            bool previewing = SessionState.GetBool(kPreviewKey, false);

            if (!previewing)
            {
                if (shipModel != null) shipModel.gameObject.SetActive(false);
                if (bridgeModel != null) bridgeModel.gameObject.SetActive(true);

                var rig = cam.GetComponent<CameraRig>();
                if (rig != null) rig.enabled = false;

                SessionState.SetBool(kPreviewKey, true);
                Debug.Log("[BridgePreview] ON — 调整 CameraAnchor_Bridge 即可在 Game View 实时预览");
            }
            else
            {
                if (shipModel != null) shipModel.gameObject.SetActive(true);
                if (bridgeModel != null) bridgeModel.gameObject.SetActive(false);

                var rig = cam.GetComponent<CameraRig>();
                if (rig != null) rig.enabled = true;

                SessionState.SetBool(kPreviewKey, false);
                Debug.Log("[BridgePreview] OFF — 恢复第三人称视角");
            }
        }

        [MenuItem("Tools/Star Trek/预览舰桥视角 (Preview Bridge View) %l", true)]
        static bool ValidateToggle()
        {
            return !Application.isPlaying;
        }
    }
}
