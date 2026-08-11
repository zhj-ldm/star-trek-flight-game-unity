using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Renders a static starfield as a fullscreen background texture.
    /// Generates a large black texture with randomly placed star dots,
    /// displayed on a Canvas behind all game objects.
    /// </summary>
    public class StarfieldGenerator : MonoBehaviour
    {
        [Header("Texture")]
        public int textureWidth = 2048;
        public int textureHeight = 1024;
        public int starCount = 3000;
        public float minBrightness = 0.3f;
        public float maxBrightness = 1f;
        public Color backgroundColor = Color.black;

        [Header("Star Sizes (pixels)")]
        public int minStarSize = 1;
        public int maxStarSize = 3;

        private Texture2D _starTexture;
        private GameObject _bgObj;

        void Start()
        {
            GenerateStarTexture();
            CreateBackgroundObject();
        }

        private void GenerateStarTexture()
        {
            _starTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _starTexture.filterMode = FilterMode.Bilinear;
            _starTexture.wrapMode = TextureWrapMode.Clamp;

            // Fill with black
            Color[] pixels = new Color[textureWidth * textureHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = backgroundColor;
            _starTexture.SetPixels(pixels);

            // Draw stars as small circles
            for (int i = 0; i < starCount; i++)
            {
                int cx = Random.Range(0, textureWidth);
                int cy = Random.Range(0, textureHeight);
                int radius = Random.Range(minStarSize, maxStarSize + 1);
                float brightness = Random.Range(minBrightness, maxBrightness);

                // Slight color variation: white, blue-white, yellow-white
                float tint = Random.Range(0f, 1f);
                Color starColor;
                if (tint < 0.6f)
                    starColor = new Color(brightness, brightness, brightness, 1f);
                else if (tint < 0.8f)
                    starColor = new Color(brightness * 0.8f, brightness * 0.9f, brightness, 1f);
                else
                    starColor = new Color(brightness, brightness * 0.95f, brightness * 0.8f, 1f);

                DrawStar(_starTexture, cx, cy, radius, starColor);
            }

            _starTexture.Apply();
        }

        private void DrawStar(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        {
                            float falloff = 1f - (dist / radius);
                            falloff = Mathf.Clamp01(falloff);
                            Color existing = tex.GetPixel(x, y);
                            Color blended = new Color(
                                Mathf.Max(existing.r, color.r * falloff),
                                Mathf.Max(existing.g, color.g * falloff),
                                Mathf.Max(existing.b, color.b * falloff),
                                1f
                            );
                            tex.SetPixel(x, y, blended);
                        }
                    }
                }
            }
        }

        private void CreateBackgroundObject()
        {
            // Create a simple quad at far distance, parented to camera
            var cam = Camera.main;
            if (cam == null) return;

            _bgObj = new GameObject("StarfieldBackground");
            _bgObj.transform.SetParent(cam.transform, false);
            _bgObj.transform.localPosition = new Vector3(0, 0, cam.farClipPlane * 0.9f);
            _bgObj.transform.localRotation = Quaternion.identity;

            // Scale quad to fill camera view at that distance
            float dist = cam.farClipPlane * 0.9f;
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float halfHeight = Mathf.Tan(fov * 0.5f) * dist;
            float halfWidth = halfHeight * cam.aspect;
            _bgObj.transform.localScale = new Vector3(halfWidth * 2, halfHeight * 2, 1);

            var mf = _bgObj.AddComponent<MeshFilter>();
            mf.mesh = CreateQuadMesh();

            var mr = _bgObj.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Unlit/Texture"));
            if (mat == null) mat = new Material(Shader.Find("UI/Default"));
            mat.mainTexture = _starTexture;
            mr.material = mat;
            mr.sortingOrder = -1000;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        void LateUpdate()
        {
            // Background follows camera — parented in Start, nothing needed here
        }

        /// <summary>Legacy method — kept for compatibility, no longer generates particles.</summary>
        public void GenerateStars()
        {
            GenerateStarTexture();
            CreateBackgroundObject();
        }
    }
}
