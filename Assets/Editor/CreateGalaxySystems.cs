using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// One-shot editor script: creates Cardassia and Chin'toka star systems in BattleScene.
/// Run via execute_csharp_script or menu item.
/// </summary>
public static class CreateGalaxySystems
{
    // Triangle layout — Bajor at origin
    // D = farthest Bajor planet distance from Bajor_Sun ≈ 76615m
    const float D = 76615f;
    static readonly Vector3 CardassiaSunPos = new Vector3(D * 14f, 0, 0);           // 14D
    static readonly Vector3 ChintokaSunPos = new Vector3(D * 18f * Mathf.Cos(85.9f * Mathf.Deg2Rad), 0, D * 18f * Mathf.Sin(85.9f * Mathf.Deg2Rad));

    // ── Ring mesh ──
    static Mesh _ringMesh;
    static Mesh GetRingMesh()
    {
        if (_ringMesh != null) return _ringMesh;
        int segs = 64;
        float innerR = 0.5f, outerR = 1.0f;
        var verts = new Vector3[(segs + 1) * 2];
        var tris = new int[segs * 6];
        var uvs = new Vector2[(segs + 1) * 2];
        for (int i = 0; i <= segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[i * 2] = new Vector3(c * innerR, 0, s * innerR);
            verts[i * 2 + 1] = new Vector3(c * outerR, 0, s * outerR);
            uvs[i * 2] = new Vector2(0, (float)i / segs);
            uvs[i * 2 + 1] = new Vector2(1, (float)i / segs);
        }
        for (int i = 0; i < segs; i++)
        {
            tris[i * 6] = i * 2;
            tris[i * 6 + 1] = i * 2 + 1;
            tris[i * 6 + 2] = (i + 1) * 2;
            tris[i * 6 + 3] = (i + 1) * 2;
            tris[i * 6 + 4] = i * 2 + 1;
            tris[i * 6 + 5] = (i + 1) * 2 + 1;
        }
        _ringMesh = new Mesh { name = "RingMesh", vertices = verts, triangles = tris, uv = uvs };
        _ringMesh.RecalculateNormals();
        // Save as asset
        var meshPath = "Assets/Meshes/RingMesh.asset";
        if (!AssetDatabase.LoadAssetAtPath<Mesh>(meshPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
                AssetDatabase.CreateFolder("Assets", "Meshes");
            AssetDatabase.CreateAsset(_ringMesh, meshPath);
        }
        return _ringMesh;
    }

    // ── Material helpers ──
    static Material CreateSunMaterial(Color color, string name)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        mat.SetColor("_Color", color);
        mat.SetColor("_EmissionColor", color * 2f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Metallic", 0f);
        var path = $"Assets/Materials/Planets/{name}.mat";
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Planets"))
            AssetDatabase.CreateFolder("Assets/Materials", "Planets");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material CreatePlanetMaterial(string name, Texture2D tex, Color? solidColor = null)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        if (tex != null)
        {
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", Color.white);
        }
        else if (solidColor.HasValue)
        {
            mat.SetColor("_Color", solidColor.Value);
        }
        // Emission off
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        mat.SetFloat("_Glossiness", 0.3f);
        mat.SetFloat("_Metallic", 0f);
        var path = $"Assets/Materials/Planets/{name}.mat";
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Planets"))
            AssetDatabase.CreateFolder("Assets/Materials", "Planets");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material CreateRingMaterial(Color color, string name)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        mat.SetColor("_Color", new Color(color.r, color.g, color.b, 0.5f));
        mat.SetFloat("_Mode", 2); // Fade
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        var path = $"Assets/Materials/Planets/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ── Texture loading ──
    static Texture2D LoadTex(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ── Planet creation ──
    struct PlanetDef
    {
        public string name;
        public Vector3 localPos;   // relative to sun
        public float scale;
        public Texture2D tex;
        public Color solidColor;
        public bool hasRing;
        public Color ringColor;
        public bool hasMoon;
        public float moonScale;
    }

    static GameObject CreateSun(Transform parent, string name, Vector3 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 5000f;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateSunMaterial(color, name + "_Mat");
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = 0.5f;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 80000f;
        light.intensity = 0f;
        return go;
    }

    static GameObject CreatePlanet(Transform parent, PlanetDef def)
    {
        var go = new GameObject(def.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = def.localPos;
        go.transform.localScale = Vector3.one * def.scale;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        string matName = def.name + "_Mat";
        mr.sharedMaterial = CreatePlanetMaterial(matName, def.tex, def.solidColor);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = 0.5f;

        // Ring
        if (def.hasRing)
        {
            var ringGo = new GameObject(def.name + "_Ring");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localRotation = Quaternion.Euler(75f, 0, 0); // tilted
            ringGo.transform.localScale = new Vector3(4.4f, 4.4f, 4.4f); // outer 2.2x planet
            var ringMf = ringGo.AddComponent<MeshFilter>();
            ringMf.sharedMesh = GetRingMesh();
            var ringMr = ringGo.AddComponent<MeshRenderer>();
            ringMr.sharedMaterial = CreateRingMaterial(def.ringColor, def.name + "_RingMat");
            ringMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringMr.receiveShadows = false;
        }

        // Moon
        if (def.hasMoon)
        {
            var moonGo = new GameObject(def.name + "_Moon1");
            moonGo.transform.SetParent(go.transform, false);
            moonGo.transform.localPosition = new Vector3(0.8f, 0.2f, 0);
            moonGo.transform.localScale = Vector3.one * (def.moonScale / def.scale);
            var moonMf = moonGo.AddComponent<MeshFilter>();
            moonMf.sharedMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
            var moonMr = moonGo.AddComponent<MeshRenderer>();
            moonMr.sharedMaterial = CreatePlanetMaterial(def.name + "_MoonMat", LoadTex("Assets/Textures/Planet_Planet_Moon.png"), new Color(0.6f, 0.6f, 0.6f));
            moonMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var moonSc = moonGo.AddComponent<SphereCollider>();
            moonSc.radius = 0.5f;
        }

        return go;
    }

    static GameObject CreateSystem(string systemName, Vector3 sunPos, Color sunColor, PlanetDef[] planets)
    {
        var se = GameObject.Find("SpaceEnvironment");
        var root = new GameObject(systemName);
        root.transform.SetParent(se != null ? se.transform : null, false);

        CreateSun(root.transform, systemName.Replace("System", "_Sun"), sunPos, sunColor);

        foreach (var pd in planets)
            CreatePlanet(root.transform, pd);

        return root;
    }

    public static void Execute()
    {
        // Clean up old systems if they exist
        foreach (var name in new[] { "CardassiaSystem", "ChintokaSystem" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }
        // Clean up old materials
        if (AssetDatabase.IsValidFolder("Assets/Materials/Planets"))
        {
            AssetDatabase.DeleteAsset("Assets/Materials/Planets");
            AssetDatabase.CreateFolder("Assets/Materials", "Planets");
        }

        // User textures
        var texGoldCrater = LoadTex("Assets/Textures/Planets/External/182331_20230126003822224209_0.jpg");
        var texMarsDesert = LoadTex("Assets/Textures/Planets/External/1a414de188e33933a46a3c07d03288.png");
        var texMercuryGray = LoadTex("Assets/Textures/Planets/External/7653513c4c082f0a11c5ef67d0e5d4.png");
        var texVenus = LoadTex("Assets/Textures/Planets/External/OIP-C-1.png");
        var texJupiter = LoadTex("Assets/Textures/Planets/External/OIP-C-2.png");
        var texNeptune = LoadTex("Assets/Textures/Planets/External/OIP-C-3.png");
        var texBarren = LoadTex("Assets/Textures/Planets/External/OIP-C.png");

        // Existing unused textures
        var texGas = LoadTex("Assets/Textures/Planet_Planet_Gas.png");
        var texIce = LoadTex("Assets/Textures/Planet_Planet_Ice.png");
        var texRock = LoadTex("Assets/Textures/Planet_Planet_Rock.png");
        var texDesert = LoadTex("Assets/Textures/Planet_Planet_Desert.png");
        var texCrystal = LoadTex("Assets/Textures/Planet_Planet_Crystal.png");
        var texFrozen = LoadTex("Assets/Textures/Planet_Planet_Frozen.png");
        var texLava = LoadTex("Assets/Textures/Planet_Planet_Lava.png");
        var texOcean = LoadTex("Assets/Textures/Planet_Planet_Ocean.png");
        var texStorm = LoadTex("Assets/Textures/Planet_Planet_Storm.png");

        // ── Cardassia System ──
        var cardassia = new PlanetDef[]
        {
            new PlanetDef { name="Cardassia1",  localPos=new Vector3(5000,100,3000),    scale=300,  tex=texGoldCrater,  solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Cardassia2",  localPos=new Vector3(-12000,-50,8000),   scale=500,  tex=texMarsDesert,  solidColor=Color.white, hasRing=false, hasMoon=true, moonScale=80 },
            new PlanetDef { name="Cardassia3",  localPos=new Vector3(18000,200,-5000),   scale=700,  tex=null,            solidColor=new Color(0.8f,0.2f,0.1f), hasRing=true, ringColor=new Color(0.9f,0.5f,0.3f), hasMoon=false },
            new PlanetDef { name="Cardassia4",  localPos=new Vector3(-25000,-100,12000), scale=600,  tex=texMercuryGray, solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Cardassia5",  localPos=new Vector3(32000,150,-18000),  scale=800,  tex=texIce,          solidColor=Color.white, hasRing=false, hasMoon=true, moonScale=120 },
            new PlanetDef { name="Cardassia6",  localPos=new Vector3(-40000,300,5000),   scale=900,  tex=null,            solidColor=new Color(0.2f,0.6f,0.3f), hasRing=true, ringColor=new Color(0.3f,0.4f,0.2f), hasMoon=false },
            new PlanetDef { name="Cardassia7",  localPos=new Vector3(15000,-200,22000),  scale=1200, tex=texVenus,        solidColor=Color.white, hasRing=true, ringColor=new Color(0.8f,0.7f,0.4f), hasMoon=true, moonScale=200 },
            new PlanetDef { name="Cardassia8",  localPos=new Vector3(-55000,100,-30000), scale=1500, tex=texGas,          solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Cardassia9",  localPos=new Vector3(62000,-50,15000),   scale=2000, tex=null,            solidColor=new Color(0.5f,0.3f,0.6f), hasRing=true, ringColor=new Color(0.6f,0.4f,0.7f), hasMoon=false },
            new PlanetDef { name="Cardassia10", localPos=new Vector3(-8000,250,-38000),  scale=450,  tex=texRock,         solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Cardassia11", localPos=new Vector3(68000,400,-22000),  scale=850,  tex=null,            solidColor=new Color(0.1f,0.4f,0.7f), hasRing=false, hasMoon=true, moonScale=150 },
            new PlanetDef { name="Cardassia12", localPos=new Vector3(52000,-300,-55000), scale=550,  tex=texCrystal,      solidColor=Color.white, hasRing=false, hasMoon=false },
        };
        CreateSystem("CardassiaSystem", CardassiaSunPos, new Color(1f, 0.4f, 0.1f), cardassia);

        // ── Chin'toka System ──
        var chintoka = new PlanetDef[]
        {
            new PlanetDef { name="Chintoka1",  localPos=new Vector3(6000,150,-4000),    scale=350,  tex=texBarren,       solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Chintoka2",  localPos=new Vector3(-11000,-100,9000),   scale=550,  tex=null,            solidColor=new Color(0.6f,0.3f,0.1f), hasRing=true, ringColor=new Color(0.7f,0.5f,0.3f), hasMoon=false },
            new PlanetDef { name="Chintoka3",  localPos=new Vector3(16000,200,5000),     scale=650,  tex=texDesert,       solidColor=Color.white, hasRing=false, hasMoon=true, moonScale=100 },
            new PlanetDef { name="Chintoka4",  localPos=new Vector3(-23000,50,-15000),   scale=500,  tex=null,            solidColor=new Color(0.3f,0.3f,0.8f), hasRing=false, hasMoon=false },
            new PlanetDef { name="Chintoka5",  localPos=new Vector3(30000,-200,12000),   scale=750,  tex=texFrozen,       solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Chintoka6",  localPos=new Vector3(-38000,300,-8000),   scale=1000, tex=null,            solidColor=new Color(0.5f,0.2f,0.8f), hasRing=true, ringColor=new Color(0.5f,0.3f,0.9f), hasMoon=true, moonScale=180 },
            new PlanetDef { name="Chintoka7",  localPos=new Vector3(14000,-150,-25000),   scale=1300, tex=texJupiter,      solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Chintoka8",  localPos=new Vector3(-50000,100,25000),   scale=900,  tex=texStorm,        solidColor=Color.white, hasRing=false, hasMoon=true, moonScale=130 },
            new PlanetDef { name="Chintoka9",  localPos=new Vector3(58000,-50,-15000),   scale=1800, tex=null,            solidColor=new Color(0.2f,0.5f,0.9f), hasRing=true, ringColor=new Color(0.3f,0.6f,0.95f), hasMoon=false },
            new PlanetDef { name="Chintoka10", localPos=new Vector3(-7000,250,40000),    scale=400,  tex=texNeptune,      solidColor=Color.white, hasRing=false, hasMoon=false },
            new PlanetDef { name="Chintoka11", localPos=new Vector3(65000,400,28000),    scale=700,  tex=null,            solidColor=new Color(0.8f,0.6f,0.2f), hasRing=false, hasMoon=true, moonScale=120 },
            new PlanetDef { name="Chintoka12", localPos=new Vector3(48000,-300,-58000), scale=480,  tex=texLava,         solidColor=Color.white, hasRing=false, hasMoon=false },
        };
        CreateSystem("ChintokaSystem", ChintokaSunPos, new Color(0.3f, 0.6f, 1f), chintoka);

        // Save scene
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Debug.Log($"[GalaxySystems] Cardassia at {CardassiaSunPos}, Chin'toka at {ChintokaSunPos}");
        Debug.Log($"[GalaxySystems] Distance Cardassia-Bajor: {Vector3.Distance(CardassiaSunPos, Vector3.zero):F0}m (target: {D*14f:F0})");
        Debug.Log($"[GalaxySystems] Distance Chintoka-Bajor: {Vector3.Distance(ChintokaSunPos, Vector3.zero):F0}m (target: {D*18f:F0})");
        Debug.Log($"[GalaxySystems] Distance Cardassia-Chintoka: {Vector3.Distance(CardassiaSunPos, ChintokaSunPos):F0}m (target: {D*22f:F0})");
    }
}
