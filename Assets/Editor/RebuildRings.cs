using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class RebuildRings
{
    public static void Execute()
    {
        // 1. Create vertex color shader
        string shaderCode = @"
Shader ""Unlit/VertexColor"" {
    SubShader {
        Tags { ""RenderType""=""Opaque"" }
        LOD 100
        Pass {
            Tags { ""LightMode""=""Always"" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; };
            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}";
        if (!AssetDatabase.IsValidFolder("Assets/Shaders"))
            AssetDatabase.CreateFolder("Assets", "Shaders");
        File.WriteAllText("Assets/Shaders/UnlitVertexColor.shader", shaderCode);
        AssetDatabase.ImportAsset("Assets/Shaders/UnlitVertexColor.shader");
        var vcShader = Shader.Find("Unlit/VertexColor");

        // 2. Build unit sphere
        int rockCount = 600;
        int latCount = 3, lonCount = 6;
        var sphereVerts = new List<Vector3>();
        var sphereTris = new List<int>();
        for (int lat = 0; lat <= latCount; lat++)
        {
            float theta = (float)lat / latCount * Mathf.PI;
            for (int lon = 0; lon <= lonCount; lon++)
            {
                float phi = (float)lon / lonCount * Mathf.PI * 2f;
                sphereVerts.Add(new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta)*Mathf.Sin(phi)));
            }
        }
        for (int lat = 0; lat < latCount; lat++)
        {
            for (int lon = 0; lon < lonCount; lon++)
            {
                int a = lat*(lonCount+1)+lon, b=a+1, c=a+lonCount+1, d=c+1;
                sphereTris.Add(a); sphereTris.Add(c); sphereTris.Add(b);
                sphereTris.Add(b); sphereTris.Add(c); sphereTris.Add(d);
            }
        }
        int sv = sphereVerts.Count, st = sphereTris.Count;

        // 3. Per-ring color palettes
        var ringPalettes = new Dictionary<string, Color[]>
        {
            {"Cardassia3", new Color[] {
                new Color(0.55f,0.35f,0.15f), new Color(0.40f,0.25f,0.10f),
                new Color(0.65f,0.45f,0.20f), new Color(0.30f,0.20f,0.08f),
                new Color(0.50f,0.50f,0.45f) }},
            {"Cardassia6", new Color[] {
                new Color(0.25f,0.50f,0.60f), new Color(0.15f,0.35f,0.50f),
                new Color(0.35f,0.60f,0.70f), new Color(0.20f,0.40f,0.55f),
                new Color(0.45f,0.50f,0.55f) }},
            {"Cardassia7", new Color[] {
                new Color(0.55f,0.45f,0.18f), new Color(0.45f,0.35f,0.12f),
                new Color(0.60f,0.50f,0.25f), new Color(0.35f,0.28f,0.10f),
                new Color(0.50f,0.40f,0.35f) }},
            {"Cardassia9", new Color[] {
                new Color(0.50f,0.20f,0.55f), new Color(0.35f,0.15f,0.40f),
                new Color(0.60f,0.30f,0.60f), new Color(0.25f,0.10f,0.35f),
                new Color(0.40f,0.35f,0.45f) }},
            {"Chintoka2", new Color[] {
                new Color(0.60f,0.25f,0.15f), new Color(0.45f,0.18f,0.10f),
                new Color(0.55f,0.35f,0.20f), new Color(0.35f,0.15f,0.08f),
                new Color(0.50f,0.40f,0.30f) }},
            {"Chintoka6", new Color[] {
                new Color(0.20f,0.45f,0.25f), new Color(0.15f,0.35f,0.18f),
                new Color(0.28f,0.50f,0.30f), new Color(0.12f,0.30f,0.15f),
                new Color(0.35f,0.40f,0.30f) }},
            {"Chintoka9", new Color[] {
                new Color(0.15f,0.30f,0.60f), new Color(0.10f,0.22f,0.45f),
                new Color(0.20f,0.38f,0.65f), new Color(0.08f,0.18f,0.40f),
                new Color(0.30f,0.35f,0.50f) }},
        };

        float innerR = 0.5f, outerR = 1.0f;

        foreach (var kvp in ringPalettes)
        {
            string ringName = kvp.Key;
            Color[] palette = kvp.Value;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();

            for (int i = 0; i < rockCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(innerR, outerR);
                float y = Random.Range(-0.003f, 0.003f);
                float size = Random.Range(0.004f, 0.018f);
                Vector3 center = new Vector3(Mathf.Cos(angle)*r, y, Mathf.Sin(angle)*r);
                Quaternion rot = Random.rotation;

                Color baseC = palette[Random.Range(0, palette.Length)];
                Color rockCol = new Color(
                    Mathf.Clamp01(baseC.r + Random.Range(-0.06f, 0.06f)),
                    Mathf.Clamp01(baseC.g + Random.Range(-0.06f, 0.06f)),
                    Mathf.Clamp01(baseC.b + Random.Range(-0.06f, 0.06f)), 1f);

                int baseIdx = verts.Count;
                for (int v = 0; v < sv; v++)
                {
                    verts.Add(center + rot * (sphereVerts[v] * size));
                    uvs.Add(new Vector2((float)i/rockCount, 0));
                    colors.Add(rockCol);
                }
                for (int t = 0; t < st; t++) tris.Add(baseIdx + sphereTris[t]);
            }

            var ringMesh = new Mesh { name = ringName + "_RingRocks" };
            ringMesh.SetVertices(verts);
            ringMesh.SetTriangles(tris, 0);
            ringMesh.SetUVs(0, uvs);
            ringMesh.SetColors(colors);
            ringMesh.RecalculateNormals();
            ringMesh.RecalculateBounds();

            var meshPath = "Assets/Meshes/" + ringName + "_RingMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                existing.Clear();
                existing.SetVertices(verts);
                existing.SetTriangles(tris, 0);
                existing.SetUVs(0, uvs);
                existing.SetColors(colors);
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(ringMesh, meshPath);
            }

            var ringGo = GameObject.Find(ringName + "_Ring");
            if (ringGo != null)
            {
                var mf = ringGo.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var mr = ringGo.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.shader = vcShader;
                    EditorUtility.SetDirty(mr.sharedMaterial);
                }
            }
            Debug.Log("  " + ringName + ": " + verts.Count + " verts, " + palette.Length + " palette colors");
        }

        AssetDatabase.SaveAssets();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[Done] Rings rebuilt with per-rock multi-color vertex colors");
    }
}
