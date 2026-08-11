using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace StarTrekCombat.Editor
{
    /// <summary>
    /// Loads a ship model into scene with visible PhaserRing for manual adjustment.
    /// Works for GLB ships (Voyager, Defiant) and FBX/prefab ships (EnterpriseXI).
    /// 
    /// Menu: Tools > Star Trek > GLB PhaserRing > Load to Scene
    /// Menu: Tools > Star Trek > GLB PhaserRing > Save Position
    /// </summary>
    public static class GlbPhaserRingEditor
    {
        const string VoyagerObjName = "VoyagerPhaserRingEditor";
        const string DefiantObjName = "DefiantPhaserRingEditor";
        const string EnterpriseXIObjName = "EnterpriseXIPhaserRingEditor";

        static string CurrentShipName = "";
        static string CurrentGlbPath = "";
        static string CurrentObjName = "";

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Voyager > Load to Scene")]
        public static void LoadVoyager() => LoadShip("Voyager", "Assets/Models/uss_voyager.glb", VoyagerObjName);

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Voyager > Save Position")]
        public static void SaveVoyager() => SaveShip("Voyager", VoyagerObjName);

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Defiant > Load to Scene")]
        public static void LoadDefiant() => LoadShip("Defiant", "Assets/Models/defiant.glb", DefiantObjName);

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Defiant > Save Position")]
        public static void SaveDefiant() => SaveShip("Defiant", DefiantObjName);

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Enterprise XI > Load to Scene")]
        public static void LoadEnterpriseXI() => LoadPrefabShip("EnterpriseXI", "Assets/Resources/EnterpriseXIModel.prefab", EnterpriseXIObjName);

        [MenuItem("Tools/Star Trek > GLB PhaserRing > Enterprise XI > Save Position")]
        public static void SaveEnterpriseXI() => SaveShip("EnterpriseXI", EnterpriseXIObjName);

        static void LoadShip(string shipName, string glbPath, string objName)
        {
            CurrentShipName = shipName;
            CurrentGlbPath = glbPath;
            CurrentObjName = objName;

            var existing = GameObject.Find(objName);
            if (existing != null) Object.DestroyImmediate(existing);

            var data = new UniGLTF.AutoGltfFileParser(glbPath).Parse();
            var context = new UniGLTF.ImporterContext(data);
            var loaded = context.LoadAsync(new UniGLTF.ImmediateCaller()).Result;
            loaded.ShowMeshes();

            var root = loaded.Root;
            root.name = objName;
            root.transform.position = Vector3.zero;

            var ringGo = new GameObject("PhaserRing");
            ringGo.transform.SetParent(root.transform, false);
            ringGo.transform.localPosition = LoadVec3(shipName + "PhaserRingPos", Vector3.zero);
            ringGo.transform.localRotation = LoadQuat(shipName + "PhaserRingRot", Quaternion.identity);
            ringGo.transform.localScale = LoadVec3(shipName + "PhaserRingScale", Vector3.one);

            var mf = ringGo.AddComponent<MeshFilter>();
            mf.sharedMesh = GenerateRingMesh(3.79f, 3.5f, 64);

            var mr = ringGo.AddComponent<MeshRenderer>();
            mr.enabled = true;

            Selection.activeGameObject = ringGo;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(root.transform.position, Vector3.one * 10), false);
            Debug.Log($"[{shipName}] Loaded to scene. Adjust PhaserRing position/rotation/scale, then run 'Save Position'.");
        }

        /// <summary>
        /// Loads a prefab-based ship (FBX) into scene with visible PhaserRing.
        /// Used for Enterprise XI and any future FBX/prefab ships.
        /// </summary>
        static void LoadPrefabShip(string shipName, string prefabPath, string objName)
        {
            CurrentShipName = shipName;
            CurrentGlbPath = prefabPath;
            CurrentObjName = objName;

            var existing = GameObject.Find(objName);
            if (existing != null) Object.DestroyImmediate(existing);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Not Found", "Prefab not found: " + prefabPath, "OK");
                return;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.name = objName;
            root.transform.position = Vector3.zero;

            // Check if prefab already has a PhaserRing (saved into prefab asset)
            var existingRing = root.transform.Find("PhaserRing");
            GameObject ringGo;
            if (existingRing != null)
            {
                ringGo = existingRing.gameObject;
                var mfExist = ringGo.GetComponent<MeshFilter>();
                if (mfExist == null) mfExist = ringGo.AddComponent<MeshFilter>();
                mfExist.sharedMesh = GenerateRingMesh(3.79f, 3.5f, 64);
                var mrExist = ringGo.GetComponent<MeshRenderer>();
                if (mrExist == null) mrExist = ringGo.AddComponent<MeshRenderer>();
                mrExist.enabled = true;
            }
            else
            {
                ringGo = new GameObject("PhaserRing");
                ringGo.transform.SetParent(root.transform, false);
                ringGo.transform.localPosition = LoadVec3(shipName + "PhaserRingPos", Vector3.zero);
                ringGo.transform.localRotation = LoadQuat(shipName + "PhaserRingRot", Quaternion.identity);
                ringGo.transform.localScale = LoadVec3(shipName + "PhaserRingScale", Vector3.one);

                var mf = ringGo.AddComponent<MeshFilter>();
                mf.sharedMesh = GenerateRingMesh(3.79f, 3.5f, 64);

                var mr = ringGo.AddComponent<MeshRenderer>();
                mr.enabled = true;
            }

            Selection.activeGameObject = ringGo;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(root.transform.position, Vector3.one * 10), false);
            Debug.Log($"[{shipName}] Loaded prefab to scene. Adjust PhaserRing position/rotation/scale, then run 'Save Position'.");
        }

        static void SaveShip(string shipName, string objName)
        {
            var ship = GameObject.Find(objName);
            if (ship == null)
            {
                EditorUtility.DisplayDialog("Not Found", $"Run '{shipName} > Load to Scene' first.", "OK");
                return;
            }

            var ring = ship.transform.Find("PhaserRing");
            if (ring == null)
            {
                EditorUtility.DisplayDialog("Not Found", "PhaserRing not found.", "OK");
                return;
            }

            SaveVec3(shipName + "PhaserRingPos", ring.localPosition);
            SaveQuat(shipName + "PhaserRingRot", ring.localRotation);
            SaveVec3(shipName + "PhaserRingScale", ring.localScale);
            PlayerPrefs.Save();

            // Also save PhaserRing into the prefab asset (so it carries to builds)
            if (shipName == "EnterpriseXI")
            {
                var prefabPath = "Assets/Resources/EnterpriseXIModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var prefabRing = prefab.transform.Find("PhaserRing");
                    if (prefabRing == null)
                    {
                        // Add PhaserRing to prefab via instance + ApplyAddedGameObject
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        var newRing = new GameObject("PhaserRing");
                        newRing.transform.SetParent(inst.transform, false);
                        newRing.transform.localPosition = ring.localPosition;
                        newRing.transform.localRotation = ring.localRotation;
                        newRing.transform.localScale = ring.localScale;
                        PrefabUtility.ApplyAddedGameObject(newRing, prefabPath, InteractionMode.AutomatedAction);
                        Object.DestroyImmediate(inst);
                    }
                    else
                    {
                        // Update existing PhaserRing via instance + ApplyPrefabInstance
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        var instRing = inst.transform.Find("PhaserRing");
                        instRing.localPosition = ring.localPosition;
                        instRing.localRotation = ring.localRotation;
                        instRing.localScale = ring.localScale;
                        PrefabUtility.ApplyPrefabInstance(inst, InteractionMode.AutomatedAction);
                        Object.DestroyImmediate(inst);
                    }
                    Debug.Log($"[{shipName}] PhaserRing saved to prefab: {prefabPath}");
                }
            }

            Debug.Log($"[{shipName}] PhaserRing saved: pos={ring.localPosition}, rot={ring.localRotation.eulerAngles}, scale={ring.localScale}");
            EditorUtility.DisplayDialog("Saved",
                $"{shipName} PhaserRing saved!\nPos: {ring.localPosition}\nRot: {ring.localRotation.eulerAngles}\nScale: {ring.localScale}", "OK");
        }

        static Vector3 LoadVec3(string key, Vector3 def)
        {
            return new Vector3(
                PlayerPrefs.GetFloat(key + "_x", def.x),
                PlayerPrefs.GetFloat(key + "_y", def.y),
                PlayerPrefs.GetFloat(key + "_z", def.z));
        }

        static Quaternion LoadQuat(string key, Quaternion def)
        {
            return new Quaternion(
                PlayerPrefs.GetFloat(key + "_x", def.x),
                PlayerPrefs.GetFloat(key + "_y", def.y),
                PlayerPrefs.GetFloat(key + "_z", def.z),
                PlayerPrefs.GetFloat(key + "_w", def.w));
        }

        static void SaveVec3(string key, Vector3 val)
        {
            PlayerPrefs.SetFloat(key + "_x", val.x);
            PlayerPrefs.SetFloat(key + "_y", val.y);
            PlayerPrefs.SetFloat(key + "_z", val.z);
        }

        static void SaveQuat(string key, Quaternion val)
        {
            PlayerPrefs.SetFloat(key + "_x", val.x);
            PlayerPrefs.SetFloat(key + "_y", val.y);
            PlayerPrefs.SetFloat(key + "_z", val.z);
            PlayerPrefs.SetFloat(key + "_w", val.w);
        }

        static Mesh GenerateRingMesh(float outerRadius, float innerRadius, int segments)
        {
            float majorR = (outerRadius + innerRadius) * 0.5f;
            float minorR = (outerRadius - innerRadius) * 0.5f;
            int ringSeg = segments;
            int tubeSeg = 12;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();

            for (int i = 0; i <= ringSeg; i++)
            {
                float a = (float)i / ringSeg * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int j = 0; j <= tubeSeg; j++)
                {
                    float b = (float)j / tubeSeg * Mathf.PI * 2f;
                    float cb = Mathf.Cos(b), sb = Mathf.Sin(b);
                    verts.Add(new Vector3((majorR + minorR * cb) * ca, (majorR + minorR * cb) * sa, minorR * sb));
                    norms.Add(new Vector3(cb * ca, cb * sa, sb));
                    uvs.Add(new Vector2((float)i / ringSeg, (float)j / tubeSeg));
                }
            }
            for (int i = 0; i < ringSeg; i++)
            {
                for (int j = 0; j < tubeSeg; j++)
                {
                    int a = i * (tubeSeg + 1) + j;
                    int b = a + 1;
                    int c = a + (tubeSeg + 1);
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            mesh.name = "PhaserRing";
            return mesh;
        }
    }
}
