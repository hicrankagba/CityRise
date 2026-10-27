using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Poly Haven (CC0) modellerini oyuna hazırlar: her model klasöründeki FBX'i alır, glTF dosyasındaki
    /// malzeme bilgisine göre URP Lit malzemeleri kurar (renk, normal, ARM → metal/pürüzsüzlük + ortam gölgesi)
    /// ve Generated/PolyHaven altına prefab kaydeder. Model 1 birim = 1 metre olacak şekilde ölçeklenir.
    /// </summary>
    public static class PolyHavenModels
    {
        const string ModelsDir = "Assets/CityRise/ThirdParty/PolyHaven/Models";
        const string OutDir = "Assets/CityRise/Generated/PolyHaven";
        const string Version = "v2";

        [MenuItem("CityRise/Poly Haven Modellerini Hazırla", priority = 31)]
        public static void RebuildAll()
        {
            AssetDatabase.DeleteAsset(OutDir);
            Ensure();
        }

        /// <summary>Hazır prefabları döndürür (yoksa üretir). Anahtar: klasör adı.</summary>
        public static Dictionary<string, GameObject> Ensure()
        {
            var result = new Dictionary<string, GameObject>();
            if (!AssetDatabase.IsValidFolder(ModelsDir)) return result;
            EnsureFolder(OutDir);
            foreach (var dir in Directory.GetDirectories(FullPath(ModelsDir)))
            {
                string name = Path.GetFileName(dir);
                string prefabPath = $"{OutDir}/{name}_{Version}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    try { prefab = Build(name, prefabPath); }
                    catch (Exception e) { Debug.LogWarning($"CityRise: {name} hazırlanamadı: {e.Message}"); }
                }
                if (prefab != null) result[name] = prefab;
            }
            return result;
        }

        [Serializable] class GltfTexInfo { public int index = -1; }
        [Serializable] class GltfPbr { public GltfTexInfo baseColorTexture; public GltfTexInfo metallicRoughnessTexture; public float metallicFactor = 1f; public float roughnessFactor = 1f; }
        [Serializable] class GltfMaterial { public string name; public GltfPbr pbrMetallicRoughness; public GltfTexInfo normalTexture; public string alphaMode; public bool doubleSided; }
        [Serializable] class GltfTexture { public int source; }
        [Serializable] class GltfImage { public string uri; }
        [Serializable] class Gltf { public GltfMaterial[] materials; public GltfTexture[] textures; public GltfImage[] images; }

        static GameObject Build(string name, string prefabPath)
        {
            string folder = $"{ModelsDir}/{name}";
            string fbxPath = $"{folder}/{name}.fbx";
            // Raf katlarını bulmak için oyun sırasında çarpışma ağı gerekir; ağ okunabilir olsun.
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) throw new Exception("FBX yok");
            var gltf = JsonUtility.FromJson<Gltf>(File.ReadAllText(FullPath($"{folder}/{name}.gltf.json")));

            string Image(GltfTexInfo t)
            {
                if (t == null || t.index < 0 || gltf.textures == null || t.index >= gltf.textures.Length) return null;
                var uri = gltf.images[gltf.textures[t.index].source].uri;
                return $"{folder}/{uri}";
            }

            var mats = new Dictionary<string, Material>();
            EnsureFolder($"{OutDir}/Malzemeler");
            if (gltf.materials != null)
                foreach (var gm in gltf.materials)
                    mats[gm.name] = MakeMaterial(name, gm, Image(gm.pbrMetallicRoughness?.baseColorTexture), Image(gm.normalTexture), Image(gm.pbrMetallicRoughness?.metallicRoughnessTexture));

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var root = new GameObject(name);
            inst.transform.SetParent(root.transform, false);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    string key = ms[i] != null ? ms[i].name : "";
                    if (!mats.TryGetValue(key, out var m) && mats.Count > 0)
                    {
                        // Ad eşleşmezse en yakın adı (ya da tek malzemeyi) kullan.
                        foreach (var kv in mats) if (key.Contains(kv.Key) || kv.Key.Contains(key)) { m = kv.Value; break; }
                        if (m == null) foreach (var kv in mats) { m = kv.Value; break; }
                    }
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
            }

            // Ölçek: Poly Haven modelleri metre cinsinden; FBX dışa aktarım çarpanı farklıysa (ör. 100) düzelt.
            var b = Bounds(root);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 20f) inst.transform.localScale *= 0.01f;
            b = Bounds(root);
            // Zemine otur, ortala.
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Material MakeMaterial(string asset, GltfMaterial gm, string diff, string nor, string arm)
        {
            string safe = string.Join("_", (asset + "_" + gm.name).Split(Path.GetInvalidFileNameChars()));
            string path = $"{OutDir}/Malzemeler/{safe}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safe };
            AssetDatabase.CreateAsset(mat, path);
            mat.enableInstancing = true;
            var baseTex = Tex(diff, false);
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            var norTex = Tex(nor, true);
            if (norTex != null) { mat.SetTexture("_BumpMap", norTex); mat.EnableKeyword("_NORMALMAP"); }
            float metal = gm.pbrMetallicRoughness != null ? gm.pbrMetallicRoughness.metallicFactor : 0f;
            float rough = gm.pbrMetallicRoughness != null ? gm.pbrMetallicRoughness.roughnessFactor : 1f;
            if (!string.IsNullOrEmpty(arm) && File.Exists(FullPath(arm)))
            {
                var (ms, ao) = ArmMaps(arm);
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetTexture("_OcclusionMap", ao);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                mat.SetFloat("_Metallic", metal);
                mat.SetFloat("_Smoothness", 1f - rough);
            }
            if (gm.alphaMode == "MASK" || gm.alphaMode == "BLEND")
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.4f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            if (gm.doubleSided) mat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>glTF ARM (R=ortam gölgesi, G=pürüzlülük, B=metal) → URP metal/pürüzsüzlük ve AO dokuları.</summary>
        static (Texture2D ms, Texture2D ao) ArmMaps(string armPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(armPath);
            string msPath = $"{OutDir}/Malzemeler/{baseName}_MS.png";
            string aoPath = $"{OutDir}/Malzemeler/{baseName}_AO.png";
            if (!File.Exists(FullPath(msPath)))
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(FullPath(armPath)));
                var px = src.GetPixels32();
                var ms = new Color32[px.Length];
                var ao = new Color32[px.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    ms[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g));
                    ao[i] = new Color32(px[i].r, px[i].r, px[i].r, 255);
                }
                Save(msPath, ms, src.width, src.height);
                Save(aoPath, ao, src.width, src.height);
                Object.DestroyImmediate(src);
            }
            return (Tex(msPath, false, linear: true), Tex(aoPath, false, linear: true));
        }

        static void Save(string path, Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            File.WriteAllBytes(FullPath(path), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static Texture2D Tex(string path, bool normal, bool linear = false)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            bool changed = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != type) { imp.textureType = type; changed = true; }
            if (!normal && imp.sRGBTexture == linear) { imp.sRGBTexture = !linear; changed = true; }
            if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; changed = true; }
            if (changed) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string FullPath(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
