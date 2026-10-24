using System;
using System.Collections.Generic;
using System.IO;
using CityRise.CameraRig;
using CityRise.City;
using CityRise.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Menü: CityRise > Başlangıç Kasabasını Oluştur.
    /// Şehir düzeni verisinden (CityLayout) sahneyi kurar: yollar, dükkanlar, evler, park, meydan,
    /// kilitli dış halkalar, kırsal çevre, gökyüzü ve ışık.
    /// Modeller Kenney (CC0), gökyüzü ve çim dokusu Poly Haven (CC0); bkz. ThirdParty klasörü.
    /// </summary>
    public static class TownBuilder
    {
        const string Root = "Assets/CityRise";
        const string GeneratedDir = Root + "/Generated/Kasaba";
        const string DataDir = Root + "/Data";
        const string LayoutPath = DataDir + "/BaslangicKasabasi.asset";
        const string ScenePath = Root + "/Scenes/BaslangicKasabasi.unity";
        const string KenneyDir = Root + "/ThirdParty/Kenney";
        const string PolyHavenDir = Root + "/ThirdParty/PolyHaven";

        /// <summary>Kenney şehir kitlerinde 1 birim = bir yol karesi = bir hücre (8 m).</summary>
        const float CityScale = 8f;
        /// <summary>Doğa kiti farklı ölçekte çizilmiş; ağaçlar 6-9 m olsun diye.</summary>
        const float NatureScale = 5f;
        /// <summary>Kenney binalarının ön yüzü (kapı, tente) modelin -Z yönüne bakar.</summary>
        const float ModelFrontYaw = 180f;
        /// <summary>Şehir sınırının dışında çizilecek kırsal alanın yarıçapı (m).</summary>
        const float CountryRadius = 650f;

        [MenuItem("CityRise/Başlangıç Kasabasını Oluştur (Gündüz)", priority = 0)]
        public static void BuildDay() => Build(night: false);

        [MenuItem("CityRise/Başlangıç Kasabasını Oluştur (Gece)", priority = 1)]
        public static void BuildNight() => Build(night: true);

        [MenuItem("CityRise/Kasaba Düzenini Sıfırla", priority = 20)]
        public static void ResetLayout()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Kasaba düzeni",
                    "Arsa düzeni varsayılan başlangıç kasabasına döndürülecek. Devam edilsin mi?", "Sıfırla", "Vazgeç"))
                return;
            var layout = LoadOrCreateLayout();
            StartingTownGenerator.Fill(layout);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        class Ctx
        {
            public CityLayout layout;
            public CityGrid g;
            public bool night;
            public Random rng = new Random(7);
            public Transform root, roads, plots, nature, lights;
            public HashSet<Vector2Int> drawnRoads = new HashSet<Vector2Int>();
            public RectInt openArea;
            public Material grass, meadow, water, stone, dirt, signBoard, signPost, lampGlow;
            public readonly Dictionary<string, Material> kenneyMats = new Dictionary<string, Material>();
            public readonly Dictionary<string, Material> natureMats = new Dictionary<string, Material>();
            public List<Rect> keepClear = new List<Rect>();
            public List<Material> emissive = new List<Material>();
            public int lightCount;
            public TreeFactory.Set trees;
            public int treeCount;
        }

        public static void Build(bool night)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("CityRise", "Hazırlanıyor...", 0.05f);
                EnsureFolder(GeneratedDir);
                EnsureFolder(DataDir);
                EnsureFolder(Root + "/Scenes");

                var c = new Ctx { night = night };
                c.layout = LoadOrCreateLayout();
                c.g = c.layout.grid;
                c.openArea = OpenArea(c.g);
                EnsureBusinessDefinitions();
                SetupRendering();

                // Malzemeler yeni sahneden sonra: sahnede tutulan gece malzemeleri sahne değişince silinir.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorUtility.DisplayProgressBar("CityRise", "Ağaçlar hazırlanıyor (ilk seferde biraz sürer)...", 0.1f);
                c.trees = TreeFactory.Ensure();
                CreateMaterials(c);
                c.root = new GameObject("Sehir").transform;
                var town = c.root.gameObject.AddComponent<CityTown>();
                town.layout = c.layout;
                c.roads = Group(c.root, "Yollar");
                c.plots = Group(c.root, "Arsalar");
                c.nature = Group(c.root, "Doga");
                c.lights = Group(c.root, "Isiklar");

                EditorUtility.DisplayProgressBar("CityRise", "Zemin ve yollar...", 0.2f);
                BuildGround(c);
                BuildRoads(c);
                EditorUtility.DisplayProgressBar("CityRise", "Arsalar ve binalar...", 0.4f);
                foreach (var plot in c.layout.plots) BuildPlot(c, plot);
                BuildBoundaryFence(c);
                BuildStreetLamps(c);
                EditorUtility.DisplayProgressBar("CityRise", "Doğa ve kırsal...", 0.6f);
                BuildCountryside(c);
                BuildWilderness(c);
                EditorUtility.DisplayProgressBar("CityRise", "Gökyüzü ve ışık...", 0.85f);
                BuildSkyAndLight(c);
                BuildCamera(c);
                BuildPostProcessing(c);

                // Doku içe aktarmaları sırasında URP malzeme anahtar kelimelerini sıfırlayabiliyor; parlayanları yeniden aç.
                foreach (var m in c.emissive)
                {
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    m.EnableKeyword("_EMISSION");
                    EditorUtility.SetDirty(m);
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild(ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityRise: Başlangıç kasabası hazır ({c.treeCount} ağaç). Game sekmesinde Play'e bas; sürükleyerek gez, tekerlekle yakınlaş.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- Veri

        static CityLayout LoadOrCreateLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<CityLayout>(LayoutPath);
            if (layout != null && layout.plots.Count > 0) return layout;
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<CityLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            StartingTownGenerator.Fill(layout);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            return layout;
        }

        static RectInt OpenArea(CityGrid g)
        {
            int r = StartingTownGenerator.OpenRings;
            var min = g.BlockRect(new Vector2Int(-r, -r)).min - Vector2Int.one;
            var max = g.BlockRect(new Vector2Int(r, r)).max + Vector2Int.one;
            return new RectInt(min, max - min);
        }

        /// <summary>Başlangıç işletmelerinin veri dosyaları (yoksa oluşturulur, varsa dokunulmaz).</summary>
        static void EnsureBusinessDefinitions()
        {
            string dir = DataDir + "/Isletmeler";
            EnsureFolder(dir);
            var defs = new (string id, string style, long cost, double income, double expense)[]
            {
                ("simit_bufesi", "commercial/building-c", 1_500, 60, 15),
                ("bakkal", "commercial/building-a", 4_000, 140, 40),
                ("manav", "commercial/building-b", 3_500, 120, 35),
                ("berber", "commercial/building-g", 3_000, 100, 25),
                ("firin", "commercial/building-d", 5_000, 170, 50),
                ("kafe", "commercial/building-h", 6_000, 200, 60),
                ("eczane", "commercial/building-f", 9_000, 260, 80),
            };
            foreach (var d in defs)
            {
                string path = $"{dir}/{d.id}.asset";
                if (AssetDatabase.LoadAssetAtPath<BusinessDefinition>(path) != null) continue;
                var def = ScriptableObject.CreateInstance<BusinessDefinition>();
                def.id = d.id;
                def.displayNameKey = "business." + d.id;
                def.requiredStage = CityStage.Seyyar;
                def.buildCost = d.cost;
                def.incomePerHour = d.income;
                def.expensePerHour = d.expense;
                def.footprint = Vector2Int.one;
                def.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StylePath(d.style));
                AssetDatabase.CreateAsset(def, path);
            }
        }

        // ---------------------------------------------------------------- Zemin ve yollar

        static void BuildGround(Ctx c)
        {
            float size = 8000f;
            Quad("Zemin", c.root, Vector3.zero, new Vector2(size, size), c.grass);

            // Kilitli halkalar: soluk çayır. Açık kasaba alanı normal çim.
            var b = c.g.Bounds;
            Quad("KilitliAlan", c.root, RectCenter(c.g, b) + Vector3.up * 0.02f, RectSize(c.g, b), c.meadow);
            Quad("KasabaZemini", c.root, RectCenter(c.g, c.openArea) + Vector3.up * 0.04f, RectSize(c.g, c.openArea), c.grass);
        }

        static void BuildRoads(Ctx c)
        {
            var b = c.g.Bounds;
            foreach (var r in c.layout.roads)
                if (r.unlockStage <= CityStage.Seyyar || r.main) c.drawnRoads.Add(r.cell);

            // Ana caddeler şehir sınırından kırsala doğru devam eder.
            int extend = Mathf.CeilToInt(CountryRadius / c.g.cellSize);
            foreach (var r in c.layout.roads)
            {
                if (!r.main) continue;
                var cell = r.cell;
                bool edgeX = cell.x == b.xMin || cell.x == b.xMax - 1;
                bool edgeY = cell.y == b.yMin || cell.y == b.yMax - 1;
                if (!(edgeX ^ edgeY)) continue;
                var dir = edgeX ? new Vector2Int(cell.x == b.xMin ? -1 : 1, 0) : new Vector2Int(0, cell.y == b.yMin ? -1 : 1);
                if (c.g.IsRoadColumn(edgeX ? cell.y : cell.x) == false) continue;
                for (int i = 1; i <= extend; i++) c.drawnRoads.Add(cell + dir * i);
            }

            foreach (var cell in c.drawnRoads)
            {
                int mask = 0;
                if (c.drawnRoads.Contains(cell + Vector2Int.up)) mask |= 1;
                if (c.drawnRoads.Contains(cell + Vector2Int.right)) mask |= 2;
                if (c.drawnRoads.Contains(cell + Vector2Int.down)) mask |= 4;
                if (c.drawnRoads.Contains(cell + Vector2Int.left)) mask |= 8;
                var (model, yaw) = RoadTile(mask);

                // Kasaba içinde kavşağa bitişik düz yollarda yaya geçidi.
                if (model == "road-straight" && c.openArea.Contains(cell))
                {
                    var along = yaw == 0f ? Vector2Int.right : Vector2Int.up;
                    if (IsJunction(c, cell + along) || IsJunction(c, cell - along)) model = "road-crossing";
                }
                var go = Spawn(c, $"{KenneyDir}/CityRoads/{model}.fbx", c.roads, c.g.CellCenter(cell), yaw, CityScale);
                go.name = "Yol";
                c.keepClear.Add(CellRect(c.g, cell, 1f));
            }
        }

        static bool IsJunction(Ctx c, Vector2Int cell)
        {
            if (!c.drawnRoads.Contains(cell)) return false;
            int n = 0;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                if (c.drawnRoads.Contains(cell + d)) n++;
            return n >= 3;
        }

        /// <summary>
        /// Komşu maskesine göre Kenney yol parçası ve açısı. Bitler: K=1, D=2, G=4, B=8.
        /// Kenney'de: düz yol B-D yönünde, viraj K-D'yi, T kavşak B-K-D'yi bağlar, çıkmaz B'ye açılır.
        /// </summary>
        static (string model, float yaw) RoadTile(int mask)
        {
            int Rot(int m, int k)
            {
                for (int i = 0; i < k; i++) m = ((m << 1) | (m >> 3)) & 15;
                return m;
            }
            int bits = 0;
            for (int i = 0; i < 4; i++) bits += (mask >> i) & 1;
            switch (bits)
            {
                case 4: return ("road-crossroad", 0f);
                case 3:
                    for (int k = 0; k < 4; k++) if (Rot(1 | 2 | 8, k) == mask) return ("road-intersection", k * 90f);
                    break;
                case 2:
                    if (mask == (2 | 8)) return ("road-straight", 0f);
                    if (mask == (1 | 4)) return ("road-straight", 90f);
                    for (int k = 0; k < 4; k++) if (Rot(1 | 2, k) == mask) return ("road-bend", k * 90f);
                    break;
                case 1:
                    for (int k = 0; k < 4; k++) if (Rot(8, k) == mask) return ("road-end", k * 90f);
                    break;
            }
            return ("road-square", 0f);
        }

        // ---------------------------------------------------------------- Arsalar

        static void BuildPlot(Ctx c, Plot p)
        {
            if (p.type == PlotType.Locked) return;
            var parent = Group(c.plots, $"Arsa_{p.id}_{p.type}");
            var center = c.g.RectCenter(p.cells);
            bool paved = p.type == PlotType.Business || p.type == PlotType.Plaza || p.type == PlotType.Civic;
            if (paved) PaveRect(c, parent, p.cells);
            c.keepClear.Add(PlotRect(c.g, p.cells));

            switch (p.type)
            {
                case PlotType.Business:
                case PlotType.Civic:
                {
                    var building = Spawn(c, StylePath(p.styleId), parent, center, YawFor(p.facing), CityScale);
                    building.name = p.occupantId;
                    if (p.type == PlotType.Civic)
                    {
                        PlanterRow(c, parent, p, 4);
                        Flag(c, parent, PlotPoint(c, p, 0.42f * p.cells.width * c.g.cellSize * 0.5f, 0.45f));
                    }
                    break;
                }
                case PlotType.House:
                    BuildHouse(c, parent, p);
                    break;
                case PlotType.Park:
                    BuildPark(c, parent, p);
                    break;
                case PlotType.Plaza:
                    BuildPlaza(c, parent, p);
                    break;
                case PlotType.ForSale:
                    ForSaleSign(c, parent, p);
                    break;
            }
        }

        static void PaveRect(Ctx c, Transform parent, RectInt r)
        {
            foreach (var cell in r.allPositionsWithin)
                Spawn(c, $"{KenneyDir}/CityRoads/tile-low.fbx", parent, c.g.CellCenter(cell), 0f, CityScale).name = "Tas";
        }

        static void BuildHouse(Ctx c, Transform parent, Plot p)
        {
            float half = p.cells.width * c.g.cellSize * 0.5f;
            var house = Spawn(c, StylePath(p.styleId), parent, PlotPoint(c, p, 0f, 0.6f), YawFor(p.facing), CityScale);
            house.name = "Ev";

            // Kapıdan yola taş yol, arka ve yanlarda çit, bahçede ağaçlar.
            Spawn(c, $"{KenneyDir}/CitySuburban/path-stones-long.fbx", parent, PlotPoint(c, p, 0f, half - 1.4f), YawFor(p.facing), CityScale);
            float fenceLen = 0.48f * CityScale;
            int pieces = Mathf.RoundToInt(2 * half / fenceLen);
            for (int i = 0; i < pieces; i++)
            {
                float t = -half + fenceLen * (i + 0.5f);
                Spawn(c, $"{KenneyDir}/CitySuburban/fence.fbx", parent, PlotPoint(c, p, t, -half + 0.3f), YawFor(p.facing), CityScale);
                if (i < pieces - 1)
                {
                    Spawn(c, $"{KenneyDir}/CitySuburban/fence.fbx", parent, PlotPoint(c, p, -half + 0.3f, t), YawFor(p.facing) + 90f, CityScale);
                    Spawn(c, $"{KenneyDir}/CitySuburban/fence.fbx", parent, PlotPoint(c, p, half - 0.3f, t), YawFor(p.facing) + 90f, CityScale);
                }
            }
            Tree(c, parent, PlotPoint(c, p, -half + 2.8f, -half + 2.8f), 0.85f);
            if (c.rng.NextDouble() < 0.5) Tree(c, parent, PlotPoint(c, p, half - 3f, -half + 3f), 0.7f);
            else Bush(c, parent, PlotPoint(c, p, half - 2.5f, -half + 2f));
            Spawn(c, $"{KenneyDir}/CitySuburban/planter.fbx", parent, PlotPoint(c, p, 3f, half - 2f), YawFor(p.facing), CityScale);
            for (int i = 0; i < 6; i++)
                Flower(c, parent, PlotPoint(c, p, -half + 1.2f + (float)c.rng.NextDouble() * 3f, half - 1.2f - (float)c.rng.NextDouble() * 2f));
        }

        static void BuildPark(Ctx c, Transform parent, Plot p)
        {
            var center = c.g.RectCenter(p.cells);
            var size = RectSize(c.g, p.cells);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;

            // Gölet ve çevresinden geçen taş yollar.
            bool big = p.Area >= 16;
            if (big)
            {
                Disc("Golet", parent, center + new Vector3(3f, 0.06f, -2f), new Vector2(13f, 9f), c.water);
                Disc("GoletKenari", parent, center + new Vector3(3f, 0.03f, -2f), new Vector2(14.6f, 10.6f), c.stone);
            }
            for (float x = -hx + 2f; x < hx - 1f; x += 0.4f * CityScale)
                Spawn(c, $"{KenneyDir}/CitySuburban/path-stones-long.fbx", parent, center + new Vector3(x, 0.05f, big ? 7f : 0f), 90f, CityScale);

            // Yol kenarında ağaç sırası, içeride birkaç küme; aralar açık çimen.
            StreetTrees(c, parent, p, 8f);
            int clusters = big ? 3 : 1;
            for (int k = 0; k < clusters; k++)
            {
                var cc = center + new Vector3(Rand(c, -hx + 6f, hx - 6f), 0, Rand(c, -hz + 6f, hz - 6f));
                if (big && Vector3.Distance(cc, center + new Vector3(3f, 0, -2f)) < 11f) cc = center + new Vector3(-hx + 7f, 0, -hz + 7f + k * 4f);
                for (int i = 0; i < 3; i++)
                    Tree(c, parent, cc + new Vector3(Rand(c, -3.5f, 3.5f), 0, Rand(c, -3.5f, 3.5f)), Rand(c, 0.8f, 1.1f));
            }
            for (int i = 0; i < p.Area * 2; i++)
            {
                var pos = center + new Vector3(Rand(c, -hx + 1f, hx - 1f), 0, Rand(c, -hz + 1f, hz - 1f));
                if (big && Vector3.Distance(pos, center + new Vector3(3f, 0, -2f)) < 8.5f) continue;
                double roll = c.rng.NextDouble();
                if (roll < 0.2) Bush(c, parent, pos); else if (roll < 0.6) Flower(c, parent, pos); else GrassTuft(c, parent, pos);
            }
        }

        static void BuildPlaza(Ctx c, Transform parent, Plot p)
        {
            var center = c.g.RectCenter(p.cells);
            var size = RectSize(c.g, p.cells);
            float h = size.x * 0.5f;

            // Ortada çeşme.
            Cylinder("CesmeHavuzu", parent, center + Vector3.up * 0.3f, new Vector3(8f, 0.3f, 8f), c.stone);
            Cylinder("CesmeSuyu", parent, center + Vector3.up * 0.58f, new Vector3(7.2f, 0.04f, 7.2f), c.water);
            Cylinder("CesmeOrtasi", parent, center + Vector3.up * 1.3f, new Vector3(0.9f, 1.3f, 0.9f), c.stone);
            Cylinder("CesmeCanagi", parent, center + Vector3.up * 2.5f, new Vector3(3f, 0.15f, 3f), c.stone);
            Cylinder("CesmeCanagiSu", parent, center + Vector3.up * 2.64f, new Vector3(2.6f, 0.02f, 2.6f), c.water);
            Cylinder("CesmeTepesi", parent, center + Vector3.up * 3.1f, new Vector3(0.45f, 0.5f, 0.45f), c.stone);

            // Dört köşede çimli bahçe: ağaç, çalı ve çiçek. Aralarında çeşmeye giden yollar kalır.
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var garden = center + new Vector3(sx * (h * 0.5f + 1.5f), 0, sz * (h * 0.5f + 1.5f));
                float gs = h - 6f;
                Box("Bahce", parent, garden + Vector3.up * 0.12f, new Vector3(gs, 0.24f, gs), c.stone);
                Quad("BahceCimi", parent, garden + Vector3.up * 0.245f, new Vector2(gs - 0.5f, gs - 0.5f), c.grass);
                Tree(c, parent, garden + new Vector3(sx * gs * 0.22f, 0.24f, sz * gs * 0.22f), 0.9f);
                for (int i = 0; i < 4; i++)
                    Bush(c, parent, garden + new Vector3(Rand(c, -gs * 0.4f, gs * 0.4f), 0.24f, -sz * gs * 0.35f));
                for (int i = 0; i < 10; i++)
                    Flower(c, parent, garden + new Vector3(Rand(c, -gs * 0.42f, gs * 0.42f), 0.24f, Rand(c, -gs * 0.42f, gs * 0.1f) * sz * -1f));
            }
            // Bahçeler arasındaki yollarda meydan lambaları.
            foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                var pos = center + d * (h - 5f) + Vector3.Cross(d, Vector3.up) * 3f;
                float yaw = Quaternion.LookRotation(-Vector3.Cross(d, Vector3.up)).eulerAngles.y + 180f;
                var lamp = Spawn(c, $"{KenneyDir}/CityRoads/light-curved.fbx", parent, pos + Vector3.up * 0.15f, yaw, CityScale);
                lamp.name = "MeydanLambasi";
                var head = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 5.1f, -1.4f);
                Box("LambaCami", parent, head, new Vector3(0.45f, 0.08f, 0.45f), c.lampGlow);
                if (c.night) PointLight(c, head + Vector3.down * 0.4f, 15f, 12f, new Color(1f, 0.68f, 0.36f));
            }

            for (int i = 0; i < 3; i++)
            {
                var pos = center + new Vector3(-6f + i * 6f, 0.15f, h - 2.2f);
                Spawn(c, $"{KenneyDir}/CityCommercial/detail-parasol-{(i % 2 == 0 ? "a" : "b")}.fbx", parent, pos, 0f, CityScale);
            }
        }

        static void ForSaleSign(Ctx c, Transform parent, Plot p)
        {
            float half = Mathf.Min(p.cells.width, p.cells.height) * c.g.cellSize * 0.5f;
            var pos = PlotPoint(c, p, 0f, half - 1.6f);
            var sign = new GameObject("SatilikTabela").transform;
            sign.SetParent(parent);
            sign.position = pos;
            sign.rotation = Quaternion.Euler(0, YawFor(p.facing) + 180f, 0);
            foreach (var x in new[] { -0.9f, 0.9f })
                Box("Direk", sign, new Vector3(x, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.12f), c.signPost);
            Box("Pano", sign, new Vector3(0, 1.75f, 0.03f), new Vector3(2.4f, 1.1f, 0.06f), c.signPost);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Yazi";
            face.transform.SetParent(sign, false);
            face.transform.localPosition = new Vector3(0, 1.75f, -0.005f);
            face.transform.localScale = new Vector3(2.3f, 1.0f, 1f);
            face.GetComponent<Renderer>().sharedMaterial = c.signBoard;
            face.isStatic = true;
            if (p.Area > 1)
            {
                for (int i = 0; i < 6; i++)
                    Flower(c, parent, c.g.RectCenter(p.cells) + new Vector3(Rand(c, -half + 2, half - 2), 0, Rand(c, -half + 2, half - 2)));
                StreetTrees(c, parent, p, 8f);
                for (int i = 0; i < 4; i++)
                    GrassTuft(c, parent, c.g.RectCenter(p.cells) + new Vector3(Rand(c, -half + 2, half - 2), 0, Rand(c, -half + 2, half - 2)));
            }
        }

        static void PlanterRow(Ctx c, Transform parent, Plot p, int count)
        {
            float half = Mathf.Max(p.cells.width, p.cells.height) * c.g.cellSize * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(-half + 2.5f, half - 2.5f, i / (float)(count - 1));
                if (Mathf.Abs(t) < 4f) continue;
                Spawn(c, $"{KenneyDir}/CitySuburban/planter.fbx", parent, PlotPoint(c, p, t, Mathf.Min(p.cells.width, p.cells.height) * c.g.cellSize * 0.5f - 1.4f), YawFor(p.facing), CityScale);
            }
        }

        static void Flag(Ctx c, Transform parent, Vector3 pos)
        {
            var pole = Box("Bayrak", parent, pos + Vector3.up * 4f, new Vector3(0.12f, 8f, 0.12f), c.signPost);
            var flagMat = SimpleMat("Bayrak", new Color(0.85f, 0.08f, 0.1f), 0.2f);
            Box("BayrakKumasi", pole.transform.parent, pos + new Vector3(0.9f, 7.2f, 0), new Vector3(1.8f, 1.2f, 0.03f), flagMat);
        }

        /// <summary>Açık kasabayı kilitli halkalardan ayıran alçak çit (ana caddelerde açıklık bırakılır).</summary>
        static void BuildBoundaryFence(Ctx c)
        {
            var parent = Group(c.root, "SinirCiti");
            float cs = c.g.cellSize;
            var r = c.openArea;
            float minX = r.xMin * cs - 1.2f, maxX = r.xMax * cs + 1.2f;
            float minZ = r.yMin * cs - 1.2f, maxZ = r.yMax * cs + 1.2f;
            float step = 0.48f * CityScale;
            void Line(Vector3 a, Vector3 b, float yaw)
            {
                float len = Vector3.Distance(a, b);
                int n = Mathf.FloorToInt(len / step);
                for (int i = 0; i < n; i++)
                {
                    var pos = Vector3.Lerp(a, b, (i + 0.5f) / n);
                    var cell = c.g.WorldToCell(pos);
                    bool nearRoad = false;
                    for (int dx = -1; dx <= 1 && !nearRoad; dx++)
                    for (int dy = -1; dy <= 1 && !nearRoad; dy++)
                    {
                        var n2 = cell + new Vector2Int(dx, dy);
                        if (!c.openArea.Contains(n2) && c.drawnRoads.Contains(n2)) nearRoad = true;
                    }
                    if (nearRoad) continue;
                    Spawn(c, $"{KenneyDir}/CitySuburban/fence.fbx", parent, pos, yaw, CityScale);
                }
            }
            Line(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), 0f);
            Line(new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ), 0f);
            Line(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), 90f);
            Line(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), 90f);
        }

        // ---------------------------------------------------------------- Sokak lambaları

        static void BuildStreetLamps(Ctx c)
        {
            var parent = Group(c.root, "SokakLambalari");
            foreach (var cell in c.drawnRoads)
            {
                if (!c.openArea.Contains(cell)) continue;
                if (((cell.x + cell.y) & 1) != 0) continue;
                bool alongX = c.drawnRoads.Contains(cell + Vector2Int.left) && c.drawnRoads.Contains(cell + Vector2Int.right)
                              && !c.drawnRoads.Contains(cell + Vector2Int.up) && !c.drawnRoads.Contains(cell + Vector2Int.down);
                bool alongZ = c.drawnRoads.Contains(cell + Vector2Int.up) && c.drawnRoads.Contains(cell + Vector2Int.down)
                              && !c.drawnRoads.Contains(cell + Vector2Int.left) && !c.drawnRoads.Contains(cell + Vector2Int.right);
                if (!alongX && !alongZ) continue;

                // Lamba kaldırımda, kolu yola bakar. Kenney lambasının kolu -Z yönünde.
                float side = ((cell.x / 2 + cell.y / 2) & 1) == 0 ? 1f : -1f;
                var center = c.g.CellCenter(cell);
                Vector3 offset = alongX ? new Vector3(0, 0, side * 3.45f) : new Vector3(side * 3.45f, 0, 0);
                float yaw = alongX ? (side > 0 ? 0f : 180f) : (side > 0 ? 90f : 270f);
                var lamp = Spawn(c, $"{KenneyDir}/CityRoads/light-square.fbx", parent, center + offset, yaw, CityScale);
                lamp.name = "Lamba";
                var head = center + offset + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 4.55f, -1.55f);
                Box("LambaCami", parent, head, new Vector3(0.5f, 0.08f, 0.35f), c.lampGlow).isStatic = true;
                if (c.night && c.lightCount < 48)
                    PointLight(c, head + Vector3.down * 0.4f, 16f, 14f, new Color(1f, 0.68f, 0.36f));
            }
        }

        static void PointLight(Ctx c, Vector3 pos, float range, float intensity, Color color)
        {
            var l = new GameObject("Isik").AddComponent<Light>();
            l.transform.SetParent(c.lights);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            c.lightCount++;
        }

        // ---------------------------------------------------------------- Doğa

        static readonly string[] FlowerModels =
        {
            "flower_redA", "flower_redB", "flower_yellowA", "flower_yellowB", "flower_purpleA", "flower_purpleB", "flower_redC", "flower_yellowC",
        };
        static readonly string[] RockModels = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_smallA", "rock_smallB", "rock_smallC" };

        /// <summary>Gerçek yaprak dokulu ağaç (bkz. TreeFactory). scale 1 ≈ 8-10 m.</summary>
        static GameObject Tree(Ctx c, Transform parent, Vector3 pos, float scale, bool pine = false)
        {
            var list = pine ? c.trees.pine : c.trees.broadleaf;
            c.treeCount++;
            return SpawnPrefab(list[c.rng.Next(list.Count)], parent, pos, Rand(c, 0, 360), scale);
        }

        static void Bush(Ctx c, Transform parent, Vector3 pos) =>
            SpawnPrefab(c.trees.bush[c.rng.Next(c.trees.bush.Count)], parent, pos, Rand(c, 0, 360), Rand(c, 0.75f, 1.2f));

        static void GrassTuft(Ctx c, Transform parent, Vector3 pos) =>
            SpawnPrefab(c.trees.grass[c.rng.Next(c.trees.grass.Count)], parent, pos, Rand(c, 0, 360), Rand(c, 0.8f, 1.4f));

        static void Flower(Ctx c, Transform parent, Vector3 pos) =>
            Spawn(c, $"{KenneyDir}/Nature/{FlowerModels[c.rng.Next(FlowerModels.Length)]}.fbx", parent, pos, Rand(c, 0, 360), NatureScale * Rand(c, 0.8f, 1.2f));

        static GameObject SpawnPrefab(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        /// <summary>
        /// Arsanın yola bakan kenarları boyunca düzenli ağaç sırası (kaldırım ağaçları).
        /// </summary>
        static void StreetTrees(Ctx c, Transform parent, Plot p, float spacing)
        {
            float cs = c.g.cellSize;
            var r = p.cells;
            var sides = new[]
            {
                (new Vector2Int(0, -1), new Vector3(r.xMin * cs, 0, r.yMin * cs + 2f), Vector3.right, r.width * cs),
                (new Vector2Int(0, 1), new Vector3(r.xMin * cs, 0, r.yMax * cs - 2f), Vector3.right, r.width * cs),
                (new Vector2Int(-1, 0), new Vector3(r.xMin * cs + 2f, 0, r.yMin * cs), Vector3.forward, r.height * cs),
                (new Vector2Int(1, 0), new Vector3(r.xMax * cs - 2f, 0, r.yMin * cs), Vector3.forward, r.height * cs),
            };
            foreach (var (outward, start, along, length) in sides)
            {
                var probe = outward.x != 0
                    ? new Vector2Int(outward.x < 0 ? r.xMin - 1 : r.xMax, r.yMin + r.height / 2)
                    : new Vector2Int(r.xMin + r.width / 2, outward.y < 0 ? r.yMin - 1 : r.yMax);
                if (!c.drawnRoads.Contains(probe)) continue;
                int n = Mathf.Max(1, Mathf.FloorToInt((length - 4f) / spacing));
                float pad = (length - (n - 1) * spacing) * 0.5f;
                for (int i = 0; i < n; i++)
                    Tree(c, parent, start + along * (pad + i * spacing), Rand(c, 0.85f, 0.95f));
            }
        }

        /// <summary>Kilitli halkalar: aralarında açık çayır kalan ağaç kümeleri, çiçek, çim öbeği ve kayalar.</summary>
        static void BuildWilderness(Ctx c)
        {
            var parent = Group(c.nature, "KilitliHalkalar");
            var b = c.g.Bounds;
            float cs = c.g.cellSize;
            var area = new Rect(b.xMin * cs, b.yMin * cs, b.width * cs, b.height * cs);
            var open = PlotRect(c.g, c.openArea);
            open.min -= Vector2.one * 3f;
            open.max += Vector2.one * 3f;
            Groves(c, parent, area, open, 34, 8f, 20f, 0.2f);
            MeadowDetails(c, parent, area, open, 1600);
        }

        /// <summary>Şehrin dışı: tarlalar, ana yollar boyunca ağaç sıraları, uzakta korular.</summary>
        static void BuildCountryside(Ctx c)
        {
            var parent = Group(c.nature, "Kirsal");
            var b = c.g.Bounds;
            float cs = c.g.cellSize;
            var city = new Rect(b.xMin * cs, b.yMin * cs, b.width * cs, b.height * cs);

            // Ana caddelerin iki yanında tarlalar.
            var crops = new[] { "crops_wheatStageB", "crops_cornStageD", "crops_wheatStageA", "crops_leafsStageB", "crops_cornStageC" };
            int fieldIndex = 0;
            foreach (var dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                for (int k = 0; k < 2; k++)
                {
                    float dist = city.width * 0.5f + 25f + k * 58f;
                    var perp = new Vector2(-dir.y, dir.x);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        var fc = dir * (dist + 22f) + perp * s * 34f;
                        var fsize = dir.x != 0 ? new Vector2(44f, 50f) : new Vector2(50f, 44f);
                        Field(c, parent, new Vector3(fc.x, 0, fc.y), fsize, crops[fieldIndex++ % crops.Length], dir.x != 0);
                    }
                }
            }

            // Şehir dışındaki ana yolların iki yanında düzenli ağaç sırası.
            foreach (var cell in c.drawnRoads)
            {
                var world = c.g.CellCenter(cell);
                var p2 = new Vector2(world.x, world.z);
                if (city.Contains(p2) || p2.magnitude > CountryRadius * 0.85f) continue;
                bool alongX = c.drawnRoads.Contains(cell + Vector2Int.left) || c.drawnRoads.Contains(cell + Vector2Int.right);
                if (((alongX ? cell.x : cell.y) & 1) != 0) continue;
                var side = alongX ? Vector3.forward : Vector3.right;
                foreach (var s in new[] { -1f, 1f })
                {
                    var pos = world + side * s * 6.5f;
                    if (InKeepClear(c, new Vector2(pos.x, pos.z))) continue;
                    Tree(c, parent, pos, Rand(c, 0.85f, 1.05f));
                }
            }

            var all = new Rect(-CountryRadius, -CountryRadius, CountryRadius * 2, CountryRadius * 2);
            var exclude = city;
            exclude.min -= Vector2.one * 6f;
            exclude.max += Vector2.one * 6f;
            Groves(c, parent, all, exclude, 46, 14f, 38f, 0.5f);
            MeadowDetails(c, parent, all, exclude, 1200);
        }

        static void Field(Ctx c, Transform parent, Vector3 center, Vector2 size, string crop, bool rowsAlongZ)
        {
            var field = Group(parent, "Tarla");
            Quad("Toprak", field, center + Vector3.up * 0.05f, size, c.dirt);
            c.keepClear.Add(new Rect(center.x - size.x * 0.5f - 2f, center.z - size.y * 0.5f - 2f, size.x + 4f, size.y + 4f));
            float spacing = 2.4f;
            for (float a = -size.x * 0.5f + 1.5f; a < size.x * 0.5f - 1f; a += rowsAlongZ ? spacing * 1.6f : spacing)
            for (float d = -size.y * 0.5f + 1.5f; d < size.y * 0.5f - 1f; d += rowsAlongZ ? spacing : spacing * 1.6f)
                Spawn(c, $"{KenneyDir}/Nature/{crop}.fbx", field, center + new Vector3(a, 0.05f, d), (c.rng.Next(4)) * 90f, NatureScale * 0.9f).isStatic = true;
        }

        /// <summary>
        /// Korular: rastgele merkezlerde, ortası sık kenarı seyrek ağaç kümeleri; kenarlarında çalılar.
        /// Kümelerin arası açık çayır kalır.
        /// </summary>
        static void Groves(Ctx c, Transform parent, Rect area, Rect exclude, int count, float minRadius, float maxRadius, float pineChance)
        {
            for (int g = 0; g < count; g++)
            {
                Vector2 center = Vector2.zero;
                bool found = false;
                for (int attempt = 0; attempt < 30 && !found; attempt++)
                {
                    center = new Vector2(Rand(c, area.xMin, area.xMax), Rand(c, area.yMin, area.yMax));
                    found = !exclude.Contains(center) && center.magnitude < CountryRadius - 20f && !Blocked(c, center);
                }
                if (!found) continue;
                float radius = Rand(c, minRadius, maxRadius);
                bool pineGrove = c.rng.NextDouble() < pineChance;
                int trees = Mathf.RoundToInt(radius * radius * 0.03f) + 2;
                for (int t = 0; t < trees; t++)
                {
                    float a = Rand(c, 0, Mathf.PI * 2f);
                    float d = radius * Mathf.Pow((float)c.rng.NextDouble(), 0.7f);
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (exclude.Contains(p) || p.magnitude > CountryRadius || Blocked(c, p)) continue;
                    bool pine = pineGrove ? c.rng.NextDouble() < 0.8 : c.rng.NextDouble() < 0.08;
                    Tree(c, parent, new Vector3(p.x, 0, p.y), Rand(c, 0.85f, 1.25f), pine);
                }
                for (int k = 0; k < trees / 2 + 1; k++)
                {
                    float a = Rand(c, 0, Mathf.PI * 2f);
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Rand(c, 0.85f, 1.15f);
                    if (exclude.Contains(p) || Blocked(c, p)) continue;
                    Bush(c, parent, new Vector3(p.x, 0, p.y));
                }
            }
        }

        /// <summary>Çayır ayrıntıları: çim öbekleri, çiçekler, birkaç kaya ve tek tük ağaç.</summary>
        static void MeadowDetails(Ctx c, Transform parent, Rect area, Rect exclude, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                var p = new Vector2(Rand(c, area.xMin, area.xMax), Rand(c, area.yMin, area.yMax));
                if (exclude.Contains(p) || p.magnitude > 340f || Blocked(c, p)) continue;
                var pos = new Vector3(p.x, 0, p.y);
                double roll = c.rng.NextDouble();
                if (roll < 0.03) Tree(c, parent, pos, Rand(c, 0.9f, 1.3f));
                else if (roll < 0.5) GrassTuft(c, parent, pos);
                else if (roll < 0.85) Flower(c, parent, pos);
                else if (roll < 0.97) Bush(c, parent, pos);
                else Spawn(c, $"{KenneyDir}/Nature/{RockModels[c.rng.Next(RockModels.Length)]}.fbx", parent, pos, Rand(c, 0, 360), NatureScale * Rand(c, 0.5f, 1f));
            }
        }

        static bool InKeepClear(Ctx c, Vector2 p)
        {
            foreach (var r in c.keepClear)
                if (r.Contains(p)) return true;
            return false;
        }

        static bool Blocked(Ctx c, Vector2 p)
        {
            foreach (var r in c.keepClear)
                if (r.Contains(p)) return true;
            var cell = c.g.WorldToCell(new Vector3(p.x, 0, p.y));
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var n = cell + new Vector2Int(dx, dy);
                if (!c.drawnRoads.Contains(n)) continue;
                var r = CellRect(c.g, n, 2.5f);
                if (r.Contains(p)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- Gökyüzü, ışık, kamera

        static void BuildSkyAndLight(Ctx c)
        {
            bool night = c.night;
            var sun = new GameObject(night ? "Ay" : "Gunes").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = night ? 0.6f : 0.8f;
            sun.transform.rotation = night ? Quaternion.Euler(50f, -60f, 0) : Quaternion.Euler(38f, -25f, 0);
            sun.color = night ? new Color(0.6f, 0.7f, 1f) : new Color(1f, 0.9f, 0.76f);
            sun.intensity = night ? 0.35f : 2.1f;

            var sky = SkyboxMat(c, SkyPath(night));
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = night ? 0.7f : 1.05f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = night ? 0.35f : 1f;

            // Uzak sis gökyüzünün ufuk rengine karışır; kasabanın sonu ufukta kaybolur.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = night ? new Color(0.07f, 0.09f, 0.16f) : new Color(0.70f, 0.81f, 0.95f);
            RenderSettings.fogStartDistance = night ? 160f : 300f;
            RenderSettings.fogEndDistance = night ? 700f : 1600f;
            DynamicGI.UpdateEnvironment();

            var probe = new GameObject("YansimaProbu").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0, 12f, 0);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(420f, 120f, 420f);
            probe.resolution = 128;
            probe.boxProjection = false;

            if (night)
                PointLight(c, new Vector3(0, 4f, 0), 16f, 10f, new Color(1f, 0.82f, 0.55f));
        }

        static void BuildCamera(Ctx c)
        {
            var rig = new GameObject("KameraRig");
            rig.transform.position = new Vector3(0, 0, -8f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 2500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var ctrl = rig.AddComponent<CityCameraController>();
            ctrl.cam = cam;
            ctrl.minDistance = 30f;
            ctrl.maxDistance = 220f;
            ctrl.distance = 70f;
            ctrl.tiltWithZoom = true;
            ctrl.pitchNear = 12f;
            ctrl.pitchFar = 55f;
            ctrl.yaw = 35f;
            float reach = (c.openArea.width * 0.5f + 6f) * c.g.cellSize;
            ctrl.boundsMin = new Vector2(-reach, -reach);
            ctrl.boundsMax = new Vector2(reach, reach);
        }

        static void BuildPostProcessing(Ctx c)
        {
            bool night = c.night;
            var volume = new GameObject("RenkAyarlari").AddComponent<Volume>();
            volume.isGlobal = true;
            string path = $"{GeneratedDir}/KasabaRenk_{(night ? "Gece" : "Gunduz")}.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(night ? 1.3f : 0.35f);
            bloom.threshold.Override(night ? 0.85f : 1.05f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(night ? -0.1f : 0.35f);
            color.contrast.Override(night ? 8f : 12f);
            color.saturation.Override(night ? 5f : 10f);
            color.colorFilter.Override(night ? new Color(0.96f, 0.97f, 1f) : new Color(1f, 0.985f, 0.96f));
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(0f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(night ? new Vector4(0.9f, 0.95f, 1.1f, 0f) : new Vector4(0.95f, 0.97f, 1.08f, 0f));
            smh.highlights.Override(night ? new Vector4(1f, 1f, 1f, 0f) : new Vector4(1.04f, 1.0f, 0.95f, 0f));
            profile.Add<Vignette>(true).intensity.Override(0.18f);

            foreach (var comp in profile.components)
            {
                comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// Projedeki URP ayarları: gölge mesafesi ve yumuşak gölge, SSAO (ortam gölgesi) özelliği.
        /// Ayar dosyalarına bir kez eklenir; tekrar çalıştırmak bir şey değiştirmez.
        /// </summary>
        static void SetupRendering()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                asset.shadowDistance = Mathf.Max(asset.shadowDistance, 220f);
                asset.shadowCascadeCount = Mathf.Max(asset.shadowCascadeCount, 2);
                var so = new SerializedObject(asset);
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            var ssaoType = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (ssaoType == null)
            {
                Debug.LogWarning("CityRise: SSAO sınıfı bulunamadı; ortam gölgesi eklenmedi.");
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null || data.rendererFeatures.Exists(f => f != null && f.GetType() == ssaoType)) continue;
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
                feature.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                var map = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (map != null && map.GetValue(data) is List<long> ids &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                    ids.Add(localId);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
        }

        // ---------------------------------------------------------------- Malzemeler

        static readonly string[] BuildingKits = { "CityCommercial", "CitySuburban", "CityIndustrial" };

        /// <summary>
        /// Önce tüm dokular hazırlanır/içe aktarılır, sonra malzemeler oluşturulup kaydedilir.
        /// (Doku içe aktarma sırasında Unity kaydedilmemiş malzeme değişikliklerini diskten geri yükleyebiliyor.)
        /// </summary>
        static void CreateMaterials(Ctx c)
        {
            var grassTex = LoadTexture($"{PolyHavenDir}/Textures/leafy_grass_diff_2k.jpg", false);
            var grassNor = LoadTexture($"{PolyHavenDir}/Textures/leafy_grass_nor_gl_2k.jpg", true);
            var signTex = SignTexture();
            NeutralRoadPalette();
            PrepareHdri(SkyPath(c.night));
            if (c.night)
                foreach (var kit in BuildingKits)
                    WindowGlowMap(kit, AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenneyDir}/{kit}/Textures/colormap.png"));

            c.grass = TexturedMat("Cim", grassTex, grassNor, new Color(0.62f, 0.90f, 0.46f), 4f);
            c.meadow = TexturedMat("Cayir", grassTex, grassNor, new Color(0.68f, 0.88f, 0.46f), 4f);
            c.water = SimpleMat("Su", new Color(0.22f, 0.55f, 0.72f), 0.95f);
            c.stone = SimpleMat("Tas", new Color(0.78f, 0.75f, 0.7f), 0.25f);
            c.dirt = SimpleMat("Toprak", new Color(0.45f, 0.32f, 0.2f), 0.1f);
            c.signBoard = SimpleMat("TabelaPano", Color.white, 0.3f, m => m.SetTexture("_BaseMap", signTex));
            c.signPost = SimpleMat("TabelaDirek", new Color(0.35f, 0.24f, 0.16f), 0.2f);
            c.lampGlow = SimpleMat("LambaCami", new Color(1f, 0.9f, 0.7f), 0.5f, m =>
            {
                if (c.night) SetEmission(c, m, null, new Color(1f, 0.78f, 0.45f) * 6f);
            }, persist: !c.night);
            foreach (var kit in BuildingKits) KenneyMat(c, kit, null);
            KenneyMat(c, "CityRoads", null);
            AssetDatabase.SaveAssets();
        }

        /// <summary>"SATILIK" yazılı tabela dokusu (yazı tipi gerektirmeyen basit piksel harflerle).</summary>
        static Texture2D SignTexture()
        {
            string path = $"{GeneratedDir}/SatilikTabela.png";
            if (!File.Exists(FullPath(path)))
            {
                var glyphs = new Dictionary<char, string[]>
                {
                    { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
                    { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
                    { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
                    { 'I', new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" } },
                    { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
                    { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
                };
                const string word = "SATILIK";
                const int w = 256, h = 112, px = 5, border = 6;
                var paper = new Color32(250, 247, 238, 255);
                var red = new Color32(205, 32, 28, 255);
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x < border || y < border || x >= w - border || y >= h - border;
                    pixels[y * w + x] = edge ? red : paper;
                }
                int textW = word.Length * 6 * px - px;
                int x0 = (w - textW) / 2, y0 = (h - 7 * px) / 2;
                for (int i = 0; i < word.Length; i++)
                {
                    var g = glyphs[word[i]];
                    for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (g[gy][gx] != '1') continue;
                        for (int sy = 0; sy < px; sy++)
                        for (int sx = 0; sx < px; sx++)
                        {
                            int x = x0 + (i * 6 + gx) * px + sx;
                            int y = y0 + (6 - gy) * px + sy;
                            pixels[y * w + x] = red;
                        }
                    }
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SetEmission(Ctx c, Material mat, Texture map, Color color)
        {
            mat.SetTexture("_EmissionMap", map);
            mat.SetColor("_EmissionColor", color);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.EnableKeyword("_EMISSION");
            if (!c.emissive.Contains(mat)) c.emissive.Add(mat);
        }

        /// <summary>
        /// Malzemeyi her kurulumda baştan oluşturup hemen diske yazar; böylece sonraki doku
        /// içe aktarmaları kaydedilmemiş değişiklikleri (ör. gece parlaması) silemez.
        /// </summary>
        static Material SimpleMat(string name, Color color, float smoothness, Action<Material> configure = null, bool persist = true)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (!persist)
            {
                // Sahnenin içine kaydedilir. URP, dosya olarak kaydedilen malzemelerde parlama
                // anahtar kelimesini içe aktarma sırasında kapatabildiği için gece malzemeleri böyle tutulur.
                configure?.Invoke(mat);
                return mat;
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            // Anahtar kelimeler (parlama, normal haritası) kayıttan SONRA açılıp hemen yazılmalı;
            // CreateAsset sırasında URP doğrulaması onları kapatıyor.
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material TexturedMat(string name, Texture2D albedo, Texture2D normal, Color tint, float meters)
        {
            return SimpleMat(name, tint, 0.05f, mat =>
            {
                mat.SetTexture("_BaseMap", albedo);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 0.6f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetTextureScale("_BaseMap", Vector2.one / meters);
            });
        }

        /// <summary>
        /// Kenney kitlerinin ortak renk paleti için malzeme. Gece sürümünde paletteki cam renkleri
        /// (açık mavi sütunlar) sıcak ışıkla parlar; böylece pencereler yanar.
        /// </summary>
        static Material KenneyMat(Ctx c, string kit, Texture original)
        {
            string key = kit + (c.night ? "_Gece" : "");
            if (c.kenneyMats.TryGetValue(key, out var cached)) return cached;
            var tex = kit == "CityRoads" ? NeutralRoadPalette()
                : AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenneyDir}/{kit}/Textures/colormap.png") ?? original as Texture2D;
            bool glow = c.night && kit != "CityRoads";
            var glowMap = glow ? WindowGlowMap(kit, tex) : null;
            var mat = SimpleMat("Kenney_" + key, Color.white, 0.22f, m =>
            {
                m.SetTexture("_BaseMap", tex);
                if (glow) SetEmission(c, m, glowMap, new Color(1f, 0.66f, 0.32f) * 1.1f);
            }, persist: !glow);
            c.kenneyMats[key] = mat;
            return mat;
        }

        /// <summary>Doğa kitinin düz renkli malzemeleri için daha sıcak, Hay Day'e yakın bir palet.</summary>
        static readonly Dictionary<string, Color> NaturePalette = new Dictionary<string, Color>
        {
            { "grass", new Color(0.42f, 0.74f, 0.25f) },
            { "leafsGreen", new Color(0.36f, 0.70f, 0.22f) },
            { "leafsDark", new Color(0.19f, 0.49f, 0.21f) },
            { "leafsFall", new Color(0.95f, 0.58f, 0.16f) },
            { "woodBark", new Color(0.47f, 0.31f, 0.18f) },
            { "woodBarkDark", new Color(0.34f, 0.22f, 0.14f) },
            { "woodBirch", new Color(0.88f, 0.85f, 0.78f) },
            { "woodInner", new Color(0.84f, 0.66f, 0.40f) },
            { "woodDark", new Color(0.30f, 0.20f, 0.12f) },
            { "dirt", new Color(0.55f, 0.38f, 0.22f) },
            { "dirtDark", new Color(0.40f, 0.27f, 0.16f) },
            { "_defaultMat", new Color(0.64f, 0.63f, 0.60f) },
            { "colorRed", new Color(0.92f, 0.20f, 0.18f) },
            { "colorYellow", new Color(1f, 0.83f, 0.20f) },
            { "colorWhite", new Color(0.97f, 0.97f, 0.95f) },
            { "colorTan", new Color(0.86f, 0.72f, 0.50f) },
            { "colorPurple", new Color(0.62f, 0.36f, 0.88f) },
            { "corn", new Color(0.98f, 0.82f, 0.30f) },
        };

        static Material NatureMat(Ctx c, string importedName)
        {
            string name = importedName.Replace(" (Instance)", "");
            if (c.natureMats.TryGetValue(name, out var cached)) return cached;
            var color = NaturePalette.TryGetValue(name, out var col) ? col : new Color(0.6f, 0.6f, 0.6f);
            var mat = SimpleMat("Doga_" + name, color, name.StartsWith("leafs") || name == "grass" ? 0.18f : 0.08f);
            c.natureMats[name] = mat;
            return mat;
        }

        /// <summary>
        /// Kenney yol paletindeki mavi-mor griler nötr asfalt ve beton grisine çevrilir
        /// (çizgiler, tabelalar gibi doygun renkler korunur).
        /// </summary>
        static Texture2D NeutralRoadPalette()
        {
            string path = $"{GeneratedDir}/Kenney_CityRoads_Notr.png";
            if (!File.Exists(FullPath(path)))
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(FullPath($"{KenneyDir}/CityRoads/Textures/colormap.png")));
                var px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    int mx = Math.Max(c.r, Math.Max(c.g, c.b)), mn = Math.Min(c.r, Math.Min(c.g, c.b));
                    if (c.b >= c.r && c.b - c.r < 90 && mx - mn < 95)
                    {
                        float l = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                        l = Mathf.Pow(l, 1.1f) * 1.05f;
                        px[i] = new Color32((byte)Mathf.Clamp(l * 255f * 1.02f, 0, 255), (byte)Mathf.Clamp(l * 255f, 0, 255), (byte)Mathf.Clamp(l * 255f * 0.96f, 0, 255), c.a);
                    }
                }
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(src);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D WindowGlowMap(string kit, Texture2D colormap)
        {
            string path = $"{GeneratedDir}/Kenney_{kit}_PencereIsigi.png";
            if (!File.Exists(FullPath(path)))
            {
                // Paletteki mavi cam tonları parlar; diğer renkler karanlık kalır.
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(FullPath(AssetDatabase.GetAssetPath(colormap))));
                var px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    bool glass = p.b > 175 && p.g > p.r + 8 && p.b > p.r + 25;
                    px[i] = glass ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
                }
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                Object.DestroyImmediate(src);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string SkyPath(bool night) =>
            $"{PolyHavenDir}/HDRI/{(night ? "qwantani_night_puresky" : "kloofendal_43d_clear_puresky")}_2k.hdr";

        static void PrepareHdri(string hdrPath)
        {
            var imp = AssetImporter.GetAtPath(hdrPath) as TextureImporter;
            if (imp != null && (imp.mipmapEnabled || imp.wrapModeV != TextureWrapMode.Clamp || imp.maxTextureSize != 2048))
            {
                imp.textureShape = TextureImporterShape.Texture2D;
                imp.mipmapEnabled = false;
                imp.wrapModeU = TextureWrapMode.Repeat;
                imp.wrapModeV = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
        }

        static Material SkyboxMat(Ctx c, string hdrPath)
        {
            string path = $"{GeneratedDir}/Gokyuzu_{(c.night ? "Gece" : "Gunduz")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath));
            mat.SetFloat("_Mapping", 1f);
            mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Exposure", c.night ? 0.45f : 1.0f);
            mat.SetFloat("_Rotation", c.night ? 0f : 200f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D LoadTexture(string path, bool normal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != type || imp.wrapMode != TextureWrapMode.Repeat)
            {
                imp.textureType = type;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- Yardımcılar

        static string StylePath(string styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return null;
            int slash = styleId.IndexOf('/');
            string kit = styleId.Substring(0, slash);
            string model = styleId.Substring(slash + 1);
            string folder = kit switch
            {
                "commercial" => "CityCommercial",
                "suburban" => "CitySuburban",
                "industrial" => "CityIndustrial",
                "roads" => "CityRoads",
                _ => "Nature",
            };
            return $"{KenneyDir}/{folder}/{model}.fbx";
        }

        static GameObject Spawn(Ctx c, string assetPath, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var prefab = assetPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"CityRise: Model bulunamadı: {assetPath}");
                var missing = new GameObject("Eksik");
                missing.transform.SetParent(parent);
                return missing;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;

            // Kenney şehir kitlerinde palet malzemesini kendi malzememizle değiştir (gece pencereleri için).
            string kit = Path.GetFileName(Path.GetDirectoryName(assetPath));
            bool cityKit = kit.StartsWith("City");
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            if (kit == "Nature")
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null) mats[i] = NatureMat(c, mats[i].name);
                    r.sharedMaterials = mats;
                }
            }
            if (cityKit)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("colormap"))
                            mats[i] = KenneyMat(c, kit, mats[i].mainTexture);
                    r.sharedMaterials = mats;
                }
            }
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            return t;
        }

        static float Rand(Ctx c, float min, float max) => min + (float)c.rng.NextDouble() * (max - min);

        static float YawFor(Facing f) => Plot.FacingYaw(f) + ModelFrontYaw;

        /// <summary>Arsanın içindeki bir nokta: along = sağa doğru, forward = ön yüze (yola) doğru metre.</summary>
        static Vector3 PlotPoint(Ctx c, Plot p, float along, float forward)
        {
            var f = Plot.FacingDirection(p.facing);
            var front = new Vector3(f.x, 0, f.y);
            var right = new Vector3(front.z, 0, -front.x);
            return c.g.RectCenter(p.cells) + right * along + front * forward;
        }

        static Vector3 RectCenter(CityGrid g, RectInt r) => g.RectCenter(r);

        static Vector2 RectSize(CityGrid g, RectInt r) => new Vector2(r.width * g.cellSize, r.height * g.cellSize);

        static Rect PlotRect(CityGrid g, RectInt r) => new Rect(r.xMin * g.cellSize, r.yMin * g.cellSize, r.width * g.cellSize, r.height * g.cellSize);

        static Rect CellRect(CityGrid g, Vector2Int cell, float margin) =>
            new Rect(cell.x * g.cellSize - margin, cell.y * g.cellSize - margin, g.cellSize + 2 * margin, g.cellSize + 2 * margin);

        static GameObject Quad(string name, Transform parent, Vector3 center, Vector2 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            var mesh = new Mesh { name = name };
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            mesh.vertices = new[] { new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz) };
            // UV metre cinsinden: doku ölçeği malzemenin tiling'inden gelir.
            mesh.uv = new[] { new Vector2(center.x - hx, center.z - hz), new Vector2(center.x - hx, center.z + hz), new Vector2(center.x + hx, center.z + hz), new Vector2(center.x + hx, center.z - hz) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static GameObject Disc(string name, Transform parent, Vector3 pos, Vector2 size, Material mat) =>
            Cylinder(name, parent, pos, new Vector3(size.x, 0.03f, size.y), mat);

        static string FullPath(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

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
