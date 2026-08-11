using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace StarTrekCombat
{
#if UNITY_EDITOR
    /// <summary>
    /// Rearranges planets into a star system layout and adds a central sun.
    /// Menu: Star Trek Combat > Rearrange Galaxy
    /// </summary>
    public static class GalaxyRearranger
    {
        private struct PlanetOrbit
        {
            public string name;
            public float radius;
            public float angleDeg;
            public float yOffset;
        }

        [MenuItem("Star Trek Combat/Rearrange Galaxy")]
        public static void Rearrange()
        {
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "BattleScene")
            {
                if (!EditorUtility.DisplayDialog("Confirm",
                    "Current scene is '" + scene.name + "', not BattleScene. Continue anyway?",
                    "Yes", "No"))
                    return;
            }

            // Planet orbital data — sorted by size (smallest innermost),
            // distributed at 36° intervals around the star in the XZ plane.
            var orbits = new PlanetOrbit[]
            {
                new PlanetOrbit { name = "Planet_Moon",    radius = 10000f, angleDeg =   0f, yOffset =   100f },
                new PlanetOrbit { name = "Planet_Crystal", radius = 14000f, angleDeg =  36f, yOffset =  -200f },
                new PlanetOrbit { name = "Planet_Desert",  radius = 18000f, angleDeg =  72f, yOffset =   150f },
                new PlanetOrbit { name = "Planet_Frozen",  radius = 22000f, angleDeg = 108f, yOffset =  -100f },
                new PlanetOrbit { name = "Planet_Lava",    radius = 26000f, angleDeg = 144f, yOffset =   200f },
                new PlanetOrbit { name = "Planet_Rock",    radius = 30000f, angleDeg = 180f, yOffset =     0f },
                new PlanetOrbit { name = "Planet_Forest",  radius = 35000f, angleDeg = 216f, yOffset =  -150f },
                new PlanetOrbit { name = "Planet_Ice",     radius = 40000f, angleDeg = 252f, yOffset =   100f },
                new PlanetOrbit { name = "Planet_Ringed",  radius = 46000f, angleDeg = 288f, yOffset =  -200f },
                new PlanetOrbit { name = "Planet_Gas",     radius = 52000f, angleDeg = 324f, yOffset =   300f },
            };

            // Move each planet to its orbital position
            int moved = 0;
            foreach (var orbit in orbits)
            {
                var go = GameObject.Find(orbit.name);
                if (go == null)
                {
                    Debug.LogWarning("[GalaxyRearranger] Planet not found: " + orbit.name);
                    continue;
                }

                Undo.RecordObject(go.transform, "Move " + orbit.name);

                float rad = orbit.angleDeg * Mathf.Deg2Rad;
                float x = orbit.radius * Mathf.Cos(rad);
                float z = orbit.radius * Mathf.Sin(rad);

                go.transform.position = new Vector3(x, orbit.yOffset, z);
                moved++;
            }

            Debug.Log("[GalaxyRearranger] Moved " + moved + " planets to orbital positions.");

            CreateSun();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GalaxyRearranger] Scene saved.");
        }

        private static void CreateSun()
        {
            // Remove existing sun if present
            var existing = GameObject.Find("Sun");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // Load sun texture
            Texture2D sunTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Sun.png");
            if (sunTex == null)
            {
                AssetDatabase.ImportAsset("Assets/Textures/Sun.png", ImportAssetOptions.ForceUpdate);
                sunTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Sun.png");
            }

            // Create or load sun material
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sun.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.name = "Sun";
                AssetDatabase.CreateAsset(mat, "Assets/Materials/Sun.mat");
            }

            mat.SetColor("_Color", new Color(1f, 0.6f, 0.1f, 1f));
            mat.SetTexture("_MainTex", sunTex);
            mat.SetFloat("_Glossiness", 0f);
            mat.SetFloat("_Metallic", 0f);

            // Slight emission — visible glow but not overwhelming
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.4f, 0.0f, 1f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            EditorUtility.SetDirty(mat);

            // Create sun GameObject using primitive sphere (ensures correct mesh)
            var sunGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunGo.name = "Sun";
            Undo.RegisterCreatedObjectUndo(sunGo, "Create Sun");

            sunGo.transform.position = Vector3.zero;
            sunGo.transform.localScale = Vector3.one * 5000f;

            var mr = sunGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = false;

            // Weak point light — orange tint, no shadows
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.2f, 1f);
            light.intensity = 0.8f;
            light.range = 80000f;
            light.shadows = LightShadows.None;

            // Parent to SpaceEnvironment if it exists
            var env = GameObject.Find("SpaceEnvironment");
            if (env != null)
            {
                sunGo.transform.SetParent(env.transform, true);
            }

            Debug.Log("[GalaxyRearranger] Sun created at origin (scale 5000, weak orange point light).");
        }
    }
#endif
}
