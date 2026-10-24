using System.Collections.Generic;
using System.IO;
using CityRise.CameraRig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Menü: CityRise > Test Sokağını Oluştur.
    /// Işık, kamera ve renk ayarlarını denemek için geçici kutu binalarla bir sokak kurar.
    /// Gökyüzü ve yüzey dokuları Poly Haven'dan (CC0) gelir; bkz. ThirdParty/PolyHaven/README.txt.
    /// Gerçekçi 3D modeller geldiğinde kutuların yerine onlar konacak.
    /// </summary>
    public static class TestStreetBuilder
    {
        const string Root = "Assets/CityRise";
        const string GeneratedDir = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/TestSokak.unity";
        const string PolyHaven = Root + "/ThirdParty/PolyHaven";

        // Cephe dokusu bir "modül": 4 pencere eninde, 2 kat boyunda.
        const float FloorHeight = 3.2f;
        const float ModuleWidth = 4 * FloorHeight;
        const float ModuleHeight = 2 * FloorHeight;
        const int ModulePxW = 2048;
        const int ModulePxH = 1024;
        const int WindowLightVariants = 3;
        // Doku üretimi değişirse bu sayıyı artır; eski dosyalar yerine yenileri üretilir.
        const string GenVersion = "v1";

        struct Surface
        {
            public string name;
            public float meters;
            public Surface(string name, float meters) { this.name = name; this.meters = meters; }
            public string Diff => $"{PolyHaven}/Textures/{name}_diff_2k.jpg";
            public string Normal => $"{PolyHaven}/Textures/{name}_nor_gl_2k.jpg";
            public string Rough => $"{PolyHaven}/Textures/{name}_rough_2k.jpg";
        }

        static readonly Surface Asphalt = new Surface("asphalt_02", 3f);
        static readonly Surface Paving = new Surface("hexagonal_concrete_paving", 1.6f);
        static readonly Surface Brick = new Surface("red_brick_03", 1f);
        static readonly Surface Plaster = new Surface("painted_plaster_wall", 2f);
        static readonly Surface Grass = new Surface("leafy_grass", 2f);

        [MenuItem("CityRise/Test Sokağını Oluştur (Gece)")]
        public static void BuildNight() => Build(night: true);

        [MenuItem("CityRise/Test Sokağını Oluştur (Gündüz)")]
        public static void BuildDay() => Build(night: false);

        static void Build(bool night)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(GeneratedDir);
            EnsureFolder(Root + "/Scenes");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.productName = "CityRise";

            try
            {
                EditorUtility.DisplayProgressBar("CityRise", "Dokular hazırlanıyor...", 0.1f);
                var asphalt = SurfaceMat("Asfalt", Asphalt, new Color(0.9f, 0.9f, 0.9f));
                var sidewalk = SurfaceMat("Kaldirim", Paving, Color.white);
                var grass = SurfaceMat("Cim", Grass, new Color(0.85f, 0.9f, 0.85f));
                var roof = SurfaceMat("Cati", Plaster, new Color(0.45f, 0.45f, 0.47f));
                var lineMat = Mat("YolCizgisi", new Color(0.9f, 0.85f, 0.6f), 0.2f);
                var lampMat = Mat("Direk", new Color(0.15f, 0.15f, 0.16f), 0.6f);
                var lampGlow = Mat("LambaIsigi", new Color(1f, 0.85f, 0.6f), 0.5f, new Color(1f, 0.75f, 0.45f) * 4f);

                EditorUtility.DisplayProgressBar("CityRise", "Bina cepheleri hazırlanıyor...", 0.4f);
                var brickFacade = Facade("Tugla", Brick);
                var plasterFacade = Facade("Siva", Plaster);

                EditorUtility.DisplayProgressBar("CityRise", "Sahne kuruluyor...", 0.8f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var city = new GameObject("Sehir").transform;

                Box("Zemin", city, new Vector3(0, -0.05f, 0), new Vector3(600, 0.1f, 600), grass);
                Box("Yol", city, new Vector3(0, 0, 0), new Vector3(200, 0.1f, 10), asphalt);
                Box("CaprazYol", city, new Vector3(0, 0.001f, 0), new Vector3(10, 0.1f, 200), asphalt);
                Box("KaldirimKuzey", city, new Vector3(0, 0.1f, 7), new Vector3(200, 0.2f, 4), sidewalk);
                Box("KaldirimGuney", city, new Vector3(0, 0.1f, -7), new Vector3(200, 0.2f, 4), sidewalk);
                for (int x = -95; x < 95; x += 8)
                {
                    if (Mathf.Abs(x) < 8) continue;
                    Box("Serit", city, new Vector3(x, 0.06f, 0), new Vector3(4, 0.02f, 0.25f), lineMat);
                }

                // Sokağın iki yanına farklı yükseklikte binalar.
                // Genişlik ve derinlik pencere aralığının katı; böylece pencereler kenarda kesilmez.
                var plasterTints = new[]
                {
                    new Color(0.95f, 0.90f, 0.78f), new Color(0.93f, 0.82f, 0.70f), new Color(0.85f, 0.86f, 0.86f),
                    new Color(0.96f, 0.93f, 0.86f), new Color(0.90f, 0.78f, 0.66f), new Color(0.80f, 0.84f, 0.88f),
                };
                var rng = new System.Random(42);
                int index = 0;
                foreach (int side in new[] { 1, -1 })
                {
                    for (float x = -70; x <= 70; x += 13)
                    {
                        if (Mathf.Abs(x) < 9) continue;
                        float w = FloorHeight * 3;
                        float d = FloorHeight * (3 + rng.Next(0, 2));
                        int floors = 2 + rng.Next(0, x > 30 || x < -30 ? 10 : 5);
                        float h = floors * FloorHeight;
                        bool brick = rng.NextDouble() < 0.4;
                        var tint = brick
                            ? Color.Lerp(new Color(0.8f, 0.78f, 0.76f), Color.white, (float)rng.NextDouble())
                            : plasterTints[rng.Next(plasterTints.Length)];
                        var offset = new Vector2(rng.Next(0, 4) * 0.25f, rng.Next(0, 2) * 0.5f);
                        var mat = BuildingMat("Bina" + index++, brick ? brickFacade : plasterFacade, tint, rng.Next(WindowLightVariants), offset, night);
                        var b = Box("Bina", city, new Vector3(x, h / 2f + 0.2f, side * (9 + d / 2f)), new Vector3(w, h, d), mat);
                        Box("Cati", b.transform.parent, new Vector3(x, h + 0.35f, side * (9 + d / 2f)), new Vector3(w + 0.4f, 0.3f, d + 0.4f), roof);
                    }
                }

                // Sokak lambaları: mobilde performans için az sayıda gerçek ışık.
                int lights = 0;
                for (float x = -60; x <= 60; x += 20)
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        Box("Direk", city, new Vector3(x, 2.5f, side * 5.6f), new Vector3(0.15f, 5f, 0.15f), lampMat);
                        Box("Lamba", city, new Vector3(x, 5f, side * 5.2f), new Vector3(0.6f, 0.15f, 0.6f), lampGlow);
                        if (night && lights < 8 && side == 1)
                        {
                            var l = new GameObject("LambaIsigi").AddComponent<Light>();
                            l.transform.SetParent(city);
                            l.transform.position = new Vector3(x, 4.8f, side * 4.5f);
                            l.type = LightType.Point;
                            l.range = 14f;
                            l.intensity = 6f;
                            l.color = new Color(1f, 0.78f, 0.5f);
                            lights++;
                        }
                    }
                }

                // Güneş ya da ay.
                var sun = new GameObject(night ? "Ay" : "Gunes").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(night ? 35f : 50f, -35f, 0);
                sun.color = night ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.95f, 0.85f);
                sun.intensity = night ? 0.25f : 1.6f;

                // Gökyüzü: gerçek fotoğraftan (HDRI) gelen ışık ve yansımalar.
                RenderSettings.skybox = SkyboxMat(night);
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity = 1f;
                RenderSettings.sun = sun;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = night ? new Color(0.05f, 0.06f, 0.11f) : new Color(0.75f, 0.8f, 0.88f);
                RenderSettings.fogStartDistance = 100f;
                RenderSettings.fogEndDistance = 300f;
                DynamicGI.UpdateEnvironment();

                // Kamera.
                var rig = new GameObject("KameraRig");
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(rig.transform);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 30f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.Skybox;
                var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var controller = rig.AddComponent<CityCameraController>();
                controller.cam = cam;
                controller.distance = 70f;

                // Renk ve ışık efektleri.
                var volumeGo = new GameObject("RenkAyarlari");
                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                string profilePath = $"{GeneratedDir}/RenkProfili_{(night ? "Gece" : "Gunduz")}.asset";
                AssetDatabase.DeleteAsset(profilePath);
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var tonemap = profile.Add<Tonemapping>(true);
                tonemap.mode.Override(TonemappingMode.ACES);
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(night ? 1.2f : 0.3f);
                bloom.threshold.Override(night ? 0.8f : 1.1f);
                bloom.scatter.Override(0.7f);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(night ? 0.3f : 0.1f);
                color.contrast.Override(12f);
                color.saturation.Override(night ? -5f : 5f);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.25f);
                foreach (var component in profile.components)
                {
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                volume.sharedProfile = profile;
                EditorUtility.SetDirty(profile);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild(ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("CityRise: Test sokağı hazır. Game sekmesinde Play tuşuna basıp fareyle sürükleyerek ve tekerlekle deneyebilirsin.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Kutu ağacı. UV'ler metre cinsindendir; böylece doku ölçeği kutunun boyundan bağımsız
        /// ve gerçek ölçüde kalır (malzeme tiling'i = 1 / dokunun kapladığı metre).
        /// </summary>
        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = MetricBox(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static Mesh MetricBox(Vector3 size)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var half = size * 0.5f;

            // u ve v, yüze dışarıdan bakan için sağ ve yukarı yönleridir.
            void Face(Vector3 n, Vector3 u, Vector3 v, float uLen, float vLen)
            {
                var c = Vector3.Scale(n, half);
                var hu = u * uLen * 0.5f;
                var hv = v * vLen * 0.5f;
                int i = verts.Count;
                verts.Add(c - hu - hv); uvs.Add(new Vector2(0, 0));
                verts.Add(c + hu - hv); uvs.Add(new Vector2(uLen, 0));
                verts.Add(c + hu + hv); uvs.Add(new Vector2(uLen, vLen));
                verts.Add(c - hu + hv); uvs.Add(new Vector2(0, vLen));
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.AddRange(new[] { i, i + 3, i + 2, i, i + 2, i + 1 });
            }

            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);

            var mesh = new Mesh { name = "Kutu" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material Mat(string name, Color baseColor, float smoothness, Color? emission = null)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            // Önceki kurulumdan kalan dokuları ve ayarları temizle.
            mat.shaderKeywords = new string[0];
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_BumpMap", null);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetTexture("_EmissionMap", null);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_Metallic", 0f);

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Gerçek ölçekli PBR zemin malzemesi (renk + normal + pürüzlülük).</summary>
        static Material SurfaceMat(string name, Surface s, Color tint)
        {
            var mat = Mat(name, tint, 1f);
            mat.SetTexture("_BaseMap", LoadTexture(s.Diff, TexKind.Color));
            mat.SetTexture("_BumpMap", LoadTexture(s.Normal, TexKind.Normal));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", MetalSmoothFromRough(s));
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTextureScale("_BaseMap", Vector2.one / s.meters);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        class FacadeTextures
        {
            public Texture2D albedo, normal, metalSmooth;
            public Texture2D[] lights;
        }

        static Material BuildingMat(string name, FacadeTextures f, Color tint, int lightVariant, Vector2 offset, bool night)
        {
            var mat = Mat(name, tint, 1f);
            mat.SetTexture("_BaseMap", f.albedo);
            mat.SetTexture("_BumpMap", f.normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", f.metalSmooth);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_EmissionMap", f.lights[lightVariant]);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", night ? Color.white * 2.2f : Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            // Tüm haritalar _BaseMap'in tiling/offset değerini kullanır.
            mat.SetTextureScale("_BaseMap", new Vector2(1f / ModuleWidth, 1f / ModuleHeight));
            mat.SetTextureOffset("_BaseMap", offset);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material SkyboxMat(bool night)
        {
            string hdr = $"{PolyHaven}/HDRI/{(night ? "modern_buildings_night" : "canary_wharf")}_2k.hdr";
            string path = $"{GeneratedDir}/Gokyuzu_{(night ? "Gece" : "Gunduz")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", LoadTexture(hdr, TexKind.Hdri));
            mat.SetFloat("_Mapping", 1f);
            mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Exposure", night ? 0.8f : 1f);
            mat.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        enum TexKind { Color, Normal, Linear, Hdri }

        /// <summary>Doku içe aktarma ayarlarını düzeltir (normal haritası, sRGB, tekrar) ve dokuyu yükler.</summary>
        static Texture2D LoadTexture(string path, TexKind kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null)
            {
                Debug.LogError($"CityRise: Doku bulunamadı: {path}");
                return null;
            }

            bool changed = false;
            var type = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            bool srgb = kind == TexKind.Color;
            if (kind != TexKind.Normal && kind != TexKind.Hdri && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
            if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
            if (kind == TexKind.Hdri)
            {
                if (importer.textureShape != TextureImporterShape.Texture2D) { importer.textureShape = TextureImporterShape.Texture2D; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp)
                {
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                    changed = true;
                }
            }
            else
            {
                if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (importer.anisoLevel != 4) { importer.anisoLevel = 4; changed = true; }
                var alpha = kind == TexKind.Linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                if (importer.alphaSource != alpha) { importer.alphaSource = alpha; changed = true; }
            }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// URP Lit pürüzlülük haritası almaz; metal/pürüzsüzlük haritası ister (alfa = 1 - pürüzlülük).
        /// </summary>
        static Texture2D MetalSmoothFromRough(Surface s)
        {
            string path = $"{GeneratedDir}/{s.name}_MetalSmooth_{GenVersion}.png";
            if (!File.Exists(FullPath(path)))
            {
                var rough = Downsample(ReadImage(s.Rough), 2, out int size);
                var pixels = new Color[rough.Length];
                for (int i = 0; i < rough.Length; i++) pixels[i] = new Color(0, 0, 0, 1f - rough[i].r);
                WritePng(path, pixels, size, size);
            }
            return LoadTexture(path, TexKind.Linear);
        }

        /// <summary>
        /// Duvar dokusunun üstüne pencereler çizilmiş cephe modülü üretir:
        /// renk, normal, metal/pürüzsüzlük ve birkaç farklı "ışığı yanan pencere" deseni.
        /// </summary>
        static FacadeTextures Facade(string style, Surface wall)
        {
            string prefix = $"{GeneratedDir}/Cephe_{style}_{GenVersion}";
            string albedoPath = prefix + "_Renk.png";
            string normalPath = prefix + "_Normal.png";
            string msPath = prefix + "_MetalSmooth.png";
            var lightPaths = new string[WindowLightVariants];
            for (int i = 0; i < WindowLightVariants; i++) lightPaths[i] = $"{prefix}_Isik{i}.png";

            if (!File.Exists(FullPath(albedoPath)) || !File.Exists(FullPath(lightPaths[WindowLightVariants - 1])))
                GenerateFacade(wall, albedoPath, normalPath, msPath, lightPaths);

            var f = new FacadeTextures
            {
                albedo = LoadTexture(albedoPath, TexKind.Color),
                normal = LoadTexture(normalPath, TexKind.Normal),
                metalSmooth = LoadTexture(msPath, TexKind.Linear),
                lights = new Texture2D[WindowLightVariants],
            };
            for (int i = 0; i < WindowLightVariants; i++) f.lights[i] = LoadTexture(lightPaths[i], TexKind.Color);
            return f;
        }

        static void GenerateFacade(Surface wall, string albedoPath, string normalPath, string msPath, string[] lightPaths)
        {
            float pxPerMeter = ModulePxW / ModuleWidth;
            int block = Mathf.Max(1, Mathf.FloorToInt(2048f / (wall.meters * pxPerMeter)));
            var wallDiff = Downsample(ReadImage(wall.Diff), block, out int tile);
            var wallNor = Downsample(ReadImage(wall.Normal), block, out _);
            var wallRough = Downsample(ReadImage(wall.Rough), block, out _);

            // Bir kat hücresi içindeki pencere ölçüleri (metre).
            const float winX0 = 0.7f, winX1 = 2.5f, winY0 = 0.9f, winY1 = 2.6f;
            const float reveal = 0.10f, frame = 0.07f, mullion = 0.035f;
            const float sillDepth = 0.12f, sillOverhang = 0.08f;
            float midX = (winX0 + winX1) * 0.5f;

            var glass = new Color(0.05f, 0.07f, 0.09f);
            var frameColor = new Color(0.86f, 0.86f, 0.84f);
            var sillColor = new Color(0.72f, 0.71f, 0.68f);
            var flat = new Color(0.5f, 0.5f, 1f);

            int w = ModulePxW, h = ModulePxH;
            var albedo = new Color[w * h];
            var normal = new Color[w * h];
            var ms = new Color[w * h];
            var glassMask = new float[w * h];
            var cellOf = new int[w * h];

            for (int y = 0; y < h; y++)
            {
                float my = (y + 0.5f) / pxPerMeter;
                int cellY = Mathf.Min(1, (int)(my / FloorHeight));
                float ly = my - cellY * FloorHeight;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float mx = (x + 0.5f) / pxPerMeter;
                    int cellX = Mathf.Min(3, (int)(mx / FloorHeight));
                    float lx = mx - cellX * FloorHeight;
                    cellOf[i] = cellY * 4 + cellX;

                    int tx = (int)(mx / wall.meters * tile) % tile;
                    int ty = (int)(my / wall.meters * tile) % tile;
                    int ti = ty * tile + tx;

                    bool inWindow = lx > winX0 && lx < winX1 && ly > winY0 && ly < winY1;
                    bool inSill = ly > winY0 - sillDepth && ly <= winY0 && lx > winX0 - sillOverhang && lx < winX1 + sillOverhang;

                    if (inWindow)
                    {
                        float dl = lx - winX0, dr = winX1 - lx, db = ly - winY0, dt = winY1 - ly;
                        float d = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                        if (d < reveal)
                        {
                            // Pencere boşluğunun iç yüzü: gölgede kalır ve içe doğru eğimlidir.
                            Vector3 n;
                            if (d == dl) n = new Vector3(0.7f, 0, 0.7f);
                            else if (d == dr) n = new Vector3(-0.7f, 0, 0.7f);
                            else if (d == db) n = new Vector3(0, 0.7f, 0.7f);
                            else n = new Vector3(0, -0.7f, 0.7f);
                            albedo[i] = wallDiff[ti] * 0.55f;
                            normal[i] = EncodeNormal(n);
                            ms[i] = new Color(0, 0, 0, 0.2f);
                        }
                        else if (d < reveal + frame || Mathf.Abs(lx - midX) < mullion)
                        {
                            albedo[i] = frameColor;
                            normal[i] = flat;
                            ms[i] = new Color(0, 0, 0, 0.55f);
                        }
                        else
                        {
                            albedo[i] = glass;
                            normal[i] = flat;
                            ms[i] = new Color(0, 0, 0, 0.95f);
                            glassMask[i] = 1f - 0.3f * Mathf.InverseLerp(winY0, winY1, ly);
                        }
                    }
                    else if (inSill)
                    {
                        albedo[i] = sillColor;
                        normal[i] = ly > winY0 - 0.025f ? EncodeNormal(new Vector3(0, 0.6f, 0.8f)) : flat;
                        ms[i] = new Color(0, 0, 0, 0.35f);
                    }
                    else
                    {
                        albedo[i] = wallDiff[ti];
                        normal[i] = wallNor[ti];
                        ms[i] = new Color(0, 0, 0, 1f - wallRough[ti].r);
                    }
                    albedo[i].a = 1f;
                }
            }

            WritePng(albedoPath, albedo, w, h);
            WritePng(normalPath, normal, w, h);
            WritePng(msPath, ms, w, h);

            // Her desende pencerelerin bir kısmı yanar; sıcak ve soğuk ışık karışık.
            var warm = new Color(1f, 0.76f, 0.48f);
            var cool = new Color(0.82f, 0.88f, 1f);
            for (int v = 0; v < lightPaths.Length; v++)
            {
                var rng = new System.Random(100 + v);
                var windowLight = new Color[8];
                for (int c = 0; c < 8; c++)
                {
                    bool lit = rng.NextDouble() < 0.45;
                    var tone = rng.NextDouble() < 0.75 ? warm : cool;
                    windowLight[c] = lit ? tone * (0.6f + 0.4f * (float)rng.NextDouble()) : Color.black;
                }
                var light = new Color[w * h];
                for (int i = 0; i < light.Length; i++)
                {
                    var c = windowLight[cellOf[i]] * glassMask[i];
                    c.a = 1f;
                    light[i] = c;
                }
                WritePng(lightPaths[v], light, w, h);
            }
        }

        static Color EncodeNormal(Vector3 n)
        {
            n.Normalize();
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        static Texture2D ReadImage(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(FullPath(assetPath)));
            return tex;
        }

        /// <summary>Kare dokuyu blok ortalamasıyla küçültür (titreşimsiz).</summary>
        static Color[] Downsample(Texture2D src, int block, out int size)
        {
            var p = src.GetPixels32();
            int w = src.width;
            size = w / block;
            var o = new Color[size * size];
            float inv = 1f / (block * block * 255f);
            for (int ty = 0; ty < size; ty++)
            for (int tx = 0; tx < size; tx++)
            {
                int r = 0, g = 0, b = 0;
                for (int by = 0; by < block; by++)
                {
                    int row = (ty * block + by) * w + tx * block;
                    for (int bx = 0; bx < block; bx++)
                    {
                        var c = p[row + bx];
                        r += c.r; g += c.g; b += c.b;
                    }
                }
                o[ty * size + tx] = new Color(r * inv, g * inv, b * inv, 1f);
            }
            Object.DestroyImmediate(src);
            return o;
        }

        static void WritePng(string assetPath, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(FullPath(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static string FullPath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
