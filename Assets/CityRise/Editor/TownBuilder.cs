using System;
using System.Collections.Generic;
using System.IO;
using CityRise.CameraRig;
using CityRise.City;
using CityRise.Core;
using CityRise.Game;
using CityRise.Progression;
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
            /// <summary>Gündüz kurulumu: gece için gereken her şey de hazırlanır, oyun sırasında gün-gece döngüsü çalışır.</summary>
            public bool dynamic;
            public Random rng = new Random(7);
            public Transform root, roads, plots, nature, lights;
            public HashSet<Vector2Int> drawnRoads = new HashSet<Vector2Int>();
            public HashSet<Vector2Int> allRoads = new HashSet<Vector2Int>();
            public RectInt openArea;
            public Material grass, meadow, water, stone, dirt, signBoard, signPost, lampGlow;
            public readonly Dictionary<string, Material> kenneyMats = new Dictionary<string, Material>();
            public readonly Dictionary<string, Material> natureMats = new Dictionary<string, Material>();
            public List<Rect> keepClear = new List<Rect>();
            public List<Material> emissive = new List<Material>();
            public int lightCount;
            public TreeFactory.Set trees;
            public BusinessCatalog catalog;
            public ProgressionConfig config;
            public CityCameraController cameraCtrl;
            public Transform wilderness;
            public GameObject signTemplate;
            public readonly Dictionary<string, Material> signMats = new Dictionary<string, Material>();
            public Material glass, roofTiles, cropWheat, cropCorn, paving;
            public Material[] bricks, plasters;
            public readonly Dictionary<string, Mesh> skinned = new Dictionary<string, Mesh>();
            public readonly Dictionary<string, (Color32[] px, int w, int h)> palettes = new Dictionary<string, (Color32[] px, int w, int h)>();
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

                var c = new Ctx { night = night, dynamic = !night };
                shopGlass = null;
                c.layout = LoadOrCreateLayout();
                c.g = c.layout.grid;
                c.openArea = OpenArea(c.g);
                SetupRendering();
                LandscapeSetup.Apply(selectGameView: false);

                // Malzemeler yeni sahneden sonra: sahnede tutulan gece malzemeleri sahne değişince silinir.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorUtility.DisplayProgressBar("CityRise", "Ağaçlar hazırlanıyor (ilk seferde biraz sürer)...", 0.1f);
                c.trees = TreeFactory.Ensure();
                // Yeni sahne kullanılmayan varlıkları bellekten attığı için oyun verisi sahneden sonra yüklenir.
                (c.catalog, c.config) = EnsureGameData();
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
                BuildGame(c, town);
                if (c.dynamic) BuildDayNight(c);

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

        static RectInt OpenArea(CityGrid g) => OpenArea(g, StartingTownGenerator.OpenRings);

        static RectInt OpenArea(CityGrid g, int r)
        {
            r = Mathf.Min(r, g.radiusBlocks);
            var min = g.BlockRect(new Vector2Int(-r, -r)).min - Vector2Int.one;
            var max = g.BlockRect(new Vector2Int(r, r)).max + Vector2Int.one;
            return new RectInt(min, max - min);
        }

        /// <summary>İşletme verisinin sürümü; değerler değişirse artır (eski dosyalar güncellenir).</summary>
        const int BusinessDataVersion = 2;
        const string CatalogPath = DataDir + "/IsletmeKatalogu.asset";
        const string ConfigPath = DataDir + "/IlerlemeAyarlari.asset";

        /// <summary>
        /// Satın alınabilir işletmeler. Net gelir saatlik $; gelir = net × 1,4, gider = net × 0,4.
        /// Başlangıç nakdi $3.250 ile ilk alım simit büfesidir.
        /// </summary>
        static readonly (string id, string style, string sign, int level, long cost, double net, int size, bool brick)[] BusinessTable =
        {
            ("simit_bufesi", "commercial/building-c", "SİMİT", 1, 1_500, 300, 1, false),
            ("bakkal", "commercial/building-a", "BAKKAL", 3, 4_000, 700, 1, true),
            ("manav", "commercial/building-b", "MANAV", 4, 3_500, 620, 1, false),
            ("kasap", "commercial/building-d", "KASAP", 5, 9_000, 1_400, 1, true),
            ("firin", "commercial/building-h", "FIRIN", 5, 7_500, 1_200, 1, true),
            ("berber", "commercial/building-g", "BERBER", 6, 6_000, 1_000, 1, false),
            ("market", "commercial/building-f", "MARKET", 8, 18_000, 2_600, 1, false),
            ("kafe", "commercial/building-c", "KAFE", 8, 14_000, 2_100, 1, true),
            ("eczane", "commercial/building-a", "ECZANE", 10, 25_000, 3_400, 1, false),
            ("supermarket", "commercial/building-i", "SÜPERMARKET", 14, 60_000, 7_500, 2, false),
        };

        /// <summary>İşletme dosyaları, katalog ve ilerleme ayarları (yoksa oluşturulur; eski sürümse güncellenir).</summary>
        static (BusinessCatalog catalog, ProgressionConfig config) EnsureGameData()
        {
            string dir = DataDir + "/Isletmeler";
            EnsureFolder(dir);
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ConfigPath);
            if (config == null)
            {
                config = ProgressionConfig.CreateDefault();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<BusinessCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BusinessCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            foreach (var d in BusinessTable)
            {
                string path = $"{dir}/{d.id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<BusinessDefinition>(path);
                bool created = def == null;
                if (created) def = ScriptableObject.CreateInstance<BusinessDefinition>();
                if (created || def.dataVersion < BusinessDataVersion)
                {
                    def.id = d.id;
                    def.displayNameKey = "business." + d.id;
                    def.requiredLevel = d.level;
                    def.requiredStage = config.StageForLevel(d.level);
                    def.buildCost = d.cost;
                    def.incomePerHour = Math.Round(d.net * 1.4);
                    def.expensePerHour = Math.Round(d.net * 0.4);
                    def.footprint = new Vector2Int(d.size, d.size);
                    def.styleId = d.style;
                    def.signText = d.sign;
                    def.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StylePath(d.style));
                    def.dataVersion = BusinessDataVersion;
                    if (created) AssetDatabase.CreateAsset(def, path);
                    EditorUtility.SetDirty(def);
                }
                if (!catalog.businesses.Contains(def))
                {
                    catalog.businesses.Add(def);
                    EditorUtility.SetDirty(catalog);
                }
            }
            AssetDatabase.SaveAssets();
            return (catalog, config);
        }

        static bool BrickFor(string businessId)
        {
            foreach (var d in BusinessTable) if (d.id == businessId) return d.brick;
            return false;
        }

        static string SignFor(string businessId)
        {
            foreach (var d in BusinessTable) if (d.id == businessId) return d.sign;
            return "";
        }

        // ---------------------------------------------------------------- Zemin ve yollar

        static void BuildGround(Ctx c)
        {
            float size = 8000f;
            Quad("Zemin", c.root, Vector3.zero, new Vector2(size, size), c.grass);

            // Kilitli halkalar: soluk çayır. Açık kasaba alanı normal çim.
            var b = c.g.Bounds;
            Quad("KilitliAlan", c.root, RectCenter(c.g, b) + Vector3.up * 0.02f, RectSize(c.g, b), c.meadow);
            foreach (var stage in Stages)
            {
                var area = OpenArea(c.g, CityEconomy.MaxRingFor(stage));
                var q = Quad("KasabaZemini_" + stage, c.root, RectCenter(c.g, area) + Vector3.up * 0.04f, RectSize(c.g, area), c.grass);
                Gate(q, stage, stage);
            }
        }

        static readonly CityStage[] Stages = { CityStage.Seyyar, CityStage.Esnaf, CityStage.Sirket, CityStage.Holding, CityStage.Metropol };

        static T Gate<T>(T obj, CityStage min, CityStage max) where T : UnityEngine.Object
        {
            var go = obj is GameObject g ? g : (obj as Component).gameObject;
            var gate = go.AddComponent<StageGated>();
            gate.minStage = min;
            gate.maxStage = max;
            go.SetActive(min <= CityStage.Seyyar && max >= CityStage.Seyyar);
            return obj;
        }

        /// <summary>Bir aşamada çizilen yollar: o aşamaya kadar açılmış yollar ve şehir dışına uzanan ana caddeler.</summary>
        static HashSet<Vector2Int> RoadSet(Ctx c, CityStage stage)
        {
            var set = new HashSet<Vector2Int>();
            var b = c.g.Bounds;
            foreach (var r in c.layout.roads)
                if (r.unlockStage <= stage || r.main) set.Add(r.cell);
            int extend = Mathf.CeilToInt(CountryRadius / c.g.cellSize);
            foreach (var r in c.layout.roads)
            {
                if (!r.main) continue;
                var cell = r.cell;
                bool edgeX = cell.x == b.xMin || cell.x == b.xMax - 1;
                bool edgeY = cell.y == b.yMin || cell.y == b.yMax - 1;
                if (!(edgeX ^ edgeY)) continue;
                var dir = edgeX ? new Vector2Int(cell.x == b.xMin ? -1 : 1, 0) : new Vector2Int(0, cell.y == b.yMin ? -1 : 1);
                if (!c.g.IsRoadColumn(edgeX ? cell.y : cell.x)) continue;
                for (int i = 1; i <= extend; i++) set.Add(cell + dir * i);
            }
            return set;
        }

        /// <summary>
        /// Yollar her aşama için ayrı hesaplanır (yeni halka açılınca kavşaklar değişir).
        /// Aynı parça art arda aşamalarda aynıysa tek nesne olur; StageGated hangi aşamalarda görüneceğini söyler.
        /// </summary>
        static void BuildRoads(Ctx c)
        {
            var sets = new Dictionary<CityStage, HashSet<Vector2Int>>();
            foreach (var st in Stages) sets[st] = RoadSet(c, st);
            c.drawnRoads = sets[CityStage.Seyyar];
            c.allRoads = sets[CityStage.Metropol];

            foreach (var cell in c.allRoads)
            {
                (string model, float yaw)? prev = null;
                CityStage runStart = CityStage.Seyyar;
                for (int i = 0; i <= Stages.Length; i++)
                {
                    (string model, float yaw)? tile = null;
                    if (i < Stages.Length && sets[Stages[i]].Contains(cell))
                    {
                        var set = sets[Stages[i]];
                        int mask = 0;
                        if (set.Contains(cell + Vector2Int.up)) mask |= 1;
                        if (set.Contains(cell + Vector2Int.right)) mask |= 2;
                        if (set.Contains(cell + Vector2Int.down)) mask |= 4;
                        if (set.Contains(cell + Vector2Int.left)) mask |= 8;
                        var t = RoadTile(mask);
                        // Açık alan içinde kavşağa bitişik düz yollarda yaya geçidi.
                        if (t.model == "road-straight" && OpenArea(c.g, CityEconomy.MaxRingFor(Stages[i])).Contains(cell))
                        {
                            var along = t.yaw == 0f ? Vector2Int.right : Vector2Int.up;
                            if (IsJunction(set, cell + along) || IsJunction(set, cell - along)) t.model = "road-crossing";
                        }
                        tile = t;
                    }
                    if (prev.HasValue && (!tile.HasValue || tile.Value != prev.Value))
                    {
                        var go = Spawn(c, $"{KenneyDir}/CityRoads/{prev.Value.model}.fbx", c.roads, c.g.CellCenter(cell), prev.Value.yaw, CityScale);
                        go.name = "Yol";
                        Gate(go, runStart, Stages[i - 1]);
                    }
                    if (tile.HasValue && (!prev.HasValue || tile.Value != prev.Value)) runStart = Stages[Mathf.Min(i, Stages.Length - 1)];
                    prev = tile;
                }
                if (c.drawnRoads.Contains(cell)) c.keepClear.Add(CellRect(c.g, cell, 1f));
            }
        }

        static bool IsJunction(HashSet<Vector2Int> set, Vector2Int cell)
        {
            if (!set.Contains(cell)) return false;
            int n = 0;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                if (set.Contains(cell + d)) n++;
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
            parent.gameObject.AddComponent<PlotView>().plotId = p.id;
            var center = c.g.RectCenter(p.cells);
            bool paved = p.type == PlotType.Business || p.type == PlotType.Plaza || p.type == PlotType.Civic;
            if (paved) PaveRect(c, parent, p.cells);
            c.keepClear.Add(PlotRect(c.g, p.cells));

            switch (p.type)
            {
                case PlotType.Business:
                {
                    // Dükkanın kendisi bina: camlı vitrin cephesi, büyük tabela.
                    var f = Plot.FacingDirection(p.facing);
                    var front = new Vector3(f.x, 0, f.y);
                    float along = (f.x != 0 ? p.cells.width : p.cells.height) * c.g.cellSize;
                    float side = (f.x != 0 ? p.cells.height : p.cells.width) * c.g.cellSize;
                    var building = ShopBuildingFor(c, parent, p.occupantId, center - front * 0.5f, Quaternion.LookRotation(front), new Vector2(side - 1.2f, along - 2.6f));
                    building.name = p.occupantId;
                    break;
                }
                case PlotType.Civic:
                {
                    var building = Spawn(c, StylePath(p.styleId), parent, center, YawFor(p.facing), CityScale);
                    building.name = p.occupantId;
                    var wall = p.type == PlotType.Civic ? c.plasters[0]
                        : (p.id % 5 < 2 ? c.bricks[p.id % c.bricks.Length] : c.plasters[p.id % c.plasters.Length]);
                    SkinBuilding(c, building, "CityCommercial", wall, c.roofTiles);
                    var f = Plot.FacingDirection(p.facing);
                    ShopSign(c, building.transform, parent, new Vector3(f.x, 0, f.y), p.type == PlotType.Civic ? "BELEDİYE" : SignFor(p.occupantId));
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

        /// <summary>Arsayı gerçek dokulu taş parkeyle kaplar (kaldırım yüksekliğinde).</summary>
        static void PaveRect(Ctx c, Transform parent, RectInt r)
        {
            Box("ParkeAlti", parent, c.g.RectCenter(r) + Vector3.up * 0.075f, new Vector3(r.width * c.g.cellSize, 0.15f, r.height * c.g.cellSize), c.stone);
            Quad("Parke", parent, c.g.RectCenter(r) + Vector3.up * 0.155f, RectSize(c.g, r), c.paving);
        }

        static void BuildHouse(Ctx c, Transform parent, Plot p)
        {
            float half = p.cells.width * c.g.cellSize * 0.5f;
            var house = Spawn(c, StylePath(p.styleId), parent, PlotPoint(c, p, 0f, 0.6f), YawFor(p.facing), CityScale);
            house.name = "Ev";
            SkinBuilding(c, house, "CitySuburban", c.bricks[p.id % c.bricks.Length], c.roofTiles);

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
                var pond = center + new Vector3(3f, 0f, -2f);
                Disc("Golet", parent, pond + Vector3.up * 0.06f, new Vector2(13f, 9f), c.water).AddComponent<CityRise.Environment.WaterScroll>();
                Disc("GoletKenari", parent, pond + Vector3.up * 0.03f, new Vector2(14.6f, 10.6f), c.stone);
                // Kenarda kümeler halinde sazlık ve çiçek.
                for (int i = 0; i < 16; i++)
                {
                    float a = Rand(c, 0f, Mathf.PI * 2f);
                    if (Mathf.Sin(a * 3f) < -0.2f) continue;
                    var edge = pond + new Vector3(Mathf.Cos(a) * 7.6f, 0, Mathf.Sin(a) * 5.6f);
                    SpawnPrefab(c.trees.reeds[c.rng.Next(c.trees.reeds.Count)], parent, edge, Rand(c, 0, 360), Rand(c, 0.8f, 1.2f));
                }
                for (int i = 0; i < 10; i++)
                {
                    float a = Rand(c, 0f, Mathf.PI * 2f);
                    Flower(c, parent, pond + new Vector3(Mathf.Cos(a) * 8.3f, 0, Mathf.Sin(a) * 6.3f));
                }
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
            Cylinder("CesmeSuyu", parent, center + Vector3.up * 0.58f, new Vector3(7.2f, 0.04f, 7.2f), c.water).AddComponent<CityRise.Environment.WaterScroll>();
            Cylinder("CesmeOrtasi", parent, center + Vector3.up * 1.3f, new Vector3(0.9f, 1.3f, 0.9f), c.stone);
            Cylinder("CesmeCanagi", parent, center + Vector3.up * 2.5f, new Vector3(3f, 0.15f, 3f), c.stone);
            Cylinder("CesmeCanagiSu", parent, center + Vector3.up * 2.64f, new Vector3(2.6f, 0.02f, 2.6f), c.water);
            Cylinder("CesmeTepesi", parent, center + Vector3.up * 3.1f, new Vector3(0.45f, 0.5f, 0.45f), c.stone);
            FountainWater(c, parent, center);

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
                // Bahçenin meydana bakan iki kenarında tek renkli çiçek tarhları.
                int col = (int)((sx + 1) + (sz + 1) / 2);
                FlowerBed(c, parent, garden + new Vector3(0, 0.24f, -sz * (gs * 0.5f - 0.8f)), new Vector2(gs - 1f, 1.2f), Quaternion.identity, col);
                FlowerBed(c, parent, garden + new Vector3(-sx * (gs * 0.5f - 0.8f), 0.24f, 0), new Vector2(1.2f, gs - 3f), Quaternion.identity, col + 2);
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
                if (c.night || c.dynamic) PointLight(c, head + Vector3.down * 0.4f, 15f, 12f, new Color(1f, 0.68f, 0.36f));
            }

            for (int i = 0; i < 3; i++)
            {
                var pos = center + new Vector3(-6f + i * 6f, 0.15f, h - 2.2f);
                Spawn(c, $"{KenneyDir}/CityCommercial/detail-parasol-{(i % 2 == 0 ? "a" : "b")}.fbx", parent, pos, 0f, CityScale);
            }
        }

        /// <summary>SATILIK tabelası şablonu (oyun sırasında yeni açılan arsalarda da kullanılır).</summary>
        static GameObject MakeSignTemplate(Ctx c)
        {
            var sign = new GameObject("SatilikTabela");
            foreach (var x in new[] { -0.9f, 0.9f })
                Box("Direk", sign.transform, new Vector3(x, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.12f), c.signPost);
            Box("Pano", sign.transform, new Vector3(0, 1.75f, 0.03f), new Vector3(2.4f, 1.1f, 0.06f), c.signPost);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Yazi";
            face.transform.SetParent(sign.transform, false);
            face.transform.localPosition = new Vector3(0, 1.75f, -0.005f);
            face.transform.localScale = new Vector3(2.3f, 1.0f, 1f);
            face.GetComponent<Renderer>().sharedMaterial = c.signBoard;
            sign.SetActive(false);
            return sign;
        }

        /// <summary>Satılık arsa: tabela, çiçek ve çimler "Susler" altında (bina kurulunca kaldırılır), kaldırım ağaçları kalır.</summary>
        static void ForSaleSign(Ctx c, Transform parent, Plot p)
        {
            if (c.signTemplate == null) c.signTemplate = MakeSignTemplate(c);
            var decor = Group(parent, "Susler");
            float half = Mathf.Min(p.cells.width, p.cells.height) * c.g.cellSize * 0.5f;
            var sign = Object.Instantiate(c.signTemplate, decor);
            sign.name = "SatilikTabela";
            sign.SetActive(true);
            sign.transform.SetPositionAndRotation(PlotPoint(c, p, 0f, half - 1.6f), Quaternion.Euler(0, Plot.FacingYaw(p.facing), 0));
            foreach (var t in sign.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            if (p.Area > 1)
            {
                for (int i = 0; i < 6; i++)
                    Flower(c, decor, c.g.RectCenter(p.cells) + new Vector3(Rand(c, -half + 2, half - 2), 0, Rand(c, -half + 2, half - 2)));
                StreetTrees(c, parent, p, 8f);
                for (int i = 0; i < 4; i++)
                    GrassTuft(c, decor, c.g.RectCenter(p.cells) + new Vector3(Rand(c, -half + 2, half - 2), 0, Rand(c, -half + 2, half - 2)));
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
        /// <summary>
        /// Açık alanı kilitli halkalardan ayıran alçak çit (ana caddelerde açıklık bırakılır).
        /// Her aşama için ayrı çit; o aşamada açık alanın sınırında durur.
        /// </summary>
        static void BuildBoundaryFence(Ctx c)
        {
            var root = Group(c.root, "SinirCiti");
            float cs = c.g.cellSize;
            float step = 0.48f * CityScale;
            foreach (var stage in Stages)
            {
                int ring = CityEconomy.MaxRingFor(stage);
                if (ring >= c.g.radiusBlocks) continue;
                var parent = Group(root, "Cit_" + stage);
                Gate(parent.gameObject, stage, stage);
                var r = OpenArea(c.g, ring);
                var roads = RoadSet(c, stage);
                float minX = r.xMin * cs - 1.2f, maxX = r.xMax * cs + 1.2f;
                float minZ = r.yMin * cs - 1.2f, maxZ = r.yMax * cs + 1.2f;
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
                            if (!r.Contains(n2) && roads.Contains(n2)) nearRoad = true;
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
        }

        // ---------------------------------------------------------------- Sokak lambaları

        /// <summary>
        /// Lamba direği yol ortasından bu uzaklıkta. Kaldırım 3,2 ile 4 m arasında; direk kaldırımın yol tarafında,
        /// yayaların yürüdüğü çizgi (3,68 m) binaya yakın tarafta.
        /// </summary>
        const float LampOffset = 3.33f;

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
                Vector3 offset = alongX ? new Vector3(0, 0, side * LampOffset) : new Vector3(side * LampOffset, 0, 0);
                float yaw = alongX ? (side > 0 ? 0f : 180f) : (side > 0 ? 90f : 270f);
                var lamp = Spawn(c, $"{KenneyDir}/CityRoads/light-square.fbx", parent, center + offset, yaw, CityScale);
                lamp.name = "Lamba";
                var head = center + offset + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 4.55f, -1.55f);
                Box("LambaCami", parent, head, new Vector3(0.5f, 0.08f, 0.35f), c.lampGlow).isStatic = true;
                if ((c.night || c.dynamic) && c.lightCount < 48)
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

        /// <summary>Gerçek fotoğraflı çiçek öbeği; color -1 ise rastgele renk.</summary>
        static void Flower(Ctx c, Transform parent, Vector3 pos, int color = -1)
        {
            var list = c.trees.flowers;
            var prefab = list[color >= 0 ? color % list.Count : c.rng.Next(list.Count)];
            SpawnPrefab(prefab, parent, pos, Rand(c, 0, 360), Rand(c, 0.85f, 1.25f));
        }

        /// <summary>Tek renkli, sık dikilmiş çiçek tarhı.</summary>
        static void FlowerBed(Ctx c, Transform parent, Vector3 center, Vector2 size, Quaternion rot, int color)
        {
            for (float x = -size.x * 0.5f + 0.35f; x < size.x * 0.5f - 0.2f; x += 0.55f)
            for (float z = -size.y * 0.5f + 0.35f; z < size.y * 0.5f - 0.2f; z += 0.55f)
                Flower(c, parent, center + rot * new Vector3(x + Rand(c, -0.1f, 0.1f), 0, z + Rand(c, -0.1f, 0.1f)), color);
        }

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
            c.wilderness = parent;
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
            bool corn = crop.Contains("corn");
            var crops = new GameObject("Ekinler");
            crops.transform.SetParent(field);
            crops.transform.position = center + Vector3.up * 0.05f;
            crops.AddComponent<MeshFilter>().sharedMesh = TreeFactory.CropFieldMesh(size, rowsAlongZ, corn, c.rng.Next());
            crops.AddComponent<MeshRenderer>().sharedMaterials = new[] { corn ? c.cropCorn : c.cropWheat, corn ? c.cropCorn : c.cropWheat, corn ? c.cropCorn : c.cropWheat };
            crops.isStatic = true;
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
                if (!c.allRoads.Contains(n)) continue;
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

            if (night || c.dynamic)
                PointLight(c, new Vector3(0, 4f, 0), 16f, 10f, new Color(1f, 0.82f, 0.55f));
        }

        /// <summary>
        /// Gün-gece döngüsü için gece parçaları: ay ışığı, gece gökyüzü ve renk ayarı, parlayan pencere/cam/lamba
        /// malzemeleri ve sokak lambası ışıkları (gündüz kapalı). Oyunda DayNightCycle bunlar arasında geçiş yapar.
        /// </summary>
        static void BuildDayNight(Ctx c)
        {
            var cycle = new GameObject("GunDongusu").AddComponent<CityRise.Life.DayNightCycle>();
            cycle.sun = RenderSettings.sun;
            var moon = new GameObject("Ay").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.6f;
            moon.transform.rotation = Quaternion.Euler(50f, -60f, 0);
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.intensity = 0.35f;
            moon.enabled = false;
            cycle.moon = moon;
            foreach (var l in c.lights.GetComponentsInChildren<Light>(true))
            {
                cycle.nightLights.Add(l);
                l.enabled = false;
            }

            void Pair(Material day, Texture glowMap, Color glow)
            {
                if (day == null) return;
                var n = new Material(day) { name = day.name + "_Gece" };
                SetEmission(c, n, glowMap, glow);
                cycle.dayMaterials.Add(day);
                cycle.nightMaterials.Add(n);
            }
            foreach (var kit in BuildingKits)
                if (c.kenneyMats.TryGetValue(kit, out var day))
                    Pair(day, WindowGlowMap(kit, day.GetTexture("_BaseMap") as Texture2D), new Color(1f, 0.66f, 0.32f) * 1.1f);
            Pair(c.glass, null, new Color(1f, 0.68f, 0.35f) * 1.1f);
            Pair(c.lampGlow, null, new Color(1f, 0.78f, 0.45f) * 6f);
            // Dükkan vitrinlerinin arkası gece aydınlık görünsün.
            Pair(shopInWall, null, new Color(1f, 0.85f, 0.6f) * 0.9f);
            Pair(shopInFloor, null, new Color(1f, 0.85f, 0.6f) * 0.35f);

            cycle.daySky = RenderSettings.skybox;
            cycle.dayVolume = Object.FindFirstObjectByType<Volume>();
            PrepareHdri(SkyPath(true));
            c.night = true;
            cycle.nightSky = SkyboxMat(c, SkyPath(true));
            BuildPostProcessing(c);
            c.night = false;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v != cycle.dayVolume)
                {
                    v.name = "RenkAyarlari_Gece";
                    v.weight = 0f;
                    v.priority = 1f;
                    cycle.nightVolume = v;
                }
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
            c.cameraCtrl = ctrl;
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
            // Oyundaki başlangıç görüntüsü sahneye de yazılır; yoksa Play'e basmadan Game sekmesi yerin altından bakar.
            ctrl.ApplyTransform();
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
            var waterNor = WaterNormalMap();
            NeutralRoadPalette();
            PrepareHdri(SkyPath(c.night));
            if (c.night)
                foreach (var kit in BuildingKits)
                    WindowGlowMap(kit, AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenneyDir}/{kit}/Textures/colormap.png"));

            c.grass = TexturedMat("Cim", grassTex, grassNor, new Color(0.62f, 0.90f, 0.46f), 4f);
            c.meadow = TexturedMat("Cayir", grassTex, grassNor, new Color(0.68f, 0.88f, 0.46f), 4f);
            c.water = SimpleMat("Su", new Color(0.05f, 0.19f, 0.23f), 0.94f, m =>
            {
                m.SetTexture("_BumpMap", waterNor);
                m.SetFloat("_BumpScale", 0.45f);
                m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BaseMap", new Vector2(2.5f, 2.5f));
                m.SetTexture("_DetailNormalMap", waterNor);
                m.SetFloat("_DetailNormalMapScale", 0.35f);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(7f, 7f));
                m.EnableKeyword("_DETAIL_MULX2");
                m.SetFloat("_EnvironmentReflections", 1f);
                m.SetFloat("_SpecularHighlights", 1f);
            });
            var stoneTex = LoadTexture($"{PolyHavenDir}/Textures/large_sandstone_blocks_diff_1k.jpg", false);
            var stoneNor = LoadTexture($"{PolyHavenDir}/Textures/large_sandstone_blocks_nor_gl_1k.jpg", true);
            c.stone = TexturedMat("Tas", stoneTex, stoneNor, new Color(1.25f, 1.2f, 1.1f), 1.5f);

            // Bina yüzeyleri: üç tuğla tonu, üç sıva rengi, kiremit ve cam.
            var brickTex = LoadTexture($"{PolyHavenDir}/Textures/red_brick_03_diff_2k.jpg", false);
            var brickNor = LoadTexture($"{PolyHavenDir}/Textures/red_brick_03_nor_gl_2k.jpg", true);
            var plasterTex = LoadTexture($"{PolyHavenDir}/Textures/painted_plaster_wall_diff_2k.jpg", false);
            var plasterNor = LoadTexture($"{PolyHavenDir}/Textures/painted_plaster_wall_nor_gl_2k.jpg", true);
            var roofTex = LoadTexture($"{PolyHavenDir}/Textures/clay_roof_tiles_02_diff_1k.jpg", false);
            var roofNor = LoadTexture($"{PolyHavenDir}/Textures/clay_roof_tiles_02_nor_gl_1k.jpg", true);
            c.bricks = new[]
            {
                TexturedMat("Tugla_Kirmizi", brickTex, brickNor, new Color(1f, 0.97f, 0.95f), 1.3f),
                TexturedMat("Tugla_Koyu", brickTex, brickNor, new Color(0.78f, 0.66f, 0.6f), 1.3f),
                TexturedMat("Tugla_Acik", brickTex, brickNor, new Color(1f, 0.9f, 0.78f), 1.3f),
            };
            c.plasters = new[]
            {
                TexturedMat("Siva_Krem", plasterTex, plasterNor, new Color(1f, 0.94f, 0.82f), 2f),
                TexturedMat("Siva_Seftali", plasterTex, plasterNor, new Color(1f, 0.85f, 0.73f), 2f),
                TexturedMat("Siva_Acik", plasterTex, plasterNor, new Color(0.96f, 0.96f, 0.94f), 2f),
            };
            c.roofTiles = TexturedMat("Kiremit", roofTex, roofNor, new Color(0.8f, 0.66f, 0.6f), 2.5f);
            var pavingTex = LoadTexture($"{PolyHavenDir}/Textures/hexagonal_concrete_paving_diff_2k.jpg", false);
            var pavingNor = LoadTexture($"{PolyHavenDir}/Textures/hexagonal_concrete_paving_nor_gl_2k.jpg", true);
            c.paving = TexturedMat("Parke", pavingTex, pavingNor, new Color(0.95f, 0.93f, 0.9f), 1.6f);
            c.glass = SimpleMat(c.night ? "Cam_Gece" : "Cam", new Color(0.2f, 0.27f, 0.32f), 0.95f, m =>
            {
                m.SetFloat("_Metallic", 0.1f);
                if (c.night) SetEmission(c, m, null, new Color(1f, 0.68f, 0.35f) * 1.1f);
            }, persist: !c.night);
            c.cropWheat = TreeFactory.CropMaterial(false);
            c.cropCorn = TreeFactory.CropMaterial(true);
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

        /// <summary>
        /// Döşenebilir (kenarları birleşen) su dalgası normal haritası: tam sayı frekanslı sinüs dalgalarının toplamı.
        /// </summary>
        static Texture2D WaterNormalMap()
        {
            string path = $"{GeneratedDir}/SuDalgasi.png";
            if (!File.Exists(FullPath(path)))
            {
                const int size = 512;
                var rng = new Random(31);
                var waves = new List<(int kx, int ky, float amp, float phase)>();
                for (int i = 0; i < 18; i++)
                {
                    int kx = rng.Next(-9, 10), ky = rng.Next(-9, 10);
                    if (kx == 0 && ky == 0) continue;
                    float k = Mathf.Sqrt(kx * kx + ky * ky);
                    waves.Add((kx, ky, 1f / (k * k * 0.6f + 1f), (float)rng.NextDouble() * 6.283f));
                }
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float dx = 0, dy = 0;
                    foreach (var w in waves)
                    {
                        float ph = 2f * Mathf.PI * (w.kx * u + w.ky * v) + w.phase;
                        float c = Mathf.Cos(ph) * w.amp * 2f * Mathf.PI;
                        dx += c * w.kx;
                        dy += c * w.ky;
                    }
                    var n = new Vector3(-dx * 0.02f, -dy * 0.02f, 1f).normalized;
                    px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Çeşmeden fışkıran ve üst çanaktan dökülen su (parçacık efektleri, oyun çalışınca oynar).</summary>
        static void FountainWater(Ctx c, Transform parent, Vector3 center)
        {
            var mat = ParticleMaterial();

            ParticleSystem Make(string name, Vector3 pos)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent);
                go.transform.position = pos;
                var ps = go.AddComponent<ParticleSystem>();
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return ps;
            }

            // Ortadan yukarı fışkıran su.
            var jet = Make("CesmeFiskiye", center + Vector3.up * 3.35f);
            jet.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var main = jet.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new Color(0.86f, 0.93f, 1f, 0.55f);
            main.gravityModifier = 1f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = jet.emission;
            em.rateOverTime = 220f;
            var sh = jet.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 7f;
            sh.radius = 0.05f;

            // Üst çanağın kenarından dökülen su perdesi.
            var fall = Make("CesmeDokulen", center + Vector3.up * 2.55f);
            fall.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var fm = fall.main;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.6f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            fm.startColor = new Color(0.86f, 0.93f, 1f, 0.45f);
            fm.gravityModifier = 1f;
            fm.maxParticles = 800;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            var fe = fall.emission;
            fe.rateOverTime = 320f;
            var fs = fall.shape;
            fs.shapeType = ParticleSystemShapeType.Circle;
            fs.radius = 1.45f;
            fs.radiusThickness = 0f;
        }

        static Material ParticleMaterial()
        {
            string texPath = $"{GeneratedDir}/SuDamlasi.png";
            if (!File.Exists(FullPath(texPath)))
            {
                const int size = 64;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    byte a = (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(FullPath(texPath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string path = $"{GeneratedDir}/SuParcacigi.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "SuParcacigi" };
            AssetDatabase.CreateAsset(mat, path);
            mat.SetTexture("_BaseMap", dot);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
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

        // ---------------------------------------------------------------- Oyun bağlantısı

        /// <summary>
        /// Satın alınabilir her işletme için sahnede gizli bir bina şablonu (zemin + bina + tabela) ve
        /// SATILIK tabelası şablonu kurar; oyun yöneticisini bunlara bağlar.
        /// </summary>
        static void BuildGame(Ctx c, CityTown town)
        {
            var templatesRoot = Group(c.root, "Sablonlar");
            var gm = new GameObject("Oyun").AddComponent<GameManager>();
            gm.layoutAsset = c.layout;
            gm.config = c.config;
            gm.catalog = c.catalog;
            gm.town = town;
            gm.cameraController = c.cameraCtrl;
            gm.plotsRoot = c.plots;
            gm.wildernessRoot = c.wilderness;
            if (c.signTemplate == null) c.signTemplate = MakeSignTemplate(c);
            gm.forSaleSignTemplate = c.signTemplate;
            c.signTemplate.transform.SetParent(templatesRoot, true);

            foreach (var def in c.catalog.Sorted())
            {
                var root = new GameObject("Sablon_" + def.id);
                root.transform.SetParent(templatesRoot, false);

                // Zemin 1×1 m; oyun sırasında arsanın boyutuna ölçeklenir.
                var ground = Group(root.transform, "Zemin");
                Box("ParkeAlti", ground, new Vector3(0, 0.075f, 0), new Vector3(1f, 0.15f, 1f), c.stone);
                var pave = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(pave.GetComponent<Collider>());
                pave.name = "Parke";
                pave.transform.SetParent(ground, false);
                pave.transform.localPosition = new Vector3(0, 0.155f, 0);
                pave.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                pave.GetComponent<Renderer>().sharedMaterial = c.paving;

                // Bina önü +Z'ye baksın (oyunda arsanın yönüne çevrilir). Dükkanın kendisi bina.
                float cs = c.g.cellSize;
                var building = ShopBuildingFor(c, root.transform, def.id, new Vector3(0, 0, -0.5f), Quaternion.identity,
                    new Vector2(def.footprint.x * cs - 1.2f, def.footprint.y * cs - 2.6f));
                building.name = "Bina";
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                root.SetActive(false);
                gm.templates.Add(new GameManager.BusinessTemplate { businessId = def.id, template = root });
            }
            templatesRoot.gameObject.SetActive(true);
            gm.interiorLibrary = BuildInteriorLibrary(c, gm.gameObject);
        }

        /// <summary>
        /// Asset Store'dan eklenen ücretsiz gerçekçi arabalar: model yolu, hedef uzunluk (m) ve
        /// önünü +Z'ye çeviren açı. Paket projede yoksa atlanır.
        /// </summary>
        static readonly (string key, string path, float length, float yaw)[] RealCars =
        {
            ("skyline", "Assets/Realistic Car Controller Pro/Models/Prototype Vehicle/Skyline (Prototype)/Model_Skyline by BUMSTRUM(3DMaesen) (Prototype).fbx", 4.5f, 0f),
            ("pickup", "Assets/Pack_Pickup/Prefabs/Pickup.prefab", 5.2f, 0f),
            ("supermini", "Assets/SuperminiCarURP/SuperminiCar URP by WITSGaming/SuperminiCarURP/SuperminiCar.fbx", 3.9f, 0f),
        };

        /// <summary>
        /// Arabayı oyuna uygun prefab yapar: ölçek (uzunluk), yön, zemine oturtma, fizik ve betikleri ayıklama,
        /// URP dışı malzemeleri URP Lit'e çevirme.
        /// </summary>
        static GameObject RealCarPrefab(string key, string path, float length, float yaw)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) return null;
            string dir = GeneratedDir + "/Arabalar";
            EnsureFolder(dir);
            string prefabPath = $"{dir}/{key}_v2.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Araba_" + key);
            var model = Object.Instantiate(src, root.transform);
            model.name = "Model";
            // Yalnızca görünüm: çarpıştırıcı, rigidbody ve paket betikleri kaldırılır.
            foreach (var c in model.GetComponentsInChildren<Component>(true))
                if (c is Collider || c is Rigidbody || c is MonoBehaviour || c is AudioSource || c is Light || c is ParticleSystem)
                    Object.DestroyImmediate(c);
            model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = CarBounds(root);
            float s = length / Mathf.Max(0.01f, b.size.z);
            model.transform.localScale *= s;
            b = CarBounds(root);
            model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (m == null || m.shader.name.StartsWith("Universal Render Pipeline")) continue;
                    ms[i] = UrpCopy(m, dir);
                }
                r.sharedMaterials = ms;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Bounds CarBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Paketin kendi (URP dışı) malzemesinden renk ve dokuyu alıp URP Lit malzeme üretir.</summary>
        static Material UrpCopy(Material m, string dir)
        {
            string path = $"{dir}/{string.Join("_", m.name.Split(Path.GetInvalidFileNameChars()))}_URP.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var u = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = m.name + "_URP" };
            AssetDatabase.CreateAsset(u, path);
            var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null);
            if (tex != null) u.SetTexture("_BaseMap", tex);
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
            u.SetColor("_BaseColor", col);
            if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null) { u.SetTexture("_BumpMap", m.GetTexture("_BumpMap")); u.EnableKeyword("_NORMALMAP"); }
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.6f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0.2f;
            // Araba boyası: parlak, biraz metalik.
            if (m.shader.name.Contains("Body")) { gloss = Mathf.Max(gloss, 0.8f); metal = Mathf.Max(metal, 0.4f); }
            u.SetFloat("_Smoothness", gloss);
            u.SetFloat("_Metallic", metal);
            if (col.a < 0.95f || m.name.ToLower().Contains("glass"))
            {
                u.SetFloat("_Surface", 1f);
                u.SetFloat("_Blend", 0f);
                u.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                u.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                u.SetFloat("_ZWrite", 0f);
                u.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                u.renderQueue = (int)RenderQueue.Transparent;
                u.SetOverrideTag("RenderType", "Transparent");
                u.SetColor("_BaseColor", new Color(col.r * 0.3f, col.g * 0.35f, col.b * 0.4f, Mathf.Min(col.a, 0.5f)));
            }
            EditorUtility.SetDirty(u);
            AssetDatabase.SaveAssets();
            return u;
        }

        /// <summary>İç görünüm modelleri (Kenney CC0) ve malzemeleri (Poly Haven CC0).</summary>
        static CityRise.Interior.InteriorLibrary BuildInteriorLibrary(Ctx c, GameObject host)
        {
            var lib = host.AddComponent<CityRise.Interior.InteriorLibrary>();
            void AddFolder(string folder, string prefix, float scale, Func<string, string> rename = null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{KenneyDir}/{folder}" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    lib.models.Add(new CityRise.Interior.InteriorLibrary.Entry
                    {
                        key = prefix + (rename != null ? rename(name) : name),
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        scale = scale,
                    });
                }
            }
            AddFolder("MiniMarket", "mm/", 2.0f, n => n == "character-employee" ? "employee" : n);
            AddFolder("Characters", "ch/", 2.2f, n => n.Replace("character-", ""));
            AddFolder("Food", "food/", 0.45f);
            AddFolder("Furniture", "furn/", 0.22f);
            AddFolder("Cars", "car/", 1.15f);
            foreach (var (key, path, length, yaw) in RealCars)
            {
                var car = RealCarPrefab(key, path, length, yaw);
                if (car != null) lib.models.Add(new CityRise.Interior.InteriorLibrary.Entry { key = "rcar/" + key, prefab = car, scale = 1f });
            }
            foreach (var kv in PolyHavenModels.Ensure())
                lib.models.Add(new CityRise.Interior.InteriorLibrary.Entry { key = "ph/" + kv.Key, prefab = kv.Value, scale = 1f });
            // Gerçekçi insanlar (Mixamo): prefablar zaten 1,72 m boyunda ve Animator'lı.
            foreach (var p in MixamoSetup.Characters())
                lib.models.Add(new CityRise.Interior.InteriorLibrary.Entry { key = "mx/" + p.name.Replace("Insan_", ""), prefab = p, scale = 1f });

            var tileTex = LoadTexture($"{PolyHavenDir}/Textures/floor_tiles_06_diff_1k.jpg", false);
            var tileNor = LoadTexture($"{PolyHavenDir}/Textures/floor_tiles_06_nor_gl_1k.jpg", true);
            var soilTex = LoadTexture($"{PolyHavenDir}/Textures/excavated_soil_wall_diff_1k.jpg", false);
            var soilNor = LoadTexture($"{PolyHavenDir}/Textures/excavated_soil_wall_nor_gl_1k.jpg", true);
            var plasterTex = LoadTexture($"{PolyHavenDir}/Textures/painted_plaster_wall_diff_2k.jpg", false);
            var plasterNor = LoadTexture($"{PolyHavenDir}/Textures/painted_plaster_wall_nor_gl_2k.jpg", true);
            // Karo dokusunda 4×4 karo var: 2,4 m'lik döşeme = 60 cm karo.
            lib.floorTiles = TexturedMat("Ic_Karo", tileTex, tileNor, new Color(1f, 0.98f, 0.95f), 2.4f);
            var parquetTex = LoadTexture($"{PolyHavenDir}/Textures/herringbone_parquet_diff_1k.jpg", false);
            if (parquetTex != null) lib.floorParquet = TexturedMat("Ic_Parke", parquetTex, LoadTexture($"{PolyHavenDir}/Textures/herringbone_parquet_nor_gl_1k.jpg", true), Color.white, 2f);
            var plankTex = LoadTexture($"{PolyHavenDir}/Textures/plank_flooring_02_diff_1k.jpg", false);
            if (plankTex != null) lib.floorPlanks = TexturedMat("Ic_Tahta", plankTex, LoadTexture($"{PolyHavenDir}/Textures/plank_flooring_02_nor_gl_1k.jpg", true), new Color(0.78f, 0.74f, 0.72f), 2.5f);
            var marbleTex = LoadTexture($"{PolyHavenDir}/Textures/marble_01_diff_1k.jpg", false);
            if (marbleTex != null) lib.floorMarble = TexturedMat("Ic_Mermer", marbleTex, LoadTexture($"{PolyHavenDir}/Textures/marble_01_nor_gl_1k.jpg", true), new Color(1.1f, 1.08f, 1.05f), 3f);
            // Market reyon tabelaları (yazı dokuya çekilir; dükkan tabelalarıyla aynı görünüm).
            lib.signTexts.Clear();
            lib.signMaterials.Clear();
            foreach (var text in CityRise.Interior.InteriorBuilder.AisleNames)
            {
                lib.signTexts.Add(text);
                lib.signMaterials.Add(SignMaterial(c, text, 1.7f / 0.36f));
            }
            // Duvar tabloları: Poly Haven (CC0) gökyüzü görüntülerinin manzara kısmı.
            lib.pictures.Clear();
            if (AssetDatabase.IsValidFolder($"{PolyHavenDir}/Tablolar"))
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{PolyHavenDir}/Tablolar" }))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    lib.pictures.Add(SimpleMat("Ic_Tablo_" + tex.name, Color.white, 0.25f, m => m.SetTexture("_BaseMap", tex)));
                }
            lib.wallInside = TexturedMat("Ic_Duvar", plasterTex, plasterNor, new Color(1f, 0.95f, 0.86f), 2f);
            lib.wallBrick = c.bricks[0];
            lib.wallCap = SimpleMat("Ic_KesitCizgisi", new Color(0.08f, 0.08f, 0.1f), 0.1f);
            lib.concrete = TexturedMat("Ic_Temel", plasterTex, plasterNor, new Color(0.62f, 0.62f, 0.63f), 2f);
            lib.soil = TexturedMat("Ic_Toprak", soilTex, soilNor, new Color(0.9f, 0.85f, 0.8f), 2f);
            lib.glass = SimpleMat("Ic_Pencere", new Color(0.62f, 0.78f, 0.92f), 0.95f);
            return lib;
        }

        static Material shopGlass, shopTrim, shopSignFrame, shopInFloor, shopInWall, shopWood;

        /// <summary>İşletmenin dükkan binası (ShopBuilding): duvar rengi işletmeye göre, tabela yazısı tabela malzemesiyle.</summary>
        static GameObject ShopBuildingFor(Ctx c, Transform parent, string id, Vector3 localPos, Quaternion localRot, Vector2 size)
        {
            if (shopGlass == null)
            {
                shopGlass = SimpleMat("VitrinCami", new Color(0.78f, 0.88f, 0.95f, 0.22f), 0.96f, m =>
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                    m.SetOverrideTag("RenderType", "Transparent");
                });
                shopTrim = SimpleMat("DukkanCerceve", new Color(0.16f, 0.17f, 0.19f), 0.45f);
                shopSignFrame = SimpleMat("DukkanTabelaBandi", new Color(0.1f, 0.16f, 0.26f), 0.35f);
                shopInFloor = SimpleMat("DukkanIcZemin", new Color(0.82f, 0.78f, 0.72f), 0.4f);
                shopInWall = SimpleMat("DukkanIcDuvar", new Color(1f, 0.95f, 0.85f), 0.1f);
                shopWood = SimpleMat("DukkanAhsap", new Color(0.5f, 0.33f, 0.2f), 0.3f);
            }
            int hash = Mathf.Abs(id.GetHashCode());
            var wall = BrickFor(id) ? c.bricks[hash % c.bricks.Length] : c.plasters[hash % c.plasters.Length];
            string text = SignFor(id);
            float sw = Mathf.Min(size.x - 1.2f, 7.5f);
            var mats = new ShopBuilding.Materials
            {
                wall = wall, trim = shopTrim, stone = c.stone, glass = shopGlass, glow = c.lampGlow,
                signFrame = shopSignFrame, sign = SignMaterial(c, text, sw / 0.95f),
                interiorFloor = shopInFloor, interiorWall = shopInWall, wood = shopWood, window = c.glass,
            };
            bool big = id == "market" || id == "supermarket";
            var go = ShopBuilding.Build(parent, "Bina", parent.TransformPoint(localPos), parent.rotation * localRot, size, big ? 6.2f : 5.4f, id, mats);
            return go;
        }

        /// <summary>Binanın ön yüzüne, kapının üstüne isim tabelası.</summary>
        static void ShopSign(Ctx c, Transform building, Transform parent, Vector3 front, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var bounds = new Bounds(building.position, Vector3.zero);
            foreach (var r in building.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float depth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(front.x), 0, Mathf.Abs(front.z))));
            var center = new Vector3(bounds.center.x, 0, bounds.center.z) + front * (depth + 0.12f) + Vector3.up * 3.3f;
            var sign = new GameObject("Tabela").transform;
            sign.SetParent(parent, false);
            sign.position = center;
            sign.rotation = Quaternion.LookRotation(-front);
            float width = Mathf.Clamp(text.Length * 0.42f + 0.8f, 2.4f, 5.2f);
            Box("Cerceve", sign, new Vector3(0, 0, 0.04f), new Vector3(width + 0.2f, 1.05f, 0.08f), c.signPost);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Yazi";
            face.transform.SetParent(sign, false);
            face.transform.localPosition = new Vector3(0, 0, -0.005f);
            face.transform.localScale = new Vector3(width, 0.85f, 1f);
            face.GetComponent<Renderer>().sharedMaterial = SignMaterial(c, text, width / 0.85f);
        }

        /// <summary>Tabela malzemesi: yazı kamerayla dokuya çekilir (yazı tipi her yerde aynı görünür).</summary>
        static Material SignMaterial(Ctx c, string text, float aspect)
        {
            if (c.signMats.TryGetValue(text, out var cached)) return cached;
            string key = Mathf.Abs(text.GetHashCode()).ToString();
            string path = $"{GeneratedDir}/Tabelalar/Tabela_{key}.png";
            if (!File.Exists(FullPath(path)))
            {
                EnsureFolder($"{GeneratedDir}/Tabelalar");
                int h = 128, w = Mathf.Clamp(Mathf.RoundToInt(h * aspect / 4f) * 4, 256, 1024);
                var bg = new Color(0.12f, 0.2f, 0.32f);
                var px = RenderText(text, w, h, bg, new Color(1f, 0.93f, 0.75f));
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var mat = SimpleMat("Tabela_" + key, Color.white, 0.3f, m => m.SetTexture("_BaseMap", texture));
            c.signMats[text] = mat;
            return mat;
        }

        static Color[] RenderText(string text, int w, int h, Color bg, Color fg)
        {
            var far = new Vector3(-9000f, 0f, -9000f);
            var go = new GameObject("TabelaYazisi");
            go.transform.position = far;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 120;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = fg;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            var b = mr.bounds;

            var camGo = new GameObject("TabelaKamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = w / (float)h;
            cam.orthographicSize = Mathf.Max(b.extents.y * 1.35f, b.extents.x * 1.12f / cam.aspect);
            cam.transform.position = b.center - Vector3.forward * 10f;
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 50f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            rt.Release();
            var px = read.GetPixels();
            Object.DestroyImmediate(read);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(go);

            // Kenarda ince çerçeve.
            var frame = new Color(0.85f, 0.72f, 0.35f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x < 5 || y < 5 || x >= w - 5 || y >= h - 5) px[y * w + x] = frame;
            return px;
        }

        // ---------------------------------------------------------------- Bina yüzeyleri

        /// <summary>Üretilen bina ağlarının sürümü; sınıflandırma değişirse artır.</summary>
        const string SkinVersion = "v1";

        /// <summary>
        /// Kenney binasının şeklini değiştirmeden yüzeylerini gerçek dokulara geçirir.
        /// Paletteki renge göre üçgenler ayrılır: beyaz/bej duvarlar tuğla ya da sıva, yeşil çatılar
        /// kiremit, açık mavi pencereler cam olur; geri kalan (çerçeve, tente, kapı) palet renginde kalır.
        /// Duvar ve çatı üçgenlerine metre cinsinden kutu izdüşümü UV verilir, böylece doku gerçek ölçekte görünür.
        /// </summary>
        static void SkinBuilding(Ctx c, GameObject go, string kit, Material wall, Material roof)
        {
            var root = go.transform;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                if (src == null || r == null) continue;
                var toMeters = Matrix4x4.Scale(Vector3.one * CityScale) * root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                string key = $"{kit}_{src.name}_{SkinVersion}";
                if (!c.skinned.TryGetValue(key, out var mesh))
                {
                    string path = $"{GeneratedDir}/Binalar/{key}.asset";
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        EnsureFolder($"{GeneratedDir}/Binalar");
                        mesh = Skin(c, src, toMeters, kit);
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    c.skinned[key] = mesh;
                }
                var palette = r.sharedMaterials.Length > 0 ? r.sharedMaterials[0] : KenneyMat(c, kit, null);
                mf.sharedMesh = mesh;
                r.sharedMaterials = new[] { palette, wall, roof, c.glass };
            }
        }

        static Mesh Skin(Ctx c, Mesh src, Matrix4x4 toMeters, string kit)
        {
            var (pal, pw, ph) = Palette(c, kit);
            var verts = src.vertices;
            var normals = src.normals;
            var uvs = src.uv;
            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nuv = new List<Vector2>();
            var tris = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                var t = src.GetTriangles(sub);
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = t[i], b = t[i + 1], d = t[i + 2];
                    var uc = (uvs[a] + uvs[b] + uvs[d]) / 3f;
                    int px = Mathf.Clamp((int)(Mathf.Repeat(uc.x, 1f) * pw), 0, pw - 1);
                    int py = Mathf.Clamp((int)(Mathf.Repeat(uc.y, 1f) * ph), 0, ph - 1);
                    int cls = Classify(pal[py * pw + px], kit);
                    Vector2 ua = uvs[a], ub = uvs[b], ud = uvs[d];
                    if (cls == 1 || cls == 2)
                    {
                        var pa = toMeters.MultiplyPoint3x4(verts[a]);
                        var pb = toMeters.MultiplyPoint3x4(verts[b]);
                        var pd = toMeters.MultiplyPoint3x4(verts[d]);
                        var fn = Vector3.Cross(pb - pa, pd - pa);
                        var ax = new Vector3(Mathf.Abs(fn.x), Mathf.Abs(fn.y), Mathf.Abs(fn.z));
                        Vector2 Proj(Vector3 p) => ax.y >= ax.x && ax.y >= ax.z ? new Vector2(p.x, p.z)
                            : ax.x >= ax.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                        ua = Proj(pa); ub = Proj(pb); ud = Proj(pd);
                    }
                    int start = nv.Count;
                    nv.Add(verts[a]); nv.Add(verts[b]); nv.Add(verts[d]);
                    if (normals.Length > 0) { nn.Add(normals[a]); nn.Add(normals[b]); nn.Add(normals[d]); }
                    nuv.Add(ua); nuv.Add(ub); nuv.Add(ud);
                    tris[cls].Add(start); tris[cls].Add(start + 1); tris[cls].Add(start + 2);
                }
            }
            var m = new Mesh { name = src.name + "_Gercekci" };
            if (nv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(nv);
            if (nn.Count == nv.Count) m.SetNormals(nn); else m.RecalculateNormals();
            m.SetUVs(0, nuv);
            m.subMeshCount = 4;
            for (int i = 0; i < 4; i++) m.SetTriangles(tris[i], i);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>0: palet, 1: duvar, 2: çatı, 3: cam.</summary>
        static int Classify(Color32 p, string kit)
        {
            int mx = Math.Max(p.r, Math.Max(p.g, p.b)), mn = Math.Min(p.r, Math.Min(p.g, p.b));
            if (p.b > 175 && p.g > p.r + 8 && p.b > p.r + 25) return 3;
            if (mn > 195 && mx - mn < 45) return 1;
            if (p.r > 235 && p.g > 195 && p.b > 150 && p.r >= p.g && p.g >= p.b) return 1;
            if (kit == "CitySuburban" && p.g > p.r + 35 && p.g > p.b + 5) return 2;
            return 0;
        }

        static (Color32[] px, int w, int h) Palette(Ctx c, string kit)
        {
            if (c.palettes.TryGetValue(kit, out var cached)) return cached;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(FullPath($"{KenneyDir}/{kit}/Textures/colormap.png")));
            var result = (tex.GetPixels32(), tex.width, tex.height);
            Object.DestroyImmediate(tex);
            c.palettes[kit] = result;
            return result;
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
