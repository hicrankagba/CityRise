using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Kodla üretilen gerçekçi yiyecekler (Poly Haven ve Kenney'de olmayanlar). Şimdilik simit:
    /// burgulu halka (iki örgü), pekmezli kızarık kabuk dokusu, üstte yüzlerce tek tek susam tanesi.
    /// Ağ, doku ve prefab Generated/Yiyecek altına kaydedilir; kütüphanede "gen/simit" anahtarıyla bulunur.
    /// Ölçek: gerçek boy (13 cm çap).
    /// </summary>
    public static class FoodFactory
    {
        const string OutDir = "Assets/CityRise/Generated/Yiyecek";
        const string Version = "v11";

        public static Dictionary<string, GameObject> Ensure()
        {
            var result = new Dictionary<string, GameObject>();
            var simit = Simit();
            if (simit != null) result["simit"] = simit;
            return result;
        }

        // ------------------------------------------------------------ Simit

        static GameObject Simit()
        {
            string prefabPath = $"{OutDir}/Simit_{Version}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;
            EnsureFolder(OutDir);

            const float R = 0.052f;     // halka merkez yarıçapı
            const float r = 0.017f;     // hamur kalınlığı
            const int twists = 11;      // halka boyunca burgu sayısı
            const int nu = 160, nv = 28;

            // Burgulu kesit: iki örgünün izi, kesit yarıçapını burgu boyunca dalgalandırır.
            float Radius(float u, float v) => r * (1f + 0.17f * Mathf.Cos(2f * v + twists * u)) * (1f - 0.12f * Mathf.Max(0, -Mathf.Sin(v)));
            Vector3 Point(float u, float v)
            {
                float rr = Radius(u, v);
                // Alt yüz hafif basık (tepside pişmiş).
                float y = rr * Mathf.Sin(v);
                if (y < 0) y *= 0.7f;
                float d = R + rr * Mathf.Cos(v);
                return new Vector3(d * Mathf.Cos(u), y + r * 0.7f, d * Mathf.Sin(u));
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nv; j++)
            {
                float u = i / (float)nu * Mathf.PI * 2f, v = j / (float)nv * Mathf.PI * 2f;
                verts.Add(Point(u, v));
                uvs.Add(new Vector2(i / (float)nu, j / (float)nv));
            }
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                int a = i * (nv + 1) + j, b = (i + 1) * (nv + 1) + j;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }

            // Susamlar: üst yüzeyde rastgele, yüzeye yatık küçük elipsoidler (ayrı alt ağ).
            var seedTris = new List<int>();
            var rng = new System.Random(7);
            var seedShape = SeedShape();
            var seedNormals = new List<(int index, Vector3 normal)>();
            for (int k = 0; k < 900; k++)
            {
                float u = (float)rng.NextDouble() * Mathf.PI * 2f;
                // Üst yarı ağırlıklı (v: -0.3..PI+0.3).
                float v = Mathf.Lerp(-0.25f, Mathf.PI + 0.25f, (float)rng.NextDouble());
                var p = Point(u, v);
                var pu = Point(u + 0.01f, v) - p;
                var pv = Point(u, v + 0.01f) - p;
                var n = Vector3.Cross(pv, pu).normalized;
                // Normal dışarı baksın (hamur ekseninden yüzeye doğru).
                var axis = new Vector3(R * Mathf.Cos(u), r * 0.7f, R * Mathf.Sin(u));
                if (Vector3.Dot(n, p - axis) < 0) n = -n;
                if (n.y < -0.2f) continue;
                var tangent = Quaternion.AngleAxis((float)rng.NextDouble() * 360f, n) * pu.normalized;
                var rot = Quaternion.LookRotation(tangent, n);
                // Susam: yüzeye yatık, ortası hafif kabarık elips (merkez + 8 kenar noktası), yelpaze üçgenler.
                int baseIndex = verts.Count;
                const float L = 0.0024f, Wd = 0.0013f, Hd = 0.0007f;
                verts.Add(p + n * (0.0006f + Hd));
                uvs.Add(new Vector2(0.5f, 0.5f));
                seedNormals.Add((baseIndex, n));
                for (int e = 0; e < 8; e++)
                {
                    float a = e / 8f * Mathf.PI * 2f;
                    var edge = new Vector3(Mathf.Cos(a) * Wd, 0, Mathf.Sin(a) * L);
                    seedNormals.Add((verts.Count, (n + rot * edge.normalized * 0.4f).normalized));
                    verts.Add(p + rot * edge + n * 0.0006f);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                }
                for (int e = 0; e < 8; e++)
                {
                    int a = baseIndex + 1 + e, b = baseIndex + 1 + (e + 1) % 8;
                    // Sarım yönü: üçgenin normali yüzey normaliyle aynı yönde olsun.
                    // Her iki yüz (normaller zaten dışarı bakıyor; hangi yüz görünürse doğru aydınlanır).
                    seedTris.Add(baseIndex); seedTris.Add(b); seedTris.Add(a);
                    seedTris.Add(baseIndex); seedTris.Add(a); seedTris.Add(b);
                }
            }

            // Hamur ve susam ayrı ağlar (ayrı nesneler; her biri kendi malzemesiyle).
            int crustCount = (nu + 1) * (nv + 1);
            var mesh = new Mesh { name = "Simit", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts.GetRange(0, crustCount));
            mesh.SetUVs(0, uvs.GetRange(0, crustCount));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, $"{OutDir}/Simit_{Version}.asset");

            var seedMesh = new Mesh { name = "Susamlar", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            seedMesh.SetVertices(verts.GetRange(crustCount, verts.Count - crustCount));
            var local = new List<int>(seedTris.Count);
            foreach (var t in seedTris) local.Add(t - crustCount);
            seedMesh.SetTriangles(local, 0);
            var sn = new Vector3[verts.Count - crustCount];
            foreach (var (index, normal) in seedNormals) sn[index - crustCount] = normal;
            seedMesh.normals = sn;
            seedMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(seedMesh, $"{OutDir}/Susamlar_{Version}.asset");

            var crust = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SimitKabuk" };
            crust.SetTexture("_BaseMap", CrustTexture(twists));
            crust.SetColor("_BaseColor", Color.white);
            crust.SetFloat("_Smoothness", 0.42f);   // pekmez parlaklığı
            AssetDatabase.CreateAsset(crust, $"{OutDir}/SimitKabuk_{Version}.mat");
            var sesame = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Susam" };
            sesame.SetColor("_BaseColor", new Color(1f, 0.97f, 0.86f));
            sesame.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(sesame, $"{OutDir}/Susam_{Version}.mat");

            var go = new GameObject("Simit");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = crust;
            var seeds = new GameObject("Susamlar");
            seeds.transform.SetParent(go.transform, false);
            seeds.AddComponent<MeshFilter>().sharedMesh = seedMesh;
            var sr = seeds.AddComponent<MeshRenderer>();
            sr.sharedMaterial = sesame;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;   // milimetrik susamlarda gölge haritası lekeler bırakır
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Susam tanesi: 3 × 1,6 × 0,8 mm, uçları sivri basık elipsoid (12 köşe).</summary>
        static Vector3[] SeedShape()
        {
            const float L = 0.0019f, W = 0.001f, H = 0.0005f;
            return new[]
            {
                new Vector3(0, 0, L), new Vector3(0, 0, -L),
                new Vector3(W, 0, 0.4f * L), new Vector3(W, 0, -0.4f * L),
                new Vector3(-W, 0, 0.4f * L), new Vector3(-W, 0, -0.4f * L),
                new Vector3(0, H, 0.4f * L), new Vector3(0, H, -0.4f * L),
            };
        }

        static readonly int[] SeedTris =
        {
            0, 2, 6, 0, 6, 4, 6, 2, 3, 6, 3, 7, 4, 6, 7, 4, 7, 5, 7, 3, 1, 5, 7, 1,
            0, 4, 2, 2, 4, 5, 2, 5, 3, 3, 5, 1,
        };

        /// <summary>
        /// Kabuk dokusu: burgu oluklarında koyu, sırtlarda pekmezli altın kahve; alt yüz soluk hamur rengi;
        /// küçük gözenek ve kızarma lekeleri.
        /// </summary>
        static Texture2D CrustTexture(int twists)
        {
            string path = $"{OutDir}/SimitKabuk_{Version}.png";
            const int W = 1024, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
            var px = new Color[W * H];
            var ridge = new Color(0.7f, 0.45f, 0.2f);
            var groove = new Color(0.42f, 0.22f, 0.08f);
            var pale = new Color(0.86f, 0.68f, 0.45f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W * Mathf.PI * 2f, v = y / (float)H * Mathf.PI * 2f;
                float twist = Mathf.Cos(2f * v + twists * u);          // -1 oluk, +1 sırt
                var c = Color.Lerp(groove, ridge, Mathf.SmoothStep(0f, 1f, twist * 0.5f + 0.5f));
                // Alt yüz (sin v < 0) daha soluk.
                float bottom = Mathf.Clamp01(-Mathf.Sin(v) * 1.4f);
                c = Color.Lerp(c, pale, bottom * 0.7f);
                // Kızarma lekeleri ve gözenekler.
                float n1 = Mathf.PerlinNoise(x * 0.02f, y * 0.05f);
                float n2 = Mathf.PerlinNoise(x * 0.25f + 50f, y * 0.25f + 30f);
                c *= 0.85f + 0.3f * n1;
                px[y * W + x] = c;
            }
            // Susam izleri: üst yüzde (v 0..PI) krem renkli küçük elipsler; doku uzaktan da susamlı görünür.
            var rng = new System.Random(11);
            var cream = new Color(0.97f, 0.9f, 0.72f);
            for (int k = 0; k < 2600; k++)
            {
                int cx = rng.Next(W), cy = (int)(H * 0.5f * (float)rng.NextDouble() * 1.05f);
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    // u yönü 4 kat sıkışık (doku 1024×256, halka çevresi kesitin ~3 katı).
                    float lx = (dx * ca + dy * sa) / 3.2f, ly = (-dx * sa + dy * ca) / 1.6f;
                    float d = lx * lx + ly * ly;
                    if (d > 1f) continue;
                    int x = (cx + dx + W) % W, y = Mathf.Clamp(cy + dy, 0, H - 1);
                    px[y * W + x] = Color.Lerp(cream, px[y * W + x], d * d * 0.6f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(FullPath(path), tex.EncodeToJPG(90));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
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
