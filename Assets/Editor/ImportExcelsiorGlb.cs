using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace StarTrekCombat.Editor
{
    /// <summary>
    /// Imports the Excelsior GLB file, extracts meshes with proper base-color textures,
    /// and creates a prefab at Assets/Resources/ExcelsiorModel.prefab.
    /// Menu: Tools > Star Trek > Import Excelsior GLB
    /// </summary>
    public static class ImportExcelsiorGlb
    {
        const string GlbPath = "Assets/Models/excelsior.glb";
        const string OutputDir = "Assets/Models/Excelsior";
        const string TextureDir = "Assets/Models/Excelsior/Textures";
        const string MeshAssetPath = "Assets/Models/Excelsior/excelsior_meshes.asset";
        const string PrefabPath = "Assets/Resources/ExcelsiorModel.prefab";

        [MenuItem("Tools/Star Trek/Import Excelsior GLB")]
        public static void Import()
        {
            if (!File.Exists(GlbPath))
            {
                Debug.LogError($"[Excelsior] GLB not found: {GlbPath}");
                return;
            }

            byte[] bytes = File.ReadAllBytes(GlbPath);
            uint magic = System.BitConverter.ToUInt32(bytes, 0);
            if (magic != 0x46546C67)
            {
                Debug.LogError("[Excelsior] Not a valid GLB!");
                return;
            }

            // Parse chunks
            int offset = 12;
            string json = null;
            byte[] binData = null;
            while (offset + 8 <= bytes.Length)
            {
                uint chunkLength = System.BitConverter.ToUInt32(bytes, offset);
                uint chunkType = System.BitConverter.ToUInt32(bytes, offset + 4);
                offset += 8;
                if (offset + (int)chunkLength > bytes.Length) break;
                byte[] chunkData = new byte[chunkLength];
                System.Array.Copy(bytes, offset, chunkData, 0, (int)chunkLength);
                offset += (int)chunkLength;
                if (chunkType == 0x4E4F534A) json = Encoding.ASCII.GetString(chunkData);
                else if (chunkType == 0x004E4942) binData = chunkData;
            }
            if (json == null || binData == null)
            {
                Debug.LogError("[Excelsior] Missing JSON or BIN chunks!");
                return;
            }

            var gltf = JsonUtility.FromJson<GltfRoot>(PreprocessJson(json));
            if (gltf.meshes == null || gltf.meshes.Length == 0)
            {
                Debug.LogError("[Excelsior] No meshes in GLB!");
                return;
            }

            // Create output directories (clean old output first)
            if (AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.DeleteAsset(OutputDir);
            EnsureFolder("Assets/Models");
            EnsureFolder(OutputDir);
            EnsureFolder(TextureDir);

            // Extract textures as PNG/JPG files and import them properly
            List<Texture2D> textures = ExtractAndSaveTextures(gltf, binData);
            AssetDatabase.Refresh(); // Ensure all textures are imported before creating materials
            Debug.Log($"[Excelsior] Extracted {textures.Count} textures");

            // Create materials with proper base color texture mapping
            List<Material> materials = CreateMaterials(gltf, textures);
            Debug.Log($"[Excelsior] Created {materials.Count} materials");

            // Build meshes and GameObject hierarchy
            List<Mesh> meshAssets = new List<Mesh>();
            GameObject rootGo = BuildHierarchy(gltf, binData, materials, meshAssets);
            Debug.Log($"[Excelsior] Built hierarchy with {meshAssets.Count} meshes");

            if (meshAssets.Count == 0)
            {
                Debug.LogError("[Excelsior] No meshes built!");
                return;
            }

            // Save meshes as .asset (textures are already saved as separate PNG files)
            Mesh primaryMesh = meshAssets[0];
            if (File.Exists(MeshAssetPath)) AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(primaryMesh, MeshAssetPath);
            for (int i = 1; i < meshAssets.Count; i++)
            {
                meshAssets[i].name = $"submesh_{i}";
                AssetDatabase.AddObjectToAsset(meshAssets[i], MeshAssetPath);
            }
            foreach (var mat in materials)
            {
                mat.name = $"mat_{mat.name}";
                AssetDatabase.AddObjectToAsset(mat, MeshAssetPath);
            }
            AssetDatabase.SaveAssets();

            // Save prefab
            EnsureFolder("Assets/Resources");
            if (File.Exists(PrefabPath)) AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);

            AssetDatabase.Refresh();
            Debug.Log($"[Excelsior] Done! Prefab: {PrefabPath}, Mesh: {MeshAssetPath}");
        }

        #region JSON Preprocessing

        /// <summary>
        /// Replace extension keys that contain dots (invalid for JsonUtility field names)
        /// with simple keys matching the serializable class fields.
        /// </summary>
        static string PreprocessJson(string json)
        {
            json = json.Replace("\"KHR_materials_pbrSpecularGlossiness\"", "\"specGloss\"");
            json = json.Replace("\"KHR_materials_emissive_strength\"", "\"emissiveStrength\"");
            return json;
        }

        #endregion

        #region Texture Extraction

        /// <summary>
        /// Extract each image from the GLB as a PNG file, import it with proper settings,
        /// and return the loaded Texture2D assets.
        /// </summary>
        static List<Texture2D> ExtractAndSaveTextures(GltfRoot gltf, byte[] binData)
        {
            var result = new List<Texture2D>();
            if (gltf.images == null) return result;

            foreach (var img in gltf.images)
            {
                var bv = gltf.bufferViews[img.bufferView];
                int off = bv.byteOffset;
                int len = bv.byteLength;
                byte[] imgData = new byte[len];
                System.Array.Copy(binData, off, imgData, 0, len);

                // Determine file extension from mimeType
                string ext = "png";
                if (!string.IsNullOrEmpty(img.mimeType) && img.mimeType.Contains("jpeg"))
                    ext = "jpg";

                string texName = string.IsNullOrEmpty(img.name) ? $"texture_{result.Count}" : img.name;
                string texPath = $"{TextureDir}/tex_{result.Count}_{texName}.{ext}";

                // Write raw image data to file and let Unity import it
                File.WriteAllBytes(texPath, imgData);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

                // Configure import settings
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true; // Base color textures should be sRGB
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    Debug.LogError($"  [Excelsior] Failed to load imported texture: {texPath}");
                    // Fallback: create runtime texture
                    tex = new Texture2D(2, 2);
                    tex.LoadImage(imgData);
                    tex.name = texName;
                }
                else
                {
                    tex.name = texName;
                }
                result.Add(tex);
                Debug.Log($"  [Excelsior] Saved texture [{result.Count - 1}]: {texPath} ({tex.width}x{tex.height})");
            }
            return result;
        }

        /// <summary>Map texture index → Texture2D via gltf.textures[source].</summary>
        static Texture2D GetTextureByIndex(GltfRoot gltf, List<Texture2D> images, int textureIndex)
        {
            if (gltf.textures == null || textureIndex < 0 || textureIndex >= gltf.textures.Length)
                return null;
            int sourceIdx = gltf.textures[textureIndex].source;
            if (sourceIdx < 0 || sourceIdx >= images.Count) return null;
            return images[sourceIdx];
        }

        #endregion

        #region Material Creation

        static List<Material> CreateMaterials(GltfRoot gltf, List<Texture2D> textures)
        {
            var result = new List<Material>();
            if (gltf.materials == null) return result;

            var stdShader = Shader.Find("Standard");
            if (stdShader == null) stdShader = Shader.Find("Universal Render Pipeline/Lit");

            foreach (var mat in gltf.materials)
            {
                Material m = new Material(stdShader);
                m.name = mat.name ?? "material";

                bool hasDiffuse = false;

                // 1) KHR_materials_pbrSpecularGlossiness (Specular-Glossiness workflow)
                if (mat.extensions != null && mat.extensions.specGloss != null)
                {
                    var sg = mat.extensions.specGloss;

                    // Diffuse color factor
                    if (sg.diffuseFactor != null && sg.diffuseFactor.Length >= 4)
                    {
                        m.SetColor("_Color", new Color(sg.diffuseFactor[0], sg.diffuseFactor[1], sg.diffuseFactor[2], sg.diffuseFactor[3]));
                    }
                    else
                    {
                        m.SetColor("_Color", Color.white);
                    }

                    // Diffuse texture = base color (_MainTex)
                    if (sg.diffuseTexture != null)
                    {
                        var tex = GetTextureByIndex(gltf, textures, sg.diffuseTexture.index);
                        if (tex != null)
                        {
                            m.SetTexture("_MainTex", tex);
                            hasDiffuse = true;
                            Debug.Log($"  [Excelsior] Material '{m.name}' specGloss diffuseTexture → _MainTex = {tex.name}");
                        }
                    }

                    // Glossiness factor → smoothness
                    m.SetFloat("_Glossiness", sg.glossinessFactor);
                    m.SetFloat("_Metallic", 0f);
                }

                // 2) Fall back to pbrMetallicRoughness if no diffuse texture found
                if (!hasDiffuse && mat.pbrMetallicRoughness != null)
                {
                    var pbr = mat.pbrMetallicRoughness;

                    if (pbr.baseColorFactor != null && pbr.baseColorFactor.Length >= 4)
                    {
                        m.SetColor("_Color", new Color(
                            pbr.baseColorFactor[0], pbr.baseColorFactor[1],
                            pbr.baseColorFactor[2], pbr.baseColorFactor[3]));
                    }
                    else
                    {
                        m.SetColor("_Color", Color.white);
                    }

                    if (pbr.baseColorTexture != null)
                    {
                        var tex = GetTextureByIndex(gltf, textures, pbr.baseColorTexture.index);
                        if (tex != null)
                        {
                            m.SetTexture("_MainTex", tex);
                            Debug.Log($"  [Excelsior] Material '{m.name}' pbr baseColorTexture → _MainTex = {tex.name}");
                        }
                    }

                    m.SetFloat("_Metallic", pbr.metallicFactor);
                    m.SetFloat("_Glossiness", 1f - pbr.roughnessFactor);
                }

                // 3) Normal map (_BumpMap)
                if (mat.normalTexture != null)
                {
                    var tex = GetTextureByIndex(gltf, textures, mat.normalTexture.index);
                    if (tex != null)
                    {
                        // Re-import as normal map
                        string texPath = AssetDatabase.GetAssetPath(tex);
                        if (!string.IsNullOrEmpty(texPath))
                        {
                            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                            {
                                importer.textureType = TextureImporterType.NormalMap;
                                importer.SaveAndReimport();
                                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                            }
                        }
                        m.SetTexture("_BumpMap", tex);
                        m.EnableKeyword("_NORMALMAP");
                        Debug.Log($"  [Excelsior] Material '{m.name}' normalTexture → _BumpMap");
                    }
                }

                // 4) Emission map (_EmissionMap)
                if (mat.emissiveTexture != null)
                {
                    var tex = GetTextureByIndex(gltf, textures, mat.emissiveTexture.index);
                    if (tex != null)
                    {
                        m.SetTexture("_EmissionMap", tex);
                        m.SetColor("_EmissionColor", Color.white);
                        m.EnableKeyword("_EMISSION");
                        Debug.Log($"  [Excelsior] Material '{m.name}' emissiveTexture → _EmissionMap");
                    }
                }

                // 5) Occlusion map (_OcclusionMap)
                if (mat.occlusionTexture != null)
                {
                    var tex = GetTextureByIndex(gltf, textures, mat.occlusionTexture.index);
                    if (tex != null)
                    {
                        m.SetTexture("_OcclusionMap", tex);
                        m.EnableKeyword("_OCCLUSIONMAP");
                        Debug.Log($"  [Excelsior] Material '{m.name}' occlusionTexture → _OcclusionMap");
                    }
                }

                // Double-sided
                if (mat.doubleSided)
                    m.SetInt("_Cull", 0);

                result.Add(m);
                Debug.Log($"  [Excelsior] Material '{m.name}': hasDiffuse={hasDiffuse}, tex={m.GetTexture("_MainTex") != null}");
            }
            return result;
        }

        static Material GetDefaultMaterial(List<Material> materials)
        {
            if (materials.Count > 0) return materials[0];
            var m = new Material(Shader.Find("Standard"));
            m.SetColor("_Color", Color.gray);
            return m;
        }

        #endregion

        #region Hierarchy Builder

        static GameObject BuildHierarchy(GltfRoot gltf, byte[] binData, List<Material> materials, List<Mesh> meshAssets)
        {
            HashSet<int> childSet = new HashSet<int>();
            if (gltf.nodes != null)
            {
                foreach (var node in gltf.nodes)
                {
                    if (node.children != null)
                        foreach (int c in node.children)
                            childSet.Add(c);
                }
            }

            GameObject root = new GameObject("ExcelsiorModel");

            if (gltf.nodes == null) return root;

            for (int i = 0; i < gltf.nodes.Length; i++)
            {
                if (childSet.Contains(i)) continue;
                BuildNode(gltf, binData, i, root.transform, materials, meshAssets);
            }

            return root;
        }

        static void BuildNode(GltfRoot gltf, byte[] binData, int nodeIdx, Transform parent,
            List<Material> materials, List<Mesh> meshAssets)
        {
            var node = gltf.nodes[nodeIdx];
            GameObject go = new GameObject(node.name ?? $"Node_{nodeIdx}");
            go.transform.SetParent(parent, false);
            ApplyNodeTransform(node, go.transform);

            if (node.mesh >= 0 && node.mesh < gltf.meshes.Length)
            {
                var mesh = gltf.meshes[node.mesh];
                BuildMeshForNode(gltf, binData, mesh, go, materials, meshAssets);
            }

            if (node.children != null)
            {
                foreach (int childIdx in node.children)
                {
                    if (childIdx >= 0 && childIdx < gltf.nodes.Length)
                        BuildNode(gltf, binData, childIdx, go.transform, materials, meshAssets);
                }
            }
        }

        static void ApplyNodeTransform(GltfNode node, Transform t)
        {
            if (node.matrix != null && node.matrix.Length == 16)
            {
                var m = new Matrix4x4(
                    new Vector4(node.matrix[0], node.matrix[1], node.matrix[2], node.matrix[3]),
                    new Vector4(node.matrix[4], node.matrix[5], node.matrix[6], node.matrix[7]),
                    new Vector4(node.matrix[8], node.matrix[9], node.matrix[10], node.matrix[11]),
                    new Vector4(node.matrix[12], node.matrix[13], node.matrix[14], node.matrix[15])
                );
                t.localPosition = m.GetColumn(3);
                t.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                t.localScale = new Vector3(
                    m.GetColumn(0).magnitude,
                    m.GetColumn(1).magnitude,
                    m.GetColumn(2).magnitude
                );
            }
            else
            {
                if (node.translation != null && node.translation.Length >= 3)
                    t.localPosition = new Vector3(node.translation[0], node.translation[1], node.translation[2]);
                if (node.rotation != null && node.rotation.Length >= 4)
                    t.localRotation = new Quaternion(node.rotation[0], node.rotation[1], node.rotation[2], node.rotation[3]);
                if (node.scale != null && node.scale.Length >= 3)
                    t.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
            }
        }

        static void BuildMeshForNode(GltfRoot gltf, byte[] binData, GltfMesh gltfMesh,
            GameObject go, List<Material> materials, List<Mesh> meshAssets)
        {
            if (gltfMesh.primitives.Length == 1)
            {
                var mesh = BuildSinglePrimitive(gltf, binData, gltfMesh.primitives[0]);
                mesh.name = gltfMesh.name ?? "mesh";
                meshAssets.Add(mesh);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();

                int matIdx = gltfMesh.primitives[0].material;
                mr.sharedMaterial = (matIdx >= 0 && matIdx < materials.Count) ? materials[matIdx] : GetDefaultMaterial(materials);
            }
            else
            {
                var combinedMesh = BuildMultiPrimitive(gltf, binData, gltfMesh.primitives);
                combinedMesh.name = gltfMesh.name ?? "mesh";
                meshAssets.Add(combinedMesh);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = combinedMesh;
                var mr = go.AddComponent<MeshRenderer>();

                var subMats = new Material[gltfMesh.primitives.Length];
                for (int i = 0; i < gltfMesh.primitives.Length; i++)
                {
                    int matIdx = gltfMesh.primitives[i].material;
                    subMats[i] = (matIdx >= 0 && matIdx < materials.Count) ? materials[matIdx] : GetDefaultMaterial(materials);
                }
                mr.sharedMaterials = subMats;
            }
        }

        static Mesh BuildSinglePrimitive(GltfRoot gltf, byte[] binData, GltfPrimitive prim)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            if (prim.attributes != null && prim.attributes.POSITION >= 0)
                verts = ReadVec3(gltf, prim.attributes.POSITION, binData);
            if (prim.attributes != null && prim.attributes.NORMAL >= 0)
                norms = ReadVec3(gltf, prim.attributes.NORMAL, binData);
            if (prim.attributes != null && prim.attributes.TEXCOORD_0 >= 0)
                uvs = ReadVec2(gltf, prim.attributes.TEXCOORD_0, binData);

            if (prim.indices >= 0)
                tris = ReadInts(gltf, prim.indices, binData);
            else
            {
                for (int i = 0; i < verts.Count; i += 3)
                {
                    tris.Add(i);
                    tris.Add(i + 1);
                    tris.Add(i + 2);
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            if (norms.Count == verts.Count) mesh.normals = norms.ToArray();
            else mesh.RecalculateNormals();
            if (uvs.Count == verts.Count) mesh.uv = uvs.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildMultiPrimitive(GltfRoot gltf, byte[] binData, GltfPrimitive[] prims)
        {
            List<Vector3> allVerts = new List<Vector3>();
            List<Vector3> allNorms = new List<Vector3>();
            List<Vector2> allUVs = new List<Vector2>();
            List<List<int>> subTris = new List<List<int>>();

            foreach (var prim in prims)
            {
                int start = allVerts.Count;
                if (prim.attributes != null && prim.attributes.POSITION >= 0)
                    allVerts.AddRange(ReadVec3(gltf, prim.attributes.POSITION, binData));
                if (prim.attributes != null && prim.attributes.NORMAL >= 0)
                    allNorms.AddRange(ReadVec3(gltf, prim.attributes.NORMAL, binData));
                if (prim.attributes != null && prim.attributes.TEXCOORD_0 >= 0)
                    allUVs.AddRange(ReadVec2(gltf, prim.attributes.TEXCOORD_0, binData));

                List<int> tri = new List<int>();
                if (prim.indices >= 0)
                {
                    var indices = ReadInts(gltf, prim.indices, binData);
                    foreach (int idx in indices) tri.Add(start + idx);
                }
                else
                {
                    int count = allVerts.Count - start;
                    for (int i = 0; i < count; i += 3)
                    {
                        tri.Add(start + i);
                        tri.Add(start + i + 1);
                        tri.Add(start + i + 2);
                    }
                }
                subTris.Add(tri);
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = allVerts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(allVerts);
            mesh.subMeshCount = subTris.Count;
            for (int i = 0; i < subTris.Count; i++)
                mesh.SetTriangles(subTris[i], i);
            if (allNorms.Count == allVerts.Count) mesh.normals = allNorms.ToArray();
            else mesh.RecalculateNormals();
            if (allUVs.Count == allVerts.Count) mesh.uv = allUVs.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        #endregion

        #region Data Readers

        static List<Vector3> ReadVec3(GltfRoot gltf, int accessorIdx, byte[] binData)
        {
            var result = new List<Vector3>();
            if (accessorIdx < 0 || accessorIdx >= gltf.accessors.Length) return result;
            var acc = gltf.accessors[accessorIdx];
            var bv = gltf.bufferViews[acc.bufferView];
            int off = acc.byteOffset + bv.byteOffset;
            for (int i = 0; i < acc.count; i++)
            {
                float x = System.BitConverter.ToSingle(binData, off + i * 12);
                float y = System.BitConverter.ToSingle(binData, off + i * 12 + 4);
                float z = System.BitConverter.ToSingle(binData, off + i * 12 + 8);
                result.Add(new Vector3(x, y, z));
            }
            return result;
        }

        static List<Vector2> ReadVec2(GltfRoot gltf, int accessorIdx, byte[] binData)
        {
            var result = new List<Vector2>();
            if (accessorIdx < 0 || accessorIdx >= gltf.accessors.Length) return result;
            var acc = gltf.accessors[accessorIdx];
            var bv = gltf.bufferViews[acc.bufferView];
            int off = acc.byteOffset + bv.byteOffset;
            for (int i = 0; i < acc.count; i++)
            {
                float x = System.BitConverter.ToSingle(binData, off + i * 8);
                float y = System.BitConverter.ToSingle(binData, off + i * 8 + 4);
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        static List<int> ReadInts(GltfRoot gltf, int accessorIdx, byte[] binData)
        {
            var result = new List<int>();
            if (accessorIdx < 0 || accessorIdx >= gltf.accessors.Length) return result;
            var acc = gltf.accessors[accessorIdx];
            var bv = gltf.bufferViews[acc.bufferView];
            int off = acc.byteOffset + bv.byteOffset;
            if (acc.componentType == 5123)
                for (int i = 0; i < acc.count; i++) result.Add(System.BitConverter.ToUInt16(binData, off + i * 2));
            else if (acc.componentType == 5125)
                for (int i = 0; i < acc.count; i++) result.Add((int)System.BitConverter.ToUInt32(binData, off + i * 4));
            else if (acc.componentType == 5121)
                for (int i = 0; i < acc.count; i++) result.Add(binData[off + i]);
            return result;
        }

        #endregion

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    #region GLTF Serializable Types

    [System.Serializable]
    public class GltfRoot
    {
        public GltfMesh[] meshes;
        public GltfAccessor[] accessors;
        public GltfBufferView[] bufferViews;
        public GltfBuffer[] buffers;
        public GltfNode[] nodes;
        public GltfImage[] images;
        public GltfTexture[] textures;
        public GltfMaterial[] materials;
    }

    [System.Serializable]
    public class GltfNode
    {
        public string name;
        public int mesh;
        public float[] matrix;
        public int[] children;
        public float[] translation;
        public float[] rotation;
        public float[] scale;
    }

    [System.Serializable]
    public class GltfMesh
    {
        public string name;
        public GltfPrimitive[] primitives;
    }

    [System.Serializable]
    public class GltfPrimitive
    {
        public GltfAttributes attributes;
        public int indices;
        public int material;
    }

    [System.Serializable]
    public class GltfAttributes
    {
        public int POSITION;
        public int NORMAL;
        public int TEXCOORD_0;
    }

    [System.Serializable]
    public class GltfAccessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
    }

    [System.Serializable]
    public class GltfBufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int target;
    }

    [System.Serializable]
    public class GltfBuffer
    {
        public int byteLength;
    }

    [System.Serializable]
    public class GltfImage
    {
        public int bufferView;
        public string mimeType;
        public string name;
    }

    [System.Serializable]
    public class GltfTexture
    {
        public int source;
    }

    [System.Serializable]
    public class GltfMaterial
    {
        public string name;
        public GltfPbrMetallicRoughness pbrMetallicRoughness;
        public GltfTextureInfo normalTexture;
        public GltfTextureInfo occlusionTexture;
        public GltfTextureInfo emissiveTexture;
        public float[] emissiveFactor;
        public string alphaMode;
        public GltfMaterialExtensions extensions;
        public bool doubleSided;
    }

    [System.Serializable]
    public class GltfMaterialExtensions
    {
        public GltfPbrSpecularGlossiness specGloss;
    }

    [System.Serializable]
    public class GltfPbrSpecularGlossiness
    {
        public float[] diffuseFactor;
        public GltfTextureInfo diffuseTexture;
        public GltfTextureInfo specularGlossinessTexture;
        public float glossinessFactor;
    }

    [System.Serializable]
    public class GltfPbrMetallicRoughness
    {
        public float[] baseColorFactor;
        public GltfTextureInfo baseColorTexture;
        public GltfTextureInfo metallicRoughnessTexture;
        public float metallicFactor;
        public float roughnessFactor;
    }

    [System.Serializable]
    public class GltfTextureInfo
    {
        public int index;
    }

    #endregion
}
