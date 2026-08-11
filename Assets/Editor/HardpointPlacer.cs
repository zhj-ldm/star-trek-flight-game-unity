using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace StarTrekCombat
{
#if UNITY_EDITOR
    /// <summary>
    /// Scene view tool: Alt+Click on ship model surface to place selected hardpoint Transform.
    /// Shift+Alt+Click = also align rotation to surface normal.
    /// </summary>
    [InitializeOnLoad]
    public static class HardpointPlacerGUI
    {
        static HardpointPlacerGUI()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Debug.Log("[HardpointPlacer] Active. Select a hardpoint Transform, then Alt+Click on model surface.");
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (Application.isPlaying) return;

            Event e = Event.current;

            // Alt + left click
            if (e.type == EventType.MouseDown && e.button == 0 && e.alt)
            {
                var selected = Selection.activeTransform;
                if (selected == null) return;

                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                // Find PlayerShip
                GameObject playerShip = null;
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name.Contains("PlayerShip"))
                    {
                        playerShip = root;
                        break;
                    }
                }
                if (playerShip == null) return;

                var shipModel = playerShip.transform.Find("ShipModel");
                if (shipModel == null) return;

                // Raycast against all mesh renderers via Physics
                var colliders = shipModel.GetComponentsInChildren<Collider>();
                RaycastHit closestHit = default;
                float closestDist = float.MaxValue;
                bool found = false;

                foreach (var col in colliders)
                {
                    RaycastHit hitInfo;
                    if (col.Raycast(ray, out hitInfo, 10000f))
                    {
                        if (hitInfo.distance < closestDist)
                        {
                            closestDist = hitInfo.distance;
                            closestHit = hitInfo;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    Undo.RecordObject(selected, "Place Hardpoint");
                    selected.position = closestHit.point;
                    if (e.shift)
                        selected.rotation = Quaternion.LookRotation(closestHit.normal);
                    EditorUtility.SetDirty(selected);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    Debug.Log($"[HardpointPlacer] Placed '{selected.name}' at {closestHit.point}");
                }
            }
        }
    }
#endif
}
