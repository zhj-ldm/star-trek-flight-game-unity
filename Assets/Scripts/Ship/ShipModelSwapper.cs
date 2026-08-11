using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using UniGLTF;

namespace StarTrekCombat
{
    /// <summary>
    /// Auto-injected via RuntimeInitializeOnLoadMethod. Swaps the PlayerShip's
    /// ShipModel child based on PlayerPrefs "SelectedShip".
    /// </summary>
    public static class ShipModelSwapper
    {
        // PhaserRing mesh — torus with thickness, same outer radius as Enterprise
        static Mesh _phaserRingMesh;
        static Mesh GetPhaserRingMesh()
        {
            if (_phaserRingMesh != null) return _phaserRingMesh;
            float majorR = 3.645f;   // center-to-tube-center (avg of 3.79 outer, 3.5 inner)
            float minorR = 0.145f;   // tube radius (half of ring width 0.29)
            int ringSeg = 64;        // segments around the ring
            int tubeSeg = 12;        // segments around the tube cross-section
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();

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
            _phaserRingMesh = new Mesh();
            _phaserRingMesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _phaserRingMesh.SetVertices(verts);
            _phaserRingMesh.SetTriangles(tris, 0);
            _phaserRingMesh.SetNormals(norms);
            _phaserRingMesh.SetUVs(0, uvs);
            _phaserRingMesh.RecalculateBounds();
            _phaserRingMesh.name = "PhaserRing";
            return _phaserRingMesh;
        }

        static GameObject LoadGlb(string path)
        {
            var data = new AutoGltfFileParser(path).Parse();
            var context = new ImporterContext(data);
            var loaded = context.LoadAsync(new ImmediateCaller()).Result;
            loaded.ShowMeshes();
            return loaded.Root;
        }

        static void AddPhaserRing(GameObject model, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var ringGo = new GameObject("PhaserRing");
            ringGo.transform.SetParent(model.transform, false);
            ringGo.transform.localPosition = localPos;
            ringGo.transform.localRotation = localRot;
            ringGo.transform.localScale = localScale;
            var mf = ringGo.AddComponent<MeshFilter>();
            mf.sharedMesh = GetPhaserRingMesh();
            var mr = ringGo.AddComponent<MeshRenderer>();
            mr.enabled = false;  // PhaserWeapon.Start() will hide ring mesh anyway
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnLoad()
        {
            // Handle current scene (e.g. playing directly from BattleScene in editor)
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            // Register for future scene loads (StartScene → BattleScene navigation)
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "BattleScene") return;

            string selectedShip = PlayerPrefs.GetString("SelectedShip", "Enterprise");
            Debug.Log($"[ShipModelSwapper] SelectedShip = {selectedShip}");

            if (selectedShip == "Enterprise") return;

            // Find PlayerShip
            GameObject playerShip = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.Contains("PlayerShip") || root.CompareTag("Player"))
                {
                    playerShip = root;
                    break;
                }
            }
            if (playerShip == null)
            {
                Debug.LogWarning("[ShipModelSwapper] PlayerShip not found!");
                return;
            }

            var shipModel = playerShip.transform.Find("ShipModel");
            if (shipModel == null)
            {
                Debug.LogWarning("[ShipModelSwapper] ShipModel child not found!");
                return;
            }

            if (selectedShip == "Excelsior")
            {
                SwapToExcelsior(playerShip, shipModel);
            }
            else if (selectedShip == "Voyager")
            {
                SwapToGlbShip(playerShip, shipModel, "Voyager", "uss_voyager.glb");
            }
            else if (selectedShip == "Defiant")
            {
                SwapToGlbShip(playerShip, shipModel, "Defiant", "defiant.glb");
            }
            else if (selectedShip == "EnterpriseXI")
            {
                SwapToFbxShip(playerShip, shipModel, "EnterpriseXI");
            }
        }

        static void SwapToExcelsior(GameObject playerShip, Transform shipModel)
        {
            // Load Excelsior prefab from Resources
            var prefab = Resources.Load<GameObject>("ExcelsiorModel");
            if (prefab == null)
            {
                Debug.LogError("[ShipModelSwapper] ExcelsiorModel prefab not found in Resources! Run Tools > Star Trek > Import Excelsior GLB first.");
                return;
            }

            // Destroy all existing children of ShipModel (Enterprise meshes)
            // Deactivate first so GetComponentsInChildren (activeOnly) skips them
            int destroyed = 0;
            for (int i = shipModel.childCount - 1; i >= 0; i--)
            {
                var child = shipModel.GetChild(i);
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
                destroyed++;
            }
            Debug.Log($"[ShipModelSwapper] Destroyed {destroyed} Enterprise mesh children");

            // Instantiate Excelsior model as child of ShipModel
            var excelsior = Object.Instantiate(prefab, shipModel, false);
            excelsior.name = "ExcelsiorModel";
            excelsior.transform.localPosition = Vector3.zero;
            excelsior.transform.localRotation = Quaternion.identity;
            excelsior.transform.localScale = Vector3.one;
            Debug.Log("[ShipModelSwapper] Instantiated Excelsior model under ShipModel");

            // Move PhaserRing from ExcelsiorModel to WeaponHardpoints
            // (PhaserWeapon looks for it at WeaponHardpoints/PhaserRing)
            var weaponHardpoints = playerShip.transform.Find("WeaponHardpoints");
            if (weaponHardpoints != null)
            {
                // Deactivate + rename Enterprise's PhaserRing so Find("PhaserRing") won't find it
                // (Object.Destroy is deferred — PhaserWeapon.Start() would find the old one first)
                var oldRing = weaponHardpoints.Find("PhaserRing");
                if (oldRing != null)
                {
                    oldRing.name = "PhaserRing_Enterprise_DISABLED";
                    oldRing.gameObject.SetActive(false);
                }

                // Find PhaserRing inside Excelsior prefab
                var newRing = excelsior.transform.Find("PhaserRing");
                if (newRing != null)
                {
                    // Reparent to WeaponHardpoints, preserve WORLD position
                    // (user adjusted position relative to ship model, must keep that world position)
                    newRing.SetParent(weaponHardpoints, true);
                    Debug.Log("[ShipModelSwapper] Moved PhaserRing to WeaponHardpoints (world position preserved)");
                }
            }

            Debug.Log("[ShipModelSwapper] Excelsior swap complete. ShipController.Start() will auto-fit collider.");
        }

        static void SwapToGlbShip(GameObject playerShip, Transform shipModel, string shipName, string glbFileName)
        {
            string prefix = shipName + "PhaserRing";
            string glbPath = Path.Combine(Application.streamingAssetsPath, "Models/" + glbFileName);
            if (!File.Exists(glbPath))
            {
                Debug.LogError($"[ShipModelSwapper] {shipName} GLB not found: {glbPath}");
                return;
            }

            int destroyed = 0;
            for (int i = shipModel.childCount - 1; i >= 0; i--)
            {
                var child = shipModel.GetChild(i);
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
                destroyed++;
            }
            Debug.Log($"[ShipModelSwapper] Destroyed {destroyed} previous mesh children");

            var model = LoadGlb(glbPath);
            model.name = shipName + "Model";
            model.transform.SetParent(shipModel, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            Debug.Log($"[ShipModelSwapper] Loaded {shipName} GLB via UniGLTF");

            // Hardcoded defaults from editor-adjusted positions (PlayerPrefs won't carry to builds)
            Vector3 defPos = Vector3.zero;
            Quaternion defRot = Quaternion.identity;
            Vector3 defScale = Vector3.one;
            if (shipName == "Voyager")
            {
                defPos = new Vector3(0f, -0.1f, 1.5f);
                defRot = new Quaternion(0.7071068f, 0f, 0f, 0.7071068f);
                defScale = new Vector3(0.3532383f, 0.7357566f, 0.3811992f);
            }
            else if (shipName == "Defiant")
            {
                defPos = new Vector3(0f, 0.7f, -0.3f);
                defRot = new Quaternion(0.7071068f, 0f, 0f, 0.7071068f);
                defScale = new Vector3(0.2360004f, 0.2360004f, 0.2360004f);
            }

            var ringPos = new Vector3(
                PlayerPrefs.GetFloat(prefix + "Pos_x", defPos.x),
                PlayerPrefs.GetFloat(prefix + "Pos_y", defPos.y),
                PlayerPrefs.GetFloat(prefix + "Pos_z", defPos.z));
            var ringRot = new Quaternion(
                PlayerPrefs.GetFloat(prefix + "Rot_x", defRot.x),
                PlayerPrefs.GetFloat(prefix + "Rot_y", defRot.y),
                PlayerPrefs.GetFloat(prefix + "Rot_z", defRot.z),
                PlayerPrefs.GetFloat(prefix + "Rot_w", defRot.w));
            var ringScale = new Vector3(
                PlayerPrefs.GetFloat(prefix + "Scale_x", defScale.x),
                PlayerPrefs.GetFloat(prefix + "Scale_y", defScale.y),
                PlayerPrefs.GetFloat(prefix + "Scale_z", defScale.z));
            AddPhaserRing(model, ringPos, ringRot, ringScale);

            var weaponHardpoints = playerShip.transform.Find("WeaponHardpoints");
            if (weaponHardpoints != null)
            {
                var oldRing = weaponHardpoints.Find("PhaserRing");
                if (oldRing != null)
                {
                    oldRing.name = "PhaserRing_Enterprise_DISABLED";
                    oldRing.gameObject.SetActive(false);
                }
            }

            Debug.Log($"[ShipModelSwapper] PhaserRing at pos={ringPos}, rot={ringRot.eulerAngles}, scale={ringScale} (under {shipName}Model)");
            Debug.Log($"[ShipModelSwapper] {shipName} swap complete.");
        }

        /// <summary>
        /// Swaps ShipModel to an FBX/prefab-based ship (loaded from Resources).
        /// Used for Enterprise XI. Same PhaserRing workflow as GLB ships.
        /// </summary>
        static void SwapToFbxShip(GameObject playerShip, Transform shipModel, string shipName)
        {
            string prefix = shipName + "PhaserRing";
            string prefabName = shipName + "Model";

            var prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null)
            {
                Debug.LogError("[ShipModelSwapper] " + prefabName + " prefab not found in Resources!");
                return;
            }

            int destroyed = 0;
            for (int i = shipModel.childCount - 1; i >= 0; i--)
            {
                var child = shipModel.GetChild(i);
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
                destroyed++;
            }
            Debug.Log("[ShipModelSwapper] Destroyed " + destroyed + " previous mesh children");

            var model = Object.Instantiate(prefab, shipModel, false);
            model.name = shipName + "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Compensate for ShipModel parent scale (e.g. 1.21 for Enterprise)
            // so the model appears at its prefab-native size
            var smScale = shipModel.localScale;
            model.transform.localScale = new Vector3(1f / smScale.x, 1f / smScale.y, 1f / smScale.z);
            Debug.Log("[ShipModelSwapper] Instantiated " + shipName + " prefab under ShipModel (scale compensated: " + model.transform.localScale + ")");

            // PhaserRing position from PlayerPrefs (adjusted via editor tool)
            // Hardcoded defaults from editor-adjusted positions (PlayerPrefs won't carry to builds)
            Vector3 defPos = new Vector3(0f, -0.52f, -6.21f);
            Quaternion defRot = new Quaternion(0.7071068f, 0f, 0f, 0.7071068f);
            Vector3 defScale = new Vector3(1.409456f, 1.409456f, 1.409456f);
            var ringPos = new Vector3(
                PlayerPrefs.GetFloat(prefix + "Pos_x", defPos.x),
                PlayerPrefs.GetFloat(prefix + "Pos_y", defPos.y),
                PlayerPrefs.GetFloat(prefix + "Pos_z", defPos.z));
            var ringRot = new Quaternion(
                PlayerPrefs.GetFloat(prefix + "Rot_x", defRot.x),
                PlayerPrefs.GetFloat(prefix + "Rot_y", defRot.y),
                PlayerPrefs.GetFloat(prefix + "Rot_z", defRot.z),
                PlayerPrefs.GetFloat(prefix + "Rot_w", defRot.w));
            var ringScale = new Vector3(
                PlayerPrefs.GetFloat(prefix + "Scale_x", defScale.x),
                PlayerPrefs.GetFloat(prefix + "Scale_y", defScale.y),
                PlayerPrefs.GetFloat(prefix + "Scale_z", defScale.z));
            // Check if prefab already has a PhaserRing (saved by editor tool into the prefab asset)
            var existingRing = model.transform.Find("PhaserRing");
            if (existingRing != null)
            {
                // PhaserRing already in prefab — just add mesh components
                var mf = existingRing.gameObject.GetComponent<MeshFilter>();
                if (mf == null)
                {
                    mf = existingRing.gameObject.AddComponent<MeshFilter>();
                    mf.sharedMesh = GetPhaserRingMesh();
                }
                var mr = existingRing.gameObject.GetComponent<MeshRenderer>();
                if (mr == null)
                    mr = existingRing.gameObject.AddComponent<MeshRenderer>();
                mr.enabled = false;
            }
            else
            {
                AddPhaserRing(model, ringPos, ringRot, ringScale);
            }

            // Disable Enterprise's PhaserRing in WeaponHardpoints
            var weaponHardpoints = playerShip.transform.Find("WeaponHardpoints");
            if (weaponHardpoints != null)
            {
                var oldRing = weaponHardpoints.Find("PhaserRing");
                if (oldRing != null)
                {
                    oldRing.name = "PhaserRing_Enterprise_DISABLED";
                    oldRing.gameObject.SetActive(false);
                }
            }

            Debug.Log("[ShipModelSwapper] PhaserRing at pos=" + ringPos + ", rot=" + ringRot.eulerAngles + ", scale=" + ringScale);
            Debug.Log("[ShipModelSwapper] " + shipName + " swap complete.");
        }
    }
}
