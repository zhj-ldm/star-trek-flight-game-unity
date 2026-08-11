using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace StarTrekCombat.Editor
{
    /// <summary>
    /// Adds a PhaserRing GameObject with a ring mesh to the ExcelsiorModel prefab.
    /// The ring is placed at origin — user adjusts position in Prefab editor.
    /// Menu: Tools > Star Trek > Add PhaserRing to Excelsior
    /// </summary>
    public static class AddPhaserRingToExcelsior
    {
        const string PrefabPath = "Assets/Resources/ExcelsiorModel.prefab";
        const string RingMeshPath = "Assets/Models/Excelsior/phaser_ring_mesh.asset";

        [MenuItem("Tools/Star Trek/Add PhaserRing to Excelsior")]
        public static void Add()
        {
            // Load prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PhaserRing] Prefab not found: {PrefabPath}");
                return;
            }

            // Generate ring mesh matching Enterprise's PhaserRing dimensions:
            // mesh bounds extent.x=3.79, so outerRadius=3.79
            // thin ring (innerRadius=3.5), 64 segments, XY plane
            EnsureFolder("Assets/Models/Excelsior");
            var ringMesh = GenerateRingMesh(3.79f, 3.5f, 64);
            ringMesh.name = "PhaserRing";
            AssetDatabase.CreateAsset(ringMesh, RingMeshPath);
            AssetDatabase.SaveAssets();

            // Load prefab contents, add PhaserRing child
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            // Remove existing PhaserRing if any
            var existing = root.transform.Find("PhaserRing");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[PhaserRing] Removed existing PhaserRing");
            }

            // Create PhaserRing GameObject — visible in editor so user can see and move it
            var ringGo = new GameObject("PhaserRing");
            ringGo.transform.SetParent(root.transform, false);
            // Match Enterprise's scene scale
            ringGo.transform.localPosition = new Vector3(118.5f, 1.8f, -16.7f);
            ringGo.transform.localRotation = Quaternion.identity;
            ringGo.transform.localScale = new Vector3(0.6700172f, 0.79671f, 0.79671f);

            var mf = ringGo.AddComponent<MeshFilter>();
            mf.sharedMesh = ringMesh;

            var mr = ringGo.AddComponent<MeshRenderer>();
            mr.enabled = true; // Visible so user can see it in the prefab editor

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            AssetDatabase.Refresh();
            Debug.Log($"[PhaserRing] Added PhaserRing to {PrefabPath}. Open the prefab to adjust position.");
        }

        static Mesh GenerateRingMesh(float outerRadius, float innerRadius, int segments)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts.Add(new Vector3(cos * outerRadius, sin * outerRadius, 0));
                norms.Add(Vector3.back);
                uvs.Add(new Vector2((float)i / segments, 1f));

                verts.Add(new Vector3(cos * innerRadius, sin * innerRadius, 0));
                norms.Add(Vector3.back);
                uvs.Add(new Vector2((float)i / segments, 0f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = i * 2 + 2;
                int d = i * 2 + 3;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
