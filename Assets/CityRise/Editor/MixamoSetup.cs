using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Mixamo karakterlerini oyuna hazırlar. Mixamo'nun büyük FBX dosyaları (dokular gömülü, ~55 MB)
    /// projeye konmaz; buradan hafif sürümler üretilir: ağ, insan iskeleti (Avatar), 1k dokulu URP malzeme
    /// ve prefab. Animasyonlar küçük FBX'lerden alınır ve tek bir Animator'da toplanır.
    /// Kaynak FBX'ler Mixamo/Kaynak altına geçici olarak konup "CityRise > Mixamo Karakterlerini Hazırla" çalıştırılır.
    /// </summary>
    public static class MixamoSetup
    {
        public const string Root = "Assets/CityRise/ThirdParty/Mixamo";
        const string SourceDir = Root + "/Kaynak";
        const string AnimDir = Root + "/Animasyonlar";
        public const string OutDir = Root + "/Hazir";
        public const string ControllerPath = OutDir + "/Insan.controller";
        const float TargetHeight = 1.72f;

        /// <summary>Animator durumları ve animasyon dosyası adındaki karşılıkları.</summary>
        public static readonly (string state, string file)[] States =
        {
            ("Idle", "Idle"),
            ("Walk", "Walking"),
            ("PickUp", "Picking Up Object"),
            ("Phone", "Talking On Phone"),
            ("Sit", "Sitting Talking"),
        };

        [MenuItem("CityRise/Mixamo Karakterlerini Hazırla", priority = 32)]
        public static void Run()
        {
            EnsureFolder(OutDir);
            var controller = BuildController();
            foreach (var path in Directory.GetFiles(FullPath(SourceDir), "*.fbx"))
            {
                string asset = SourceDir + "/" + Path.GetFileName(path);
                string name = Path.GetFileNameWithoutExtension(path).Split('@')[0].Replace("_nonPBR", "");
                BuildCharacter(asset, name, controller);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CityRise: Mixamo karakterleri hazır.");
        }

        /// <summary>Hazır karakter prefabları (oyun kurulurken kullanılır).</summary>
        public static List<GameObject> Characters()
        {
            var list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(OutDir)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutDir }))
                list.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            return list;
        }

        // ------------------------------------------------------------ Animasyonlar

        static AnimatorController BuildController()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var path in Directory.GetFiles(FullPath(AnimDir), "*.fbx"))
            {
                string asset = AnimDir + "/" + Path.GetFileName(path);
                var imp = (ModelImporter)AssetImporter.GetAtPath(asset);
                string label = Path.GetFileNameWithoutExtension(path).Split('@')[1];
                bool loop = label != "Picking Up Object";
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importBlendShapes = false;
                var src = imp.defaultClipAnimations;
                if (src.Length > 0)
                {
                    var c = src[0];
                    c.name = label;
                    c.loopTime = loop;
                    // Yerinde yürüme: kök hareketi poza gömülür, karakter kodla ilerletilir.
                    c.lockRootPositionXZ = true;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true;
                    imp.clipAnimations = new[] { c };
                }
                imp.SaveAndReimport();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(asset))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) { clips[label] = clip; break; }
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var (state, file) in States)
            {
                var s = sm.AddState(state);
                if (clips.TryGetValue(file, out var clip)) s.motion = clip;
                if (state == "Idle") sm.defaultState = s;
            }
            return controller;
        }

        // ------------------------------------------------------------ Karakterler

        static void BuildCharacter(string fbx, string name, AnimatorController controller)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = false;
                imp.SaveAndReimport();
            }
            string texDir = SourceDir + "/Dokular_" + name;
            EnsureFolder(texDir);
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();

            string dir = OutDir + "/" + name;
            AssetDatabase.DeleteAsset(dir);
            EnsureFolder(dir);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = Object.Instantiate(model);
            inst.name = "Insan_" + name;
            var avatar = AvatarBuilder.BuildHumanAvatar(inst, imp.humanDescription);
            avatar.name = name + "_Avatar";
            AssetDatabase.CreateAsset(avatar, $"{dir}/{name}_Avatar.asset");

            var matCache = new Dictionary<Material, Material>();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = Object.Instantiate(smr.sharedMesh);
                mesh.name = smr.sharedMesh.name;
                AssetDatabase.CreateAsset(mesh, $"{dir}/{name}_{Safe(mesh.name)}.asset");
                smr.sharedMesh = mesh;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!matCache.TryGetValue(m, out var u)) matCache[m] = u = UrpMaterial(m, dir, name, texDir);
                    mats[i] = u;
                }
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;
            }

            var animator = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Boy: 1,72 m.
            var b = Bounds(inst);
            float s = TargetHeight / Mathf.Max(0.1f, b.size.y);
            inst.transform.localScale *= s;

            PrefabUtility.SaveAsPrefabAsset(inst, $"{dir}/Insan_{name}.prefab");
            Object.DestroyImmediate(inst);
        }

        /// <summary>
        /// Mixamo dokuları iki sette gelir: 1001 = gövde ve kıyafet, 1002 = saç (saydam kenarlı).
        /// Malzeme adına göre doğru set seçilir.
        /// </summary>
        static Material UrpMaterial(Material src, string dir, string charName, string texDir)
        {
            var u = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = charName + "_" + src.name };
            bool hair = src.name.ToLower().Contains("hair") || src.name.ToLower().Contains("lash");
            string set = hair && File.Exists(FullPath($"{texDir}/{charName}_1002_Diffuse.png")) ? "1002" : "1001";
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{charName}_{set}_Diffuse.png");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{charName}_{set}_Normal.png");
            if (diff != null) u.SetTexture("_BaseMap", SmallCopy(diff, dir, charName, alpha: hair));
            u.SetColor("_BaseColor", Color.white);
            u.SetFloat("_Smoothness", hair ? 0.35f : 0.2f);
            u.SetFloat("_Metallic", 0f);
            if (nor != null)
            {
                u.SetTexture("_BumpMap", SmallCopy(nor, dir, charName, normal: true));
                u.EnableKeyword("_NORMALMAP");
            }
            if (hair)
            {
                u.SetFloat("_AlphaClip", 1f);
                u.SetFloat("_Cutoff", 0.35f);
                u.EnableKeyword("_ALPHATEST_ON");
                u.SetFloat("_Cull", 0f);
                u.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            u.enableInstancing = true;
            AssetDatabase.CreateAsset(u, $"{dir}/{Safe(u.name)}.mat");
            return u;
        }

        /// <summary>Dokuyu 1024 piksele küçültür; saydamlık gerekmiyorsa JPG (depoyu küçük tutmak için).</summary>
        static Texture2D SmallCopy(Texture2D tex, string dir, string charName, bool normal = false, bool alpha = false)
        {
            string srcPath = AssetDatabase.GetAssetPath(tex);
            string outPath = $"{dir}/{Safe(tex.name)}.{(alpha ? "png" : "jpg")}";
            if (!File.Exists(FullPath(outPath)))
            {
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                raw.LoadImage(File.ReadAllBytes(FullPath(srcPath)));
                int size = Mathf.Min(1024, Mathf.Max(raw.width, raw.height));
                var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, normal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                Graphics.Blit(raw, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, normal);
                small.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(FullPath(outPath), alpha ? small.EncodeToPNG() : small.EncodeToJPG(88));
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(small);
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
                if (normal) imp.textureType = TextureImporterType.NormalMap;
                if (alpha) imp.alphaIsTransparency = true;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static string Safe(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));

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
