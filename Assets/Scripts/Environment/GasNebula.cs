using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// 3D volumetric gas nebula using layered particle systems with procedural
    /// noise textures. Multiple color clusters (green, purple, cyan, magenta,
    /// orange) create a rich multi-color nebula. All procedural, no external assets.
    /// Uses Emit() + Local simulation space so particles are visible in edit mode
    /// and follow the GameObject transform for manual positioning.
    /// </summary>
    [ExecuteAlways]
    public class GasNebula : MonoBehaviour
    {
        [Header("Scale")]
        public float radius = 5000f;
        public float coreRadius = 1200f;

        [Header("Particle Counts")]
        public int hazeCount = 300;
        public int cloudCount = 800;
        public int knotCount = 400;
        public int starCount = 150;

        [Header("Color Clusters")]
        public Color[] clusterColors = new Color[]
        {
            new Color(0.08f, 0.6f, 0.15f, 0.25f),   // Green
            new Color(0.35f, 0.05f, 0.55f, 0.25f),   // Purple
            new Color(0.05f, 0.4f, 0.6f, 0.25f),      // Cyan-blue
            new Color(0.6f, 0.05f, 0.35f, 0.25f),     // Magenta
            new Color(0.7f, 0.35f, 0.05f, 0.25f),     // Orange
            new Color(0.15f, 0.5f, 0.3f, 0.25f),      // Teal
        };

        [Header("Hot Core Color")]
        public Color hotColor = new Color(1f, 0.85f, 0.6f, 0.5f);

        [Header("Star Colors")]
        public Color starColorA = new Color(0.4f, 0.7f, 1f, 1f);  // Blue-white
        public Color starColorB = new Color(1f, 0.8f, 0.5f, 1f);  // Yellow-white

        [Header("Animation")]
        public float rotationSpeed = 0.15f;

        [Header("Noise Seed")]
        public int noiseSeed = 42;

        // ---- Cached procedural textures ----
        private static Texture2D _cloudTex;
        private static Texture2D _coreTex;
        private static Texture2D _starTex;

        // =================================================================
        //  Procedural textures
        // =================================================================

        private static Texture2D GetCloudTexture()
        {
            if (_cloudTex != null) return _cloudTex;
            int sz = 256;
            _cloudTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _cloudTex.filterMode = FilterMode.Bilinear;
            _cloudTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float nx = x / (float)sz;
                    float ny = y / (float)sz;
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float falloff = Mathf.Clamp01(1f - d);
                    falloff = Mathf.Pow(falloff, 1.8f);
                    float n = FBM2D(nx * 6f, ny * 6f, 5);
                    n = Mathf.Lerp(0.3f, 1f, n);
                    float alpha = falloff * n;
                    px[y * sz + x] = new Color(1f, 0.92f, 0.82f, alpha);
                }
            _cloudTex.SetPixels(px);
            _cloudTex.Apply();
            return _cloudTex;
        }

        private static Texture2D GetCoreTexture()
        {
            if (_coreTex != null) return _coreTex;
            int sz = 128;
            _coreTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _coreTex.filterMode = FilterMode.Bilinear;
            _coreTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 2.5f);
                    float n = FBM2D(x / 64f, y / 64f, 3);
                    a *= Mathf.Lerp(0.6f, 1f, n);
                    px[y * sz + x] = new Color(1f, 0.88f, 0.68f, a);
                }
            _coreTex.SetPixels(px);
            _coreTex.Apply();
            return _coreTex;
        }

        private static Texture2D GetStarTexture()
        {
            if (_starTex != null) return _starTex;
            int sz = 32;
            _starTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _starTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    px[y * sz + x] = new Color(1f, 1f, 0.95f, a);
                }
            _starTex.SetPixels(px);
            _starTex.Apply();
            return _starTex;
        }

        // =================================================================
        //  2D noise utilities
        // =================================================================

        private static float Hash2D(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static float ValueNoise2D(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            float a = Hash2D(ix, iy);
            float b = Hash2D(ix + 1, iy);
            float cc = Hash2D(ix, iy + 1);
            float dd = Hash2D(ix + 1, iy + 1);
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(cc, dd, ux), uy);
        }

        private static float FBM2D(float x, float y, int octaves)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += ValueNoise2D(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return total / maxValue;
        }

        // =================================================================
        //  3D noise for particle distribution
        // =================================================================

        private float Hash3D(float x, float y, float z)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + z * 74.7f + noiseSeed) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private float ValueNoise3D(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            int iz = Mathf.FloorToInt(z);
            float fx = x - ix;
            float fy = y - iy;
            float fz = z - iz;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            float uz = fz * fz * (3f - 2f * fz);

            float c000 = Hash3D(ix, iy, iz);
            float c100 = Hash3D(ix + 1, iy, iz);
            float c010 = Hash3D(ix, iy + 1, iz);
            float c110 = Hash3D(ix + 1, iy + 1, iz);
            float c001 = Hash3D(ix, iy, iz + 1);
            float c101 = Hash3D(ix + 1, iy, iz + 1);
            float c011 = Hash3D(ix, iy + 1, iz + 1);
            float c111 = Hash3D(ix + 1, iy + 1, iz + 1);

            float x00 = Mathf.Lerp(c000, c100, ux);
            float x10 = Mathf.Lerp(c010, c110, ux);
            float x01 = Mathf.Lerp(c001, c101, ux);
            float x11 = Mathf.Lerp(c011, c111, ux);
            float y0 = Mathf.Lerp(x00, x10, uy);
            float y1 = Mathf.Lerp(x01, x11, uy);
            return Mathf.Lerp(y0, y1, uz);
        }

        private float FBM3D(float x, float y, float z, int octaves)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += ValueNoise3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return total / maxValue;
        }

        // =================================================================
        //  Material helper
        // =================================================================

        private static Material CreateAdditiveMat(Texture2D tex, Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Mode", 4); // Additive
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        // =================================================================
        //  Color cluster lookup
        // =================================================================

        private Color GetClusterColor(Vector3 pos, float distFromCenter)
        {
            float colorNoise = FBM3D(
                (pos.x + radius) * 0.0008f,
                (pos.y + radius) * 0.0008f,
                (pos.z + radius) * 0.0008f, 3);

            int clusterIdx = Mathf.FloorToInt(colorNoise * clusterColors.Length);
            clusterIdx = Mathf.Clamp(clusterIdx, 0, clusterColors.Length - 1);

            float blend = (colorNoise * clusterColors.Length) - clusterIdx;
            Color c0 = clusterColors[clusterIdx];
            Color c1 = clusterColors[(clusterIdx + 1) % clusterColors.Length];
            Color result = Color.Lerp(c0, c1, blend);

            if (distFromCenter < coreRadius)
            {
                float hotT = 1f - (distFromCenter / coreRadius);
                result = Color.Lerp(result, hotColor, hotT * 0.7f);
            }

            result.r *= Random.Range(0.85f, 1f);
            result.g *= Random.Range(0.85f, 1f);
            result.b *= Random.Range(0.85f, 1f);
            result.a *= Random.Range(0.7f, 1f);

            return result;
        }

        // =================================================================
        //  Build
        // =================================================================

        private void Awake()
        {
            BuildNebula();
        }

        private void OnEnable()
        {
            if (transform.childCount == 0)
                BuildNebula();
        }

        private void Update()
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
        }

        public void BuildNebula()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            // Layer 0: Deep haze
            CreateLayer(
                "Haze", GetCloudTexture(),
                count: hazeCount,
                minSize: radius * 0.35f, maxSize: radius * 0.55f,
                sortFudge: -20,
                noiseScale: 0.0012f,
                densityBias: 0.25f,
                useClusterColor: true);

            // Layer 1: Main clouds
            CreateLayer(
                "Clouds", GetCloudTexture(),
                count: cloudCount,
                minSize: radius * 0.08f, maxSize: radius * 0.2f,
                sortFudge: -10,
                noiseScale: 0.0025f,
                densityBias: 0.35f,
                useClusterColor: true);

            // Layer 2: Bright knots
            CreateLayer(
                "Knots", GetCoreTexture(),
                count: knotCount,
                minSize: radius * 0.02f, maxSize: radius * 0.06f,
                sortFudge: 0,
                noiseScale: 0.004f,
                densityBias: 0.5f,
                useClusterColor: true);

            // Layer 3: Embedded stars
            CreateLayer(
                "Stars", GetStarTexture(),
                count: starCount,
                minSize: 3f, maxSize: 10f,
                sortFudge: 5,
                noiseScale: 0.006f,
                densityBias: 0.55f,
                useClusterColor: false,
                isStar: true);
        }

        private void CreateLayer(
            string name, Texture2D tex,
            int count, float minSize, float maxSize, int sortFudge,
            float noiseScale, float densityBias,
            bool useClusterColor, bool isStar = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            // Local space: particles follow the nebula transform position
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = Mathf.Infinity;
            main.startSpeed = 0f;
            main.maxParticles = count + 20;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.burstCount = 0;

            var shape = ps.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
            renderer.material = CreateAdditiveMat(tex, Color.white);
            renderer.sortingFudge = sortFudge;

            // Play so particles are visible, then emit. startSpeed=0 so they stay put.
            ps.Play();
            var emitParams = new ParticleSystem.EmitParams();

            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 30;

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;
                Vector3 dir = Random.onUnitSphere;
                float dist = Mathf.Pow(Random.value, 0.5f) * radius;
                Vector3 pos = dir * dist;

                float density = FBM3D(
                    (pos.x + radius) * noiseScale,
                    (pos.y + radius) * noiseScale,
                    (pos.z + radius) * noiseScale, 4);

                if (density < densityBias)
                    continue;

                float distFromCenter = pos.magnitude;
                Color particleColor;

                if (isStar)
                {
                    Color sc = Random.value > 0.5f ? starColorA : starColorB;
                    particleColor = new Color(
                        sc.r * Random.Range(0.7f, 1f),
                        sc.g * Random.Range(0.7f, 1f),
                        sc.b * Random.Range(0.7f, 1f),
                        Random.Range(0.6f, 1f));
                }
                else if (useClusterColor)
                {
                    particleColor = GetClusterColor(pos, distFromCenter);
                }
                else
                {
                    particleColor = Color.white;
                }

                float size = Random.Range(minSize, maxSize);

                emitParams.position = pos;
                emitParams.startSize = size;
                emitParams.startColor = particleColor;
                emitParams.startLifetime = Mathf.Infinity;
                emitParams.velocity = Vector3.zero;
                emitParams.rotation = Random.Range(0f, 360f);

                ps.Emit(emitParams, 1);
                placed++;
            }

            // System stays in Playing state with 0 emission rate — particles persist
        }

        [ContextMenu("Rebuild Nebula")]
        public void Rebuild()
        {
            BuildNebula();
        }
    }
}
