using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

namespace StarTrekCombat.Editor
{
    /// <summary>
    /// Creates the StartScene with Camera + ShipSelectionUI and adds it to Build Settings.
    /// Menu: Tools > Star Trek > Create Start Scene
    /// </summary>
    public static class CreateStartScene
    {
        const string ScenePath = "Assets/Scenes/StartScene.scene";

        [MenuItem("Tools/Star Trek/Create Start Scene")]
        public static void Create()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Add Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;

            // Add ShipSelectionUI
            var uiGo = new GameObject("ShipSelectionUI");
            uiGo.AddComponent<ShipSelectionUI>();

            // Save scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CreateStartScene] Created StartScene at {ScenePath}");

            // Add to Build Settings (as scene 0, before BattleScene)
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath)
                {
                    s.enabled = true;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[CreateStartScene] Added StartScene to Build Settings as scene 0");
            }

            AssetDatabase.Refresh();
        }
    }
}
