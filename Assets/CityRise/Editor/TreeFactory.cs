using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Mobil için hafif ama gerçek yaprak fotoğraflarıyla ağaç, çalı ve çim öbeği üretir.
    /// Poly Haven'ın (CC0) yaprak ve kabuk dokularından yaprak kümesi dokuları birleştirilir;
    /// gövde/dal tüpleri ve yaprak kartlarından oluşan ağaçlar LOD0, LOD1 ve uzak mesafe için
    /// çapraz billboard (LOD2) ile prefab olarak kaydedilir. Sonuçlar Generated/Agaclar altındadır.
    /// </summary>
    public static class TreeFactory
    {
        const string FoliageDir = "Assets/CityRise/ThirdParty/PolyHaven/Foliage";
        const string OutDir = "Assets/CityRise/Generated/Agaclar";
        /// <summary>Üretim değişirse artır; eski prefablar yerine yenileri üretilir.</summary>
        const string Version = "v2";

        public const int BroadleafCount = 4;
        public const int PineCount = 2;
        public const int BushCount = 3;
        public const int GrassCount = 2;

        public class Set
        {
            public List<GameObject> broadleaf = new List<GameObject>();
            public List<GameObject> pine = new List<GameObject>();
            public List<GameObject> bush = new List<GameObject>();
            public List<GameObject> grass = new List<GameObject>();
            public readonly Dictionary<string, int> lod0Triangles = new Dictionary<string, int>();
            public readonly Dictionary<string, int> lod1Triangles = new Dictionary<string, int>();
        }

        [MenuItem("CityRise/Ağaçları Yeniden Üret", priority = 30)]
        public static void Regenerate()
        {
            AssetDatabase.DeleteAsset(OutDir);
            Ensure();
            Debug.Log("CityRise: Ağaçlar yeniden üretildi.");
        }

        static string PrefabPath(string kind, int i) => $"{OutDir}/{kind}_{i}_{Version}.prefab";

        /// <summary>Prefablar yoksa üretir ve hepsini döndürür. Açık sahnede geçici nesne oluşturur ve siler.</summary>
        public static Set Ensure()
        {
            EnsureFolder(OutDir);
            var set = new Set();
            bool missing = !File.Exists(FullPath(PrefabPath("Agac", BroadleafCount - 1))) || !File.Exists(FullPath(PrefabPath("Cam", PineCount - 1)))
                           || !File.Exists(FullPath(PrefabPath("Cali", BushCount - 1))) || !File.Exists(FullPath(PrefabPath("Cim", GrassCount - 1)));
            if (missing) Generate();

            for (int i = 0; i < BroadleafCount; i++) set.broadleaf.Add(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Agac", i)));
            for (int i = 0; i < PineCount; i++) set.pine.Add(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Cam", i)));
            for (int i = 0; i < BushCount; i++) set.bush.Add(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Cali", i)));
            for (int i = 0; i < GrassCount; i++) set.grass.Add(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Cim", i)));
            foreach (var p in set.broadleaf) CountTriangles(set, p);
            foreach (var p in set.pine) CountTriangles(set, p);
            foreach (var p in set.bush) CountTriangles(set, p);
            foreach (var p in set.grass) CountTriangles(set, p);
            return set;
        }

        static void CountTriangles(Set set, GameObject prefab)
        {
            if (prefab == null) return;
            var group = prefab.GetComponent<LODGroup>();
            if (group == null) return;
            var lods = group.GetLODs();
            int Tris(int l) => l < lods.Length && lods[l].renderers.Length > 0
                ? (lods[l].renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3) : 0;
            set.lod0Triangles[prefab.name] = Tris(0);
            set.lod1Triangles[prefab.name] = Tris(1);
        }

        // ------------------------------------------------------------------ Üretim

        static void Generate()
        {
            var leafDiff = Read($"{FoliageDir}/island_tree_02_leaves_diff_1k.jpg");
            var leafAlpha = Read($"{FoliageDir}/island_tree_02_leaves_alpha_1k.jpg");
            var twigDiff = Read($"{FoliageDir}/fir_tree_01_twig_diff_1k.jpg");
            var twigAlpha = Read($"{FoliageDir}/fir_tree_01_twig_alpha_1k.jpg");
            var leaves = ExtractSprites(leafDiff, leafAlpha, 1500);
            var sprigs = ExtractSprites(twigDiff, twigAlpha, 1500);
            Object.DestroyImmediate(leafDiff); Object.DestroyImmediate(leafAlpha);
            Object.DestroyImmediate(twigDiff); Object.DestroyImmediate(twigAlpha);
            if (leaves.Count == 0 || sprigs.Count == 0)
                throw new Exception("CityRise: Yaprak dokularından parça çıkarılamadı.");

            // Yaprak kümesi dokuları: yaprakların sapı merkeze bakar, uçları dışarı.
            var leafTex = SaveTexture($"{OutDir}/YaprakKumesi.png",
                ComposeCluster(leaves, 512, 120, new Random(11), 0.11f, 0.17f, radial: true, new Color(0.95f, 1.02f, 0.9f)), alphaTest: true);
            var needleTex = SaveTexture($"{OutDir}/IgneKumesi.png",
                ComposeCluster(sprigs, 512, 26, new Random(12), 0.42f, 0.6f, radial: false, new Color(0.95f, 1f, 0.95f)), alphaTest: true);
            var grassTex = SaveTexture($"{OutDir}/CimYapraklari.png", GrassBlades(256, new Random(13)), alphaTest: true);

            var barkTex = LoadTex($"{FoliageDir}/bark_brown_02_diff_1k.jpg", false);
            var barkNor = LoadTex($"{FoliageDir}/bark_brown_02_nor_gl_1k.jpg", true);
            var pineBarkTex = LoadTex($"{FoliageDir}/pine_bark_diff_1k.jpg", false);
            var pineBarkNor = LoadTex($"{FoliageDir}/pine_bark_nor_gl_1k.jpg", true);

            var bark = Mat("Kabuk", m => { m.SetTexture("_BaseMap", barkTex); Normal(m, barkNor); m.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.75f)); m.SetFloat("_Smoothness", 0.1f); });
            var pineBark = Mat("CamKabuk", m => { m.SetTexture("_BaseMap", pineBarkTex); Normal(m, pineBarkNor); m.SetColor("_BaseColor", new Color(0.8f, 0.72f, 0.66f)); m.SetFloat("_Smoothness", 0.1f); });
            var leaf = Mat("Yaprak", m => Foliage(m, leafTex, new Color(0.92f, 1f, 0.82f)));
            var leafShade = Mat("YaprakGolge", m => Foliage(m, leafTex, new Color(0.62f, 0.72f, 0.55f)));
            var leafAutumn = Mat("YaprakSicak", m => Foliage(m, leafTex, new Color(1.05f, 0.95f, 0.62f)));
            var needle = Mat("Igne", m => Foliage(m, needleTex, new Color(0.85f, 0.95f, 0.85f)));
            var needleShade = Mat("IgneGolge", m => Foliage(m, needleTex, new Color(0.55f, 0.66f, 0.55f)));
            var grassMat = Mat("CimObegi", m => Foliage(m, grassTex, Color.white));

            for (int i = 0; i < BroadleafCount; i++)
            {
                var rng = new Random(100 + i);
                var front = i == BroadleafCount - 1 ? leafAutumn : leaf;
                var lod0 = Broadleaf(new Random(100 + i), detailed: true);
                var lod1 = Broadleaf(new Random(100 + i), detailed: false);
                SavePrefab("Agac", i, lod0, lod1, new[] { bark, front, leafShade }, 0.22f, 0.07f, 0.008f);
            }
            for (int i = 0; i < PineCount; i++)
            {
                var lod0 = Pine(new Random(200 + i), detailed: true);
                var lod1 = Pine(new Random(200 + i), detailed: false);
                SavePrefab("Cam", i, lod0, lod1, new[] { pineBark, needle, needleShade }, 0.22f, 0.07f, 0.008f);
            }
            for (int i = 0; i < BushCount; i++)
            {
                var lod0 = Bush(new Random(300 + i), detailed: true);
                var lod1 = Bush(new Random(300 + i), detailed: false);
                SavePrefab("Cali", i, lod0, lod1, new[] { bark, leaf, leafShade }, 0.08f, 0.025f, 0f);
            }
            for (int i = 0; i < GrassCount; i++)
            {
                var lod0 = GrassClump(new Random(400 + i));
                SavePrefab("Cim", i, lod0, null, new[] { grassMat, grassMat, grassMat }, 0.02f, 0f, 0f);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Yaprak dokuları

        class Sprite
        {
            public Color[] px;
            public int w, h;
        }

        static Texture2D Read(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(FullPath(assetPath)));
            return tex;
        }

        /// <summary>Alfa haritasındaki her bağımsız parçayı (tek yaprak / tek dal ucu) ayrı bir resim olarak keser.</summary>
        static List<Sprite> ExtractSprites(Texture2D diff, Texture2D alpha, int minArea)
        {
            int w = alpha.width, h = alpha.height;
            var a = alpha.GetPixels32();
            var d = diff.GetPixels32();
            var seen = new bool[w * h];
            var result = new List<Sprite>();
            var queue = new Queue<int>();
            for (int start = 0; start < a.Length; start++)
            {
                if (seen[start] || a[start].r < 128) continue;
                int minX = w, minY = h, maxX = 0, maxY = 0, area = 0;
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = i % w, y = i / w;
                    area++;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    void Visit(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                        int n = ny * w + nx;
                        if (seen[n] || a[n].r < 128) return;
                        seen[n] = true;
                        queue.Enqueue(n);
                    }
                    Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
                }
                int bw = maxX - minX + 1, bh = maxY - minY + 1;
                bool touchesEdge = minX <= 1 || minY <= 1 || maxX >= w - 2 || maxY >= h - 2;
                float aspect = bw / (float)bh;
                if (area < minArea || touchesEdge || aspect > 3f || aspect < 0.15f) continue;
                if (area > bw * bh * 0.85f) continue; // dikdörtgen bloklar (kabuk şeridi vb.) yaprak değildir

                var s = new Sprite { w = bw + 4, h = bh + 4 };
                s.px = new Color[s.w * s.h];
                for (int y = 0; y < s.h; y++)
                for (int x = 0; x < s.w; x++)
                {
                    int sx = minX - 2 + x, sy = minY - 2 + y;
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                    var c = d[sy * w + sx];
                    s.px[y * s.w + x] = new Color(c.r / 255f, c.g / 255f, c.b / 255f, a[sy * w + sx].r / 255f);
                }
                result.Add(s);
            }
            return result;
        }

        /// <summary>
        /// Birçok yaprağı (ya da dal ucunu) döndürüp boyutlandırarak tek bir yaprak kümesi dokusu oluşturur.
        /// Önce çizilenler koyulaştırılır; böylece kümede derinlik hissi oluşur.
        /// </summary>
        static Color[] ComposeCluster(List<Sprite> sprites, int size, int count, Random rng, float minScale, float maxScale, bool radial, Color tint)
        {
            var canvas = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            for (int n = 0; n < count; n++)
            {
                var s = sprites[rng.Next(sprites.Count)];
                float t = n / (float)count;
                float targetH = size * Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                float scale = targetH / s.h;
                Vector2 pos;
                float angle;
                if (radial)
                {
                    float r = Mathf.Sqrt((float)rng.NextDouble()) * size * 0.33f;
                    float dirA = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(dirA), Mathf.Sin(dirA));
                    pos = center + dir * r;
                    angle = Mathf.Atan2(dir.y, dir.x) - Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                }
                else
                {
                    // Dal uçları: hepsi yukarı doğru, yelpaze gibi.
                    pos = new Vector2(size * (0.3f + 0.4f * (float)rng.NextDouble()), size * (0.2f + 0.45f * (float)rng.NextDouble()));
                    angle = ((float)rng.NextDouble() - 0.5f) * 1.3f;
                }
                float shade = Mathf.Lerp(0.62f, 1.08f, t) * Mathf.Lerp(0.88f, 1.08f, (float)rng.NextDouble());
                var hue = new Color(Mathf.Lerp(0.92f, 1.06f, (float)rng.NextDouble()), 1f, Mathf.Lerp(0.88f, 1.04f, (float)rng.NextDouble()));
                DrawSprite(canvas, size, s, pos, angle, scale, new Color(tint.r * hue.r * shade, tint.g * shade, tint.b * hue.b * shade));
            }
            return canvas;
        }

        static void DrawSprite(Color[] canvas, int size, Sprite s, Vector2 pos, float angle, float scale, Color mul)
        {
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float hw = s.w * 0.5f * scale, hh = s.h * 0.5f * scale;
            float ext = Mathf.Sqrt(hw * hw + hh * hh);
            // Sprite'ın tabanı (sap) pos noktasında olsun diye merkez yukarı kaydırılır.
            var up = new Vector2(-sin, cos);
            var c = pos + up * hh;
            int x0 = Mathf.Max(0, (int)(c.x - ext)), x1 = Mathf.Min(size - 1, (int)(c.x + ext));
            int y0 = Mathf.Max(0, (int)(c.y - ext)), y1 = Mathf.Min(size - 1, (int)(c.y + ext));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                float lx = (dx * cos + dy * sin) / scale + s.w * 0.5f;
                float ly = (-dx * sin + dy * cos) / scale + s.h * 0.5f;
                if (lx < 0 || ly < 0 || lx >= s.w - 1 || ly >= s.h - 1) continue;
                var src = Bilinear(s, lx, ly);
                if (src.a <= 0.01f) continue;
                int i = y * size + x;
                var dst = canvas[i];
                float a = src.a;
                var col = new Color(src.r * mul.r, src.g * mul.g, src.b * mul.b);
                canvas[i] = new Color(
                    col.r * a + dst.r * (1 - a),
                    col.g * a + dst.g * (1 - a),
                    col.b * a + dst.b * (1 - a),
                    a + dst.a * (1 - a));
            }
        }

        static Color Bilinear(Sprite s, float x, float y)
        {
            int ix = (int)x, iy = (int)y;
            float fx = x - ix, fy = y - iy;
            var c00 = s.px[iy * s.w + ix];
            var c10 = s.px[iy * s.w + ix + 1];
            var c01 = s.px[(iy + 1) * s.w + ix];
            var c11 = s.px[(iy + 1) * s.w + ix + 1];
            return Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
        }

        /// <summary>Basit çim yaprakları: dipte koyu, uçta sarımsı yeşil.</summary>
        static Color[] GrassBlades(int size, Random rng)
        {
            var canvas = new Color[size * size];
            for (int b = 0; b < 70; b++)
            {
                float baseX = size * (0.08f + 0.84f * (float)rng.NextDouble());
                float height = size * (0.45f + 0.5f * (float)rng.NextDouble());
                float lean = ((float)rng.NextDouble() - 0.5f) * size * 0.35f;
                float width = 3f + 4f * (float)rng.NextDouble();
                var root = new Color(0.16f, 0.30f, 0.08f);
                var tip = Color.Lerp(new Color(0.55f, 0.72f, 0.25f), new Color(0.72f, 0.75f, 0.35f), (float)rng.NextDouble());
                for (int y = 0; y < (int)height; y++)
                {
                    float t = y / height;
                    float cx = baseX + lean * t * t;
                    float half = width * (1f - t) * 0.5f + 0.3f;
                    var col = Color.Lerp(root, tip, Mathf.Pow(t, 0.7f));
                    for (int x = (int)(cx - half - 1); x <= (int)(cx + half + 1); x++)
                    {
                        if (x < 0 || x >= size) continue;
                        float cover = Mathf.Clamp01(half + 0.5f - Mathf.Abs(x + 0.5f - cx));
                        if (cover <= 0) continue;
                        int i = y * size + x;
                        var dst = canvas[i];
                        canvas[i] = new Color(col.r * cover + dst.r * (1 - cover), col.g * cover + dst.g * (1 - cover), col.b * cover + dst.b * (1 - cover), cover + dst.a * (1 - cover));
                    }
                }
            }
            return canvas;
        }

        static Texture2D SaveTexture(string path, Color[] pixels, bool alphaTest)
        {
            int size = (int)Mathf.Sqrt(pixels.Length);
            // Saydam piksellerin rengini yaprak ortalamasıyla doldur (mipmap'lerde koyu kenar olmasın).
            Color avg = Color.black;
            float n = 0;
            foreach (var p in pixels) if (p.a > 0.5f) { avg += p; n++; }
            if (n > 0) avg /= n;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a < 0.02f) pixels[i] = new Color(avg.r, avg.g, avg.b, 0f);
                else if (pixels[i].a < 1f) pixels[i] = new Color(pixels[i].r / Mathf.Max(pixels[i].a, 0.2f), pixels[i].g / Mathf.Max(pixels[i].a, 0.2f), pixels[i].b / Mathf.Max(pixels[i].a, 0.2f), pixels[i].a);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = alphaTest;
            imp.alphaTestReferenceValue = 0.45f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D LoadTex(string path, bool normal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != type || imp.maxTextureSize != 1024)
                {
                    imp.textureType = type;
                    imp.maxTextureSize = 1024;
                    imp.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Malzemeler

        /// <summary>Malzemeyi önce kaydeder, sonra ayarlar ve hemen yazar (URP anahtar kelimeleri kaybolmasın).</summary>
        static Material Mat(string name, Action<Material> configure)
        {
            string path = $"{OutDir}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            mat.enableInstancing = true;
            mat.SetFloat("_Metallic", 0f);
            configure(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Normal(Material m, Texture2D tex)
        {
            if (tex == null) return;
            m.SetTexture("_BumpMap", tex);
            m.EnableKeyword("_NORMALMAP");
        }

        /// <summary>Yaprak kartı: alfa kesme, iki yüzlü.</summary>
        static void Foliage(Material m, Texture2D tex, Color tint)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.45f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }

        // ------------------------------------------------------------------ Ağaç geometrisi

        class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] tris = { new List<int>(), new List<int>(), new List<int>() };

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.subMeshCount = 3;
                for (int i = 0; i < 3; i++) m.SetTriangles(tris[i], i);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        static float R(Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Vector3 RandomUnit(Random rng)
        {
            float z = R(rng, -1f, 1f), a = R(rng, 0f, Mathf.PI * 2f), r = Mathf.Sqrt(1 - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        /// <summary>Yol boyunca incelen tüp (gövde ya da dal). Kabuk dokusu ~1.2 m'de bir tekrar eder.</summary>
        static void Tube(MeshData md, IList<Vector3> path, IList<float> radii, int sides)
        {
            int start = md.v.Count;
            float len = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) len += Vector3.Distance(path[i], path[i - 1]);
                var dir = i < path.Count - 1 ? (path[i + 1] - path[i]).normalized : (path[i] - path[i - 1]).normalized;
                var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var side2 = Vector3.Cross(dir, side);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    var nrm = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                    md.v.Add(path[i] + nrm * radii[i]);
                    md.n.Add(nrm);
                    md.uv.Add(new Vector2(s / (float)sides * Mathf.Max(1, Mathf.Round(radii[0] * 6f)), len / 1.2f));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = start + i * (sides + 1) + s, b = a + sides + 1;
                md.tris[0].AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }

        /// <summary>Yaprak kartı. Işıklandırma normali taç merkezinden dışarı bakar; kart yumuşak, hacimli görünür.</summary>
        static void Card(MeshData md, int sub, Vector3 center, Vector3 right, Vector3 up, float w, float h, Vector3 lightNormal, bool baseAtCenter = false)
        {
            int i = md.v.Count;
            var r = right.normalized * w * 0.5f;
            var u = up.normalized * h * (baseAtCenter ? 1f : 0.5f);
            var b = baseAtCenter ? Vector3.zero : -u;
            md.v.Add(center - r + b); md.uv.Add(new Vector2(0, 0));
            md.v.Add(center + r + b); md.uv.Add(new Vector2(1, 0));
            md.v.Add(center + r + u); md.uv.Add(new Vector2(1, 1));
            md.v.Add(center - r + u); md.uv.Add(new Vector2(0, 1));
            for (int k = 0; k < 4; k++) md.n.Add(lightNormal);
            md.tris[sub].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        static void Canopy(MeshData md, Random rng, Vector3 c, Vector3 radii, int cards, float cardMin, float cardMax)
        {
            for (int k = 0; k < cards; k++)
            {
                var dir = RandomUnit(rng);
                dir.y = dir.y * 0.8f + 0.15f;
                dir.Normalize();
                float t = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt((float)rng.NextDouble()));
                var p = c + Vector3.Scale(dir, radii) * t;
                var facing = (dir + RandomUnit(rng) * 0.6f).normalized;
                var right = Vector3.Cross(facing, RandomUnit(rng)).normalized;
                var up = Vector3.Cross(right, facing);
                float size = R(rng, cardMin, cardMax);
                var light = (dir * 0.75f + Vector3.up * 0.4f).normalized;
                bool shaded = dir.y < -0.15f || t < 0.62f;
                Card(md, shaded ? 2 : 1, p, right, up, size, size, light);
            }
        }

        static Mesh Broadleaf(Random rng, bool detailed)
        {
            var md = new MeshData();
            float trunkH = R(rng, 2.4f, 3.4f);
            float baseR = R(rng, 0.22f, 0.3f);
            var trunk = new List<Vector3>();
            var radii = new List<float>();
            int segs = detailed ? 5 : 3;
            var lean = new Vector3(R(rng, -0.25f, 0.25f), 0, R(rng, -0.25f, 0.25f));
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                trunk.Add(new Vector3(0, trunkH * t, 0) + lean * t * t);
                radii.Add(Mathf.Lerp(baseR, baseR * 0.55f, t) + (i == 0 ? 0.08f : 0f));
            }
            Tube(md, trunk, radii, detailed ? 8 : 5);

            var top = trunk[trunk.Count - 1];
            var crown = new Vector3(R(rng, 2.6f, 3.4f), R(rng, 2.3f, 2.9f), R(rng, 2.6f, 3.4f));
            var c = top + Vector3.up * crown.y * 0.75f;
            int branches = detailed ? rng.Next(4, 7) : 3;
            for (int b = 0; b < branches; b++)
            {
                float a = b / (float)branches * Mathf.PI * 2f + R(rng, -0.3f, 0.3f);
                var start = trunk[segs - 1] + (top - trunk[segs - 1]) * R(rng, 0.2f, 1f);
                var dir = new Vector3(Mathf.Cos(a) * 0.75f, R(rng, 0.55f, 0.9f), Mathf.Sin(a) * 0.75f).normalized;
                float len = crown.x * R(rng, 0.65f, 0.95f);
                var path = new List<Vector3> { start, start + dir * len * 0.5f + Vector3.up * 0.2f, start + dir * len + Vector3.up * 0.35f };
                Tube(md, path, new List<float> { baseR * 0.45f, baseR * 0.28f, 0.03f }, detailed ? 5 : 4);
            }
            Canopy(md, rng, c, crown, detailed ? 120 : 34, detailed ? 1.7f : 2.6f, detailed ? 2.5f : 3.4f);
            return md.ToMesh(detailed ? "LOD0" : "LOD1");
        }

        static Mesh Pine(Random rng, bool detailed)
        {
            var md = new MeshData();
            float h = R(rng, 8f, 10.5f);
            Tube(md, new List<Vector3> { Vector3.zero, Vector3.up * h * 0.5f, Vector3.up * h }, new List<float> { 0.3f, 0.2f, 0.04f }, detailed ? 7 : 5);
            float maxR = R(rng, 2.4f, 3f);
            float step = detailed ? 0.5f : 1.1f;
            int perLevel = detailed ? 8 : 5;
            float offset = 0;
            for (float y = 1.4f; y < h - 0.4f; y += step)
            {
                float f = (y - 1.4f) / (h - 1.8f);
                float rr = maxR * Mathf.Pow(1f - f, 0.95f) + 0.35f;
                offset += 0.7f;
                for (int k = 0; k < perLevel; k++)
                {
                    float a = offset + k / (float)perLevel * Mathf.PI * 2f + R(rng, -0.2f, 0.2f);
                    var outDir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    // Dal kartı gövdeden dışarı uzanır ve hafif sarkar.
                    var along = (outDir + Vector3.down * R(rng, 0.25f, 0.45f)).normalized;
                    var right = Vector3.Cross(Vector3.up, outDir).normalized;
                    var light = (outDir * 0.55f + Vector3.up * 0.8f).normalized;
                    bool shaded = f < 0.25f && k % 2 == 0;
                    Card(md, shaded ? 2 : 1, new Vector3(0, y + 0.15f, 0), right, along, rr * 1.15f + 0.4f, rr + 0.3f, light, baseAtCenter: true);
                }
            }
            // Tepe.
            for (int k = 0; k < 2; k++)
            {
                var right = k == 0 ? Vector3.right : Vector3.forward;
                Card(md, 1, new Vector3(0, h - 1.4f, 0), right, Vector3.up, 1.3f, 1.8f, Vector3.up, baseAtCenter: true);
            }
            return md.ToMesh(detailed ? "LOD0" : "LOD1");
        }

        static Mesh Bush(Random rng, bool detailed)
        {
            var md = new MeshData();
            var radii = new Vector3(R(rng, 0.9f, 1.4f), R(rng, 0.7f, 1f), R(rng, 0.9f, 1.4f));
            Canopy(md, rng, Vector3.up * radii.y * 0.8f, radii, detailed ? 30 : 12, detailed ? 0.9f : 1.3f, detailed ? 1.3f : 1.8f);
            return md.ToMesh(detailed ? "LOD0" : "LOD1");
        }

        static Mesh GrassClump(Random rng)
        {
            var md = new MeshData();
            for (int k = 0; k < 3; k++)
            {
                float a = k / 3f * Mathf.PI + R(rng, -0.2f, 0.2f);
                var right = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Card(md, 1, Vector3.zero, right, Vector3.up, R(rng, 0.9f, 1.3f), R(rng, 0.5f, 0.75f), Vector3.up, baseAtCenter: true);
            }
            return md.ToMesh("LOD0");
        }

        // ------------------------------------------------------------------ Prefab, LOD ve billboard

        static void SavePrefab(string kind, int index, Mesh lod0, Mesh lod1, Material[] mats, float h0, float h1, float h2)
        {
            string baseName = $"{kind}_{index}_{Version}";
            var root = new GameObject(baseName);
            var lods = new List<LOD>();
            var r0 = MeshChild(root, "LOD0", lod0, mats, $"{OutDir}/{baseName}_LOD0.asset");
            lods.Add(new LOD(h0, new Renderer[] { r0 }));
            if (lod1 != null)
            {
                var r1 = MeshChild(root, "LOD1", lod1, mats, $"{OutDir}/{baseName}_LOD1.asset");
                r1.shadowCastingMode = ShadowCastingMode.On;
                lods.Add(new LOD(h1, new Renderer[] { r1 }));
            }
            if (h2 > 0f)
            {
                var bb = Billboard(root, r0, baseName);
                lods.Add(new LOD(h2, new Renderer[] { bb }));
            }
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
            group.fadeMode = LODFadeMode.None;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(kind, index));
            Object.DestroyImmediate(root);
        }

        static MeshRenderer MeshChild(GameObject root, string name, Mesh mesh, Material[] mats, string meshPath)
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return r;
        }

        /// <summary>
        /// Uzak mesafe için ağacın yandan görünüşünü dokuya çeker ve iki çapraz düzlem yapar.
        /// Saydamlık, siyah ve beyaz arka planla iki kez çekip farkından hesaplanır.
        /// </summary>
        static MeshRenderer Billboard(GameObject root, MeshRenderer source, string baseName)
        {
            var bounds = source.bounds;
            float w = Mathf.Max(bounds.size.x, bounds.size.z), h = bounds.size.y;
            const int texH = 256;
            int texW = Mathf.Clamp(Mathf.RoundToInt(texH * w / h / 4f) * 4, 64, 256);

            var far = new Vector3(5000f, 0f, 5000f);
            var probe = Object.Instantiate(source.gameObject);
            probe.transform.position = far;
            var camGo = new GameObject("BillboardKamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.aspect = w / h;
            cam.transform.position = far + new Vector3(0, bounds.center.y - root.transform.position.y, -50f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.renderShadows = false;

            var oldMode = RenderSettings.ambientMode;
            var oldColor = RenderSettings.ambientLight;
            var oldFog = RenderSettings.fog;
            var oldSky = RenderSettings.skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            var lightGo = new GameObject("BillboardIsik");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.25f;
            light.transform.rotation = Quaternion.Euler(35f, 20f, 0f);

            var onBlack = Capture(cam, texW, texH, Color.black);
            var onWhite = Capture(cam, texW, texH, Color.white);

            RenderSettings.ambientMode = oldMode;
            RenderSettings.ambientLight = oldColor;
            RenderSettings.fog = oldFog;
            RenderSettings.skybox = oldSky;
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(probe);

            var px = new Color[texW * texH];
            for (int i = 0; i < px.Length; i++)
            {
                var b = onBlack[i];
                var wht = onWhite[i];
                float alpha = Mathf.Clamp01(1f - ((wht.r - b.r) + (wht.g - b.g) + (wht.b - b.b)) / 3f);
                // Siyah zemindeki renk alfa ile çarpılmış durumda; yarı saydam kenarlarda bölmek parlatır, sınırla.
                float k = 1f / Mathf.Max(alpha, 0.6f);
                px[i] = alpha > 0.01f ? new Color(b.r * k, b.g * k, b.b * k, alpha) : new Color(0, 0, 0, 0);
            }
            var square = new Color[Math.Max(texW, texH) * Math.Max(texW, texH)];
            int size = Math.Max(texW, texH);
            int ox = (size - texW) / 2;
            for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
                square[y * size + ox + x] = px[y * texW + x];
            var tex = SaveTexture($"{OutDir}/{baseName}_Uzak.png", square, alphaTest: true);
            var mat = Mat(baseName + "_Uzak", m => Foliage(m, tex, new Color(0.85f, 0.85f, 0.85f)));

            // İki çapraz düzlem. Doku kare; genişlik oranı UV ile korunur.
            var md = new MeshData();
            float u0 = ox / (float)size, u1 = (ox + texW) / (float)size;
            for (int k = 0; k < 2; k++)
            {
                var right = k == 0 ? Vector3.right : Vector3.forward;
                int i = md.v.Count;
                var r = right * w * 0.5f;
                var c = new Vector3(bounds.center.x - root.transform.position.x, bounds.min.y - root.transform.position.y, bounds.center.z - root.transform.position.z);
                md.v.Add(c - r); md.uv.Add(new Vector2(u0, 0));
                md.v.Add(c + r); md.uv.Add(new Vector2(u1, 0));
                md.v.Add(c + r + Vector3.up * h); md.uv.Add(new Vector2(u1, 1));
                md.v.Add(c - r + Vector3.up * h); md.uv.Add(new Vector2(u0, 1));
                var bn = (Vector3.up * 0.6f + Vector3.Cross(right, Vector3.up) * 0.8f).normalized;
                for (int n = 0; n < 4; n++) md.n.Add(bn);
                md.tris[0].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            var mesh = md.ToMesh("Uzak");
            var r2 = MeshChild(root, "LOD2", mesh, new[] { mat, mat, mat }, $"{OutDir}/{baseName}_LOD2.asset");
            r2.shadowCastingMode = ShadowCastingMode.Off;
            return r2;
        }

        static Color[] Capture(Camera cam, int w, int h, Color background)
        {
            cam.backgroundColor = background;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            rt.Release();
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
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
