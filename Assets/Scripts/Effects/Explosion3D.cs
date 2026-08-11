using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// High-quality explosion using layered particle systems with procedural
    /// textures and additive blending. No geometric primitives.
    /// Layers: core flash, fireball, expanding fire, sparks, shockwave ring, smoke.
    /// All colors red/orange. Self-cleans via Destroy().
    /// </summary>
    public class Explosion3D : MonoBehaviour
    {
        [Header("Scale")]
        public float scale = 1f;
        public float duration = 2.5f;

        // ---- Procedural textures (cached) ----
        private static Texture2D _fireTex;    // radial gradient: white center -> orange -> transparent
        private static Texture2D _smokeTex;   // noisy cloud
        private static Texture2D _sparkTex;   // small bright dot
        private static Texture2D _ringTex;    // thin ring

        private float _timer;

        void Start()
        {
            BuildExplosion();
            _timer = 0f;
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= duration)
                Destroy(gameObject);
        }

        // =================================================================
        //  Texture generation — all procedural, no external files needed
        // =================================================================

        private static Texture2D GetFireTexture()
        {
            if (_fireTex != null) return _fireTex;
            int sz = 128;
            _fireTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _fireTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 1.5f);
                    // White-hot center -> orange -> dark red edge
                    float heat = a;
                    float cr = Mathf.Lerp(0.3f, 1f, heat);
                    float cg = Mathf.Lerp(0.02f, 0.85f, heat * heat);
                    float cb = Mathf.Lerp(0f, 0.3f, heat * heat * heat);
                    px[y * sz + x] = new Color(cr, cg, cb, a);
                }
            _fireTex.SetPixels(px);
            _fireTex.Apply();
            return _fireTex;
        }

        private static Texture2D GetSmokeTexture()
        {
            if (_smokeTex != null) return _smokeTex;
            int sz = 128;
            _smokeTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _smokeTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            // Pseudo-random noise seed
            uint seed = 12345;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 2f);
                    // Add noise
                    seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF;
                    float n = (seed % 1000) / 1000f;
                    a *= 0.5f + n * 0.5f;
                    px[y * sz + x] = new Color(0.12f, 0.06f, 0.03f, a * 0.7f);
                }
            _smokeTex.SetPixels(px);
            _smokeTex.Apply();
            return _smokeTex;
        }

        private static Texture2D GetSparkTexture()
        {
            if (_sparkTex != null) return _sparkTex;
            int sz = 32;
            _sparkTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _sparkTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a; // very tight
                    px[y * sz + x] = new Color(1f, 0.9f, 0.5f, a);
                }
            _sparkTex.SetPixels(px);
            _sparkTex.Apply();
            return _sparkTex;
        }

        private static Texture2D GetRingTexture()
        {
            if (_ringTex != null) return _ringTex;
            int sz = 128;
            _ringTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            _ringTex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            Vector2 c = new Vector2(sz / 2f, sz / 2f);
            float r = sz / 2f;
            float ringStart = r * 0.7f;
            float ringEnd = r * 0.95f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = 0f;
                    if (d >= ringStart && d <= ringEnd)
                    {
                        float t = (d - ringStart) / (ringEnd - ringStart);
                        a = Mathf.Sin(t * Mathf.PI); // peak in middle of ring band
                    }
                    px[y * sz + x] = new Color(1f, 0.5f, 0.1f, a * 0.8f);
                }
            _ringTex.SetPixels(px);
            _ringTex.Apply();
            return _ringTex;
        }

        // =================================================================
        //  Material helpers
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

        private static Material CreateAlphaMat(Texture2D tex, Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Mode", 2); // Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        // =================================================================
        //  Explosion builder
        // =================================================================

        private void BuildExplosion()
        {
            float s = scale;

            // No light — red/orange point light illuminates ship hull and looks like internal glow

            // ---- Layer 1: Core flash (tiny, blinding white-hot burst) ----
            CreateParticleLayer("CoreFlash", GetFireTexture(), new Color(1f, 0.95f, 0.7f, 1f),
                startSize: 6f * s, startSpeed: 0f, lifetime: 0.3f, count: 1,
                additive: true, sortFudge: 10);

            // ---- Layer 2: Fireball (large, expanding) ----
            CreateParticleLayer("Fireball", GetFireTexture(), new Color(1f, 0.6f, 0.15f, 1f),
                startSize: 10f * s, startSpeed: 2f * s, lifetime: 0.8f, count: 8,
                additive: true, sortFudge: 5, sizeOverLife: new Keyframe[]
                {
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0.1f)
                },
                colorOverLifeStart: new Color(1f, 0.7f, 0.2f, 0.9f), colorOverLifeEnd: new Color(0.3f, 0.02f, 0f, 0f));

            // ---- Layer 3: Expanding fire cloud ----
            CreateParticleLayer("FireCloud", GetFireTexture(), new Color(1f, 0.35f, 0.05f, 0.8f),
                startSize: 8f * s, startSpeed: 12f * s, lifetime: 1.2f, count: 25,
                additive: true, sortFudge: 3, sizeOverLife: new Keyframe[]
                {
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.4f, 1f),
                    new Keyframe(1f, 0.3f)
                },
                colorOverLifeStart: new Color(1f, 0.4f, 0.1f, 0.7f), colorOverLifeEnd: new Color(0.2f, 0.01f, 0f, 0f),
                shapeRadius: 1f * s);

            // ---- Layer 4: Sparks (bright, fast, many) ----
            CreateParticleLayer("Sparks", GetSparkTexture(), new Color(1f, 0.85f, 0.4f, 1f),
                startSize: 0.4f * s, startSpeed: 25f * s, lifetime: 1.5f, count: 60,
                additive: true, sortFudge: 8, sizeOverLife: new Keyframe[]
                {
                    new Keyframe(0f, 1f),
                    new Keyframe(1f, 0f)
                },
                colorOverLifeStart: new Color(1f, 0.9f, 0.5f, 1f), colorOverLifeEnd: new Color(0.5f, 0.05f, 0f, 0f),
                shapeRadius: 0.5f * s);

            // ---- Layer 5: Shockwave ring (flat, expanding) ----
            CreateShockwave(s);

            // ---- Layer 6: Smoke (dark, rising, alpha-blended) ----
            CreateParticleLayer("Smoke", GetSmokeTexture(), new Color(0.12f, 0.06f, 0.03f, 0.5f),
                startSize: 6f * s, startSpeed: 4f * s, lifetime: 2.5f, count: 20,
                additive: false, sortFudge: -5, sizeOverLife: new Keyframe[]
                {
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.5f, 1f),
                    new Keyframe(1f, 1.8f)
                },
                colorOverLifeStart: new Color(0.15f, 0.08f, 0.03f, 0.5f), colorOverLifeEnd: new Color(0.05f, 0.02f, 0.01f, 0f),
                shapeRadius: 2f * s,
                worldSpaceVelocity: true, upwardBias: true);
        }

        private GameObject CreateParticleLayer(
            string name, Texture2D tex, Color startColor,
            float startSize, float startSpeed, float lifetime, int count,
            bool additive, int sortFudge,
            Keyframe[] sizeOverLife = null,
            Color? colorOverLifeStart = null, Color? colorOverLifeEnd = null,
            float shapeRadius = 0f,
            bool worldSpaceVelocity = false, bool upwardBias = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startColor = startColor;
            main.maxParticles = count + 5;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, count));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = shapeRadius;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();

            Color matColor = colorOverLifeStart ?? startColor;
            renderer.material = additive
                ? CreateAdditiveMat(tex, matColor)
                : CreateAlphaMat(tex, matColor);
            renderer.sortingFudge = sortFudge;

            // Size over lifetime
            if (sizeOverLife != null && sizeOverLife.Length > 0)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var curve = new AnimationCurve(sizeOverLife);
                sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            // Color over lifetime
            if (colorOverLifeStart.HasValue && colorOverLifeEnd.HasValue)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(colorOverLifeStart.Value, colorOverLifeEnd.Value);
            }

            // Upward drift for smoke
            if (worldSpaceVelocity && upwardBias)
            {
                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.World;
                vol.y = new ParticleSystem.MinMaxCurve(2f, 6f);
                vol.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
                vol.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            }

            ps.Play();
            Destroy(go, lifetime + 0.5f);
            return go;
        }

        private void CreateShockwave(float s)
        {
            var go = new GameObject("ShockwaveRing");
            go.transform.SetParent(transform, false);
            go.transform.Rotate(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 2f * s;
            main.startColor = new Color(1f, 0.4f, 0.08f, 0.8f);
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 1));

            var shape = ps.shape;
            shape.enabled = false; // use the texture itself as the ring

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
            renderer.material = CreateAdditiveMat(GetRingTexture(), new Color(1f, 0.4f, 0.08f, 0.8f));
            renderer.sortingFudge = 1;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.3f, 0.8f),
                new Keyframe(1f, 0.1f)
            ));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.5f, 0.1f, 0.8f),
                new Color(0.3f, 0.02f, 0f, 0f)
            );

            ps.Play();
            Destroy(go, 1f);
        }

        /// <summary>
        /// Spawn a high-quality explosion at the given position.
        /// scale: 0.5 = small (torpedo), 1.5 = large (ship destruction)
        /// </summary>
        public static Explosion3D Spawn(Vector3 position, float scale = 1f)
        {
            var obj = new GameObject("Explosion3D");
            obj.transform.position = position;
            var exp = obj.AddComponent<Explosion3D>();
            exp.scale = scale;
            return exp;
        }
    }
}
