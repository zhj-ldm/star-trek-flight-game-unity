using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace StarTrekCombat
{
    /// <summary>
    /// Imports STL (binary and ASCII) and OBJ files into Unity Mesh assets.
    /// Access via menu: Tools > Star Trek > Import Ship Models
    /// </summary>
    public class STLImporter : EditorWindow
    {
        private string _sourceDir = @"D:\DESK-P\StarTrek-Ship-Models\新建文件夹";
        private string _outputDir = "Assets/Models/EnemyShips";

        [MenuItem("Tools/Star Trek/Import Ship Models")]
        public static void ShowWindow()
        {
            GetWindow<STLImporter>("STL/OBJ Importer");
        }

        void OnGUI()
        {
            GUILayout.Label("Import Ship Models", EditorStyles.boldLabel);
            _sourceDir = EditorGUILayout.TextField("Source Directory", _sourceDir);
            _outputDir = EditorGUILayout.TextField("Output Directory", _outputDir);

            if (GUILayout.Button("Import All Models"))
            {
                ImportAllModels(_sourceDir, _outputDir);
            }
        }

        public static void ImportAllModels(string sourceDir, string outputDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"Source directory not found: {sourceDir}");
                return;
            }

            // Ensure output directory exists
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                string parent = Path.GetDirectoryName(outputDir);
                string folderName = Path.GetFileName(outputDir);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    string pp = Path.GetDirectoryName(parent);
                    string pn = Path.GetFileName(parent);
                    if (!AssetDatabase.IsValidFolder(pp))
                        AssetDatabase.CreateFolder("Assets", pn);
                    else
                        AssetDatabase.CreateFolder(pp, pn);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }

            string[] files = Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly);
            int imported = 0;

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".stl" && ext != ".obj") continue;

                string name = Path.GetFileNameWithoutExtension(file);
                Mesh mesh = null;

                if (ext == ".stl")
                    mesh = ParseSTL(file);
                else if (ext == ".obj")
                    mesh = ParseOBJ(file);

                if (mesh != null)
                {
                    mesh.name = name;
                    string assetPath = $"{outputDir}/{name}.asset";
                    AssetDatabase.CreateAsset(mesh, assetPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Imported mesh: {name} ({mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles)");
                    imported++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"STLImporter: Imported {imported} models to {outputDir}");
        }

        #region STL Parser

        private static Mesh ParseSTL(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);

            // Try binary first (standard STL binary format)
            // Binary STL: 80-byte header + 4-byte triangle count + triangles
            // Each triangle: 12 floats (normal + 3 vertices) + 2 bytes attribute = 50 bytes
            int expectedTriangles = System.BitConverter.ToInt32(data, 80);
            long expectedSize = 84 + (expectedTriangles * 50);

            if (data.Length == expectedSize)
            {
                return ParseBinarySTL(data, expectedTriangles);
            }

            // Try ASCII STL
            string text = Encoding.ASCII.GetString(data);
            if (text.Contains("solid") && text.Contains("facet"))
            {
                return ParseASCIISTL(text);
            }

            // Fallback: try binary anyway
            if (data.Length > 84)
            {
                int triCount = data.Length <= 84 + 4 ? 0 : expectedTriangles;
                if (triCount > 0 && triCount < 5000000)
                    return ParseBinarySTL(data, triCount);
            }

            Debug.LogError($"Failed to parse STL file: {filePath}");
            return null;
        }

        private static Mesh ParseBinarySTL(byte[] data, int triangleCount)
        {
            List<Vector3> vertices = new List<Vector3>(triangleCount * 3);
            List<int> triangles = new List<int>(triangleCount * 3);
            HashSet<long> vertexSet = new HashSet<long>();

            int offset = 84;
            for (int i = 0; i < triangleCount; i++)
            {
                // Skip normal (12 bytes)
                offset += 12;

                for (int j = 0; j < 3; j++)
                {
                    float x = System.BitConverter.ToSingle(data, offset);
                    float y = System.BitConverter.ToSingle(data, offset + 4);
                    float z = System.BitConverter.ToSingle(data, offset + 8);
                    offset += 12;

                    // Deduplicate vertices
                    long key = ((long)(x * 1000) & 0xFFFFFF) |
                               ((long)(y * 1000) & 0xFFFFFF) << 24 |
                               ((long)(z * 1000) & 0xFFFFFF) << 48;

                    if (vertexSet.Contains(key))
                    {
                        // Find existing vertex (linear search is slow but ok for import)
                        bool found = false;
                        for (int k = vertices.Count - 1; k >= 0 && k >= vertices.Count - 100; k--)
                        {
                            if (Vector3.SqrMagnitude(vertices[k] - new Vector3(x, y, z)) < 0.0001f)
                            {
                                triangles.Add(k);
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            vertices.Add(new Vector3(x, y, z));
                            triangles.Add(vertices.Count - 1);
                        }
                    }
                    else
                    {
                        vertexSet.Add(key);
                        vertices.Add(new Vector3(x, y, z));
                        triangles.Add(vertices.Count - 1);
                    }
                }

                // Skip attribute byte count
                offset += 2;
            }

            return BuildMesh(vertices, triangles);
        }

        private static Mesh ParseASCIISTL(string text)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            string[] lines = text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("vertex"))
                {
                    string[] parts = trimmed.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x, y, z;
                        if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) &&
                            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                        {
                            vertices.Add(new Vector3(x, y, z));
                            triangles.Add(vertices.Count - 1);
                        }
                    }
                }
            }

            return BuildMesh(vertices, triangles);
        }

        #endregion

        #region OBJ Parser

        private static Mesh ParseOBJ(string filePath)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("v "))
                {
                    string[] parts = trimmed.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x, y, z;
                        if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) &&
                            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                        {
                            vertices.Add(new Vector3(x, y, z));
                        }
                    }
                }
                else if (trimmed.StartsWith("f "))
                {
                    string[] parts = trimmed.Substring(2).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    List<int> faceIndices = new List<int>();

                    foreach (string p in parts)
                    {
                        // Handle v/vt/vn format
                        string vIdx = p.Split('/')[0];
                        int idx;
                        if (int.TryParse(vIdx, out idx))
                        {
                            if (idx > 0) idx--; // OBJ is 1-based
                            else if (idx < 0) idx = vertices.Count + idx; // Negative indices
                            faceIndices.Add(idx);
                        }
                    }

                    // Triangulate face (fan triangulation)
                    if (faceIndices.Count >= 3)
                    {
                        for (int i = 1; i < faceIndices.Count - 1; i++)
                        {
                            triangles.Add(faceIndices[0]);
                            triangles.Add(faceIndices[i]);
                            triangles.Add(faceIndices[i + 1]);
                        }
                    }
                }
            }

            return BuildMesh(vertices, triangles);
        }

        #endregion

        private static Mesh BuildMesh(List<Vector3> vertices, List<int> triangles)
        {
            if (vertices.Count == 0 || triangles.Count == 0)
            {
                Debug.LogError("No geometry found in mesh data");
                return null;
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
