using System;
using System.Collections;
using System.Collections.Generic;
using CityRise.CameraRig;
using CityRise.City;
using CityRise.Core;
using CityRise.Interior;
using CityRise.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityRise.Game
{
    /// <summary>
    /// Oyunu sahneye bağlar: kaydı yükler, kuralları (CityEconomy) çalıştırır, dokunmaları arsalara çevirir,
    /// kurulan işletmeleri sahnede gösterir, aşama değişince yeni halkayı açar ve düzenli kaydeder.
    /// Sahnedeki referansları TownBuilder doldurur.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class BusinessTemplate
        {
            public string businessId;
            public GameObject template;
        }

        [Header("Veri")]
        public CityLayout layoutAsset;
        public ProgressionConfig config;
        public BusinessCatalog catalog;

        [Header("Sahne")]
        public CityTown town;
        public CityCameraController cameraController;
        public Transform plotsRoot;
        public Transform wildernessRoot;
        public GameObject forSaleSignTemplate;
        public List<BusinessTemplate> templates = new List<BusinessTemplate>();
        public InteriorLibrary interiorLibrary;

        /// <summary>İç görünümün kurulduğu yer: şehirden uzakta, kamera şehri görmesin.</summary>
        static readonly Vector3 InteriorOrigin = new Vector3(6000f, 0f, 6000f);

        public CityEconomy Economy { get; private set; }
        public PlayerProfile Profile => Economy.profile;
        public MissionSystem Missions { get; private set; }
        public static GameManager Instance { get; private set; }

        readonly Dictionary<int, PlotView> views = new Dictionary<int, PlotView>();
        GameHud hud;
        GameObject interior;
        int interiorPlotId = -1;
        bool transitioning;
        CameraState savedCamera;

        struct CameraState
        {
            public Vector3 position;
            public float distance, yaw, minDistance, maxDistance;
            public CameraClearFlags clear;
            public Color background;
            public bool fog;
        }

        public bool InInterior => interior != null;
        public CityRise.Life.CityLife Life { get; private set; }
        public int InteriorPlotId => interiorPlotId;
        float saveTimer;
        Vector2 pressPos;
        float pressTime;
        bool pressOverUI;

        void Awake()
        {
            Instance = this;
            if (config == null) config = ProgressionConfig.CreateDefault();
            var layout = Instantiate(layoutAsset); // Oyun sırasında asıl düzen dosyası değişmesin.
            town.layout = layout;

            var profile = PlayerProfile.LoadOrNew(config);
            Economy = new CityEconomy(profile, config, catalog, layout);
            Economy.Restore();
            Missions = new MissionSystem(Economy);
            town.stage = profile.stage;

            foreach (var v in plotsRoot.GetComponentsInChildren<PlotView>(true)) views[v.plotId] = v;
            ApplyStage(profile.stage, null);
            foreach (var owned in profile.businesses)
                if (!owned.acquired) ShowBusiness(layout.FindPlot(owned.plotId), owned, animate: false);

            Economy.BusinessBuilt += (plot, owned) => ShowBusiness(plot, owned, animate: true);
            Economy.BusinessUpgraded += (plot, owned) =>
            {
                if (views.TryGetValue(plot.id, out var v) && v.building) StartCoroutine(Bounce(v.building.transform, 0.35f));
                if (interiorPlotId == plot.id) BuildInterior(plot, owned.businessId, owned.level);
            };
            Economy.BusinessAcquired += (plot, owned) =>
            {
                if (views.TryGetValue(plot.id, out var v)) StartCoroutine(Bounce(v.transform, 0.35f));
                if (interiorPlotId == plot.id) hud.EnterInterior(plot, owned, owned.businessId, owned.level);
                hud.Toast(Loc.T("interior.bought", Loc.T("business." + owned.businessId)));
            };
            Economy.StageUnlocked += (stage, blocks) => { town.stage = stage; ApplyStage(stage, blocks); };

            hud = gameObject.AddComponent<GameHud>();
            hud.Init(this);
            Life = new GameObject("SehirHayati").AddComponent<CityRise.Life.CityLife>();
            Life.Init(this);

            double hoursAway = (DateTime.UtcNow - new DateTime(profile.lastSaveUtcTicks, DateTimeKind.Utc)).TotalHours;
            if (profile.businesses.Count > 0 && hoursAway > 0.02)
            {
                double earned = Economy.ApplyOffline(hoursAway);
                if (earned >= 1) hud.ShowWelcome(earned);
            }
        }

        void Update()
        {
            Economy.Tick(Time.deltaTime);
            HandlePointer();
            saveTimer += Time.deltaTime;
            if (saveTimer > 15f) Save();
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Save()
        {
            saveTimer = 0;
            Economy?.profile.Save();
        }

        // ------------------------------------------------------------ Dokunma

        void HandlePointer()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                pressPos = pos;
                pressTime = Time.unscaledTime;
                pressOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            }
            if (pointer.press.wasReleasedThisFrame && !pressOverUI)
            {
                bool tap = Vector2.Distance(pos, pressPos) < Screen.height * 0.02f && Time.unscaledTime - pressTime < 0.5f;
                if (tap && !hud.IsModalOpen && !InInterior && !transitioning) OnTap(pos);
            }
#endif
        }

        void OnTap(Vector2 screenPos)
        {
            var cam = cameraController != null ? cameraController.cam : Camera.main;
            var ray = cam.ScreenPointToRay(screenPos);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
            var plot = town.PlotAt(ray.GetPoint(enter));
            if (plot == null) return;
            OnPlotTapped(plot);
        }

        public void OnPlotTapped(Plot plot)
        {
            var owned = Profile.Find(plot.id);
            if (owned != null)
            {
                OpenInterior(plot);
                return;
            }
            switch (plot.type)
            {
                case PlotType.ForSale:
                    if (Economy.IsOpen(plot)) hud.ShowBuyPanel(plot);
                    else hud.Toast(Loc.T("plot.locked", Loc.Stage(plot.unlockStage), config.LevelForStage(plot.unlockStage)));
                    break;
                case PlotType.Locked:
                    hud.Toast(Loc.T("plot.locked", Loc.Stage(plot.unlockStage), config.LevelForStage(plot.unlockStage)));
                    break;
                case PlotType.Business:
                    OpenInterior(plot);
                    break;
                case PlotType.House: hud.Toast(Loc.T("plot.house")); break;
                case PlotType.Park: hud.Toast(Loc.T("plot.park")); break;
                case PlotType.Plaza: hud.Toast(Loc.T("plot.plaza")); break;
                case PlotType.Civic: OpenInterior(plot); break;
            }
        }

        // ------------------------------------------------------------ Sahne

        /// <summary>Aşamaya göre yolları, çitleri, zemini gösterir; açılan blokların doğasını temizler.</summary>
        void ApplyStage(CityStage stage, List<Vector2Int> newBlocks)
        {
            foreach (var g in FindObjectsByType<StageGated>(FindObjectsInactive.Include)) g.Apply(stage);

            var grid = town.Grid;
            int maxRing = CityEconomy.MaxRingFor(stage);
            if (wildernessRoot != null)
            {
                foreach (Transform t in wildernessRoot)
                {
                    var block = grid.BlockOf(grid.WorldToCell(t.position));
                    if (CityGrid.Ring(block) <= maxRing) t.gameObject.SetActive(false);
                }
            }

            // Yeni açılan arsalar için sahne nesnesi ve SATILIK tabelası.
            foreach (var plot in town.layout.plots)
            {
                if (plot.type != PlotType.ForSale || views.ContainsKey(plot.id)) continue;
                var go = new GameObject($"Arsa_{plot.id}_{plot.type}");
                go.transform.SetParent(plotsRoot, false);
                var view = go.AddComponent<PlotView>();
                view.plotId = plot.id;
                views[plot.id] = view;
                if (forSaleSignTemplate != null)
                {
                    var decor = new GameObject("Susler").transform;
                    decor.SetParent(go.transform, false);
                    var sign = Instantiate(forSaleSignTemplate, decor);
                    sign.SetActive(true);
                    sign.transform.SetPositionAndRotation(SignPosition(plot), Quaternion.Euler(0, Plot.FacingYaw(plot.facing), 0));
                }
            }

            if (cameraController != null)
            {
                float reach = (grid.BlockRect(new Vector2Int(maxRing, maxRing)).xMax + 6) * grid.cellSize;
                cameraController.boundsMin = new Vector2(-reach, -reach);
                cameraController.boundsMax = new Vector2(reach, reach);
            }
        }

        Vector3 SignPosition(Plot plot)
        {
            var g = town.Grid;
            float half = Mathf.Min(plot.cells.width, plot.cells.height) * g.cellSize * 0.5f;
            var f = Plot.FacingDirection(plot.facing);
            return g.RectCenter(plot.cells) + new Vector3(f.x, 0, f.y) * (half - 1.6f);
        }

        void ShowBusiness(Plot plot, OwnedBusiness owned, bool animate)
        {
            if (plot == null) return;
            var template = templates.Find(t => t.businessId == owned.businessId)?.template;
            if (template == null)
            {
                Debug.LogWarning($"CityRise: '{owned.businessId}' için bina şablonu yok. Kasabayı menüden yeniden kurun.");
                return;
            }
            if (!views.TryGetValue(plot.id, out var view))
            {
                var go = new GameObject($"Arsa_{plot.id}");
                go.transform.SetParent(plotsRoot, false);
                view = go.AddComponent<PlotView>();
                view.plotId = plot.id;
                views[plot.id] = view;
            }
            view.ClearDecor();
            var g = town.Grid;
            var b = Instantiate(template, view.transform);
            b.name = owned.businessId;
            b.SetActive(true);
            b.transform.SetPositionAndRotation(g.RectCenter(plot.cells), Quaternion.Euler(0, Plot.FacingYaw(plot.facing), 0));
            var ground = b.transform.Find("Zemin");
            // Parke yalnızca binanın kapladığı alanda; büyük arsanın geri kalanı çimen kalır.
            var fp = catalog.Find(owned.businessId)?.footprint ?? Vector2Int.one;
            if (ground != null) ground.localScale = new Vector3(Mathf.Min(fp.x, plot.cells.width) * g.cellSize, 1f, Mathf.Min(fp.y, plot.cells.height) * g.cellSize);
            view.building = b;
            if (animate) StartCoroutine(Construct(b.transform.Find("Bina") ?? b.transform));
        }

        /// <summary>Basit inşaat animasyonu: bina yerden yükselir ve hafifçe sekerek yerine oturur.</summary>
        IEnumerator Construct(Transform t)
        {
            var target = t.localScale;
            float d = 1.1f;
            for (float time = 0; time < d; time += Time.deltaTime)
            {
                float k = time / d;
                float y = 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2);
                t.localScale = new Vector3(target.x * Mathf.Min(1f, 0.6f + k), target.y * Mathf.Max(0.02f, y), target.z * Mathf.Min(1f, 0.6f + k));
                yield return null;
            }
            t.localScale = target;
        }

        IEnumerator Bounce(Transform t, float duration)
        {
            var baseScale = t.localScale;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float s = 1f + 0.08f * Mathf.Sin(time / duration * Mathf.PI);
                t.localScale = baseScale * s;
                yield return null;
            }
            t.localScale = baseScale;
        }

        // ------------------------------------------------------------ İç görünüm

        /// <summary>
        /// İşletmenin (oyuncunun ya da komşunun) veya belediyenin içini kesit olarak açar;
        /// kamera yumuşak geçişle oraya gider.
        /// </summary>
        public void OpenInterior(Plot plot)
        {
            if (plot == null || interiorLibrary == null || InInterior) return;
            var owned = Profile.Find(plot.id);
            string id;
            int level;
            if (owned != null) { id = owned.businessId; level = owned.level; }
            else if (plot.type == PlotType.Business && !string.IsNullOrEmpty(plot.occupantId)) { id = plot.occupantId; level = 2; }
            else if (plot.type == PlotType.Civic) { id = "belediye"; level = 2; }
            else return;
            StartCoroutine(OpenWhenReady(plot, owned, id, level));
        }

        IEnumerator OpenWhenReady(Plot plot, OwnedBusiness owned, string businessId, int level)
        {
            while (transitioning) yield return null;
            if (!InInterior) yield return OpenInteriorRoutine(plot, owned, businessId, level);
        }

        public void CloseInterior()
        {
            if (!InInterior || transitioning) return;
            StartCoroutine(CloseInteriorRoutine());
        }

        IEnumerator OpenInteriorRoutine(Plot plot, OwnedBusiness owned, string businessId, int level)
        {
            transitioning = true;
            yield return ZoomTo(CoinAnchor(plot) - Vector3.up * 10f, 26f, 0.35f);
            yield return hud.Fade(1f, 0.2f);

            var ctrl = cameraController;
            var cam = ctrl.cam;
            savedCamera = new CameraState
            {
                position = ctrl.transform.position, distance = ctrl.distance, yaw = ctrl.yaw,
                minDistance = ctrl.minDistance, maxDistance = ctrl.maxDistance,
                clear = cam.clearFlags, background = cam.backgroundColor, fog = RenderSettings.fog,
            };
            interiorPlotId = plot.id;
            ctrl.orbitMode = true;
            ctrl.yaw = 42f;
            BuildInterior(plot, businessId, level);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            RenderSettings.fog = false;
            hud.EnterInterior(plot, owned, businessId, level);
            // İlk karede malzemeler hazırlanırken ekran kararık kalsın.
            yield return null;
            yield return null;
            yield return hud.Fade(0f, 0.3f);
            transitioning = false;
        }

        IEnumerator CloseInteriorRoutine()
        {
            transitioning = true;
            yield return hud.Fade(1f, 0.2f);
            if (interior != null) Destroy(interior);
            interior = null;
            interiorPlotId = -1;
            var ctrl = cameraController;
            var cam = ctrl.cam;
            ctrl.orbitMode = false;
            ctrl.transform.position = savedCamera.position;
            ctrl.minDistance = savedCamera.minDistance;
            ctrl.maxDistance = savedCamera.maxDistance;
            ctrl.distance = savedCamera.distance;
            ctrl.yaw = savedCamera.yaw;
            cam.clearFlags = savedCamera.clear;
            cam.backgroundColor = savedCamera.background;
            RenderSettings.fog = savedCamera.fog;
            hud.ExitInterior();
            yield return hud.Fade(0f, 0.3f);
            transitioning = false;
        }

        void BuildInterior(Plot plot, string businessId, int level)
        {
            if (interior != null) Destroy(interior);
            interior = InteriorBuilder.Build(interiorLibrary, businessId, level, SignMaterial(plot, businessId), InteriorOrigin);
            FrameInterior();
        }

        /// <summary>
        /// Kamerayı odanın tamamı (tabela ve toprak dilimi dahil) ekrana sığacak uzaklığa koyar.
        /// Üst ve alttaki arayüz şeritleri için dikey görüş biraz daraltılarak hesaplanır.
        /// </summary>
        void FrameInterior()
        {
            var renderers = interior.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var ctrl = cameraController;
            var cam = ctrl.cam;
            float radius = b.extents.magnitude;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad * 0.74f;
            float hHalf = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect) * 0.92f;
            float d = radius / Mathf.Sin(Mathf.Min(vHalf, hHalf)) * 0.76f;
            ctrl.transform.position = b.center;
            ctrl.distance = d;
            ctrl.minDistance = d * 0.45f;
            ctrl.maxDistance = d * 1.3f;
        }

        /// <summary>Tabela: komşunun binasındaki tabela, yoksa işletme şablonundaki.</summary>
        Material SignMaterial(Plot plot, string businessId)
        {
            if (views.TryGetValue(plot.id, out var view))
                foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Yazi" && r.transform.parent != null && r.transform.parent.name == "Tabela") return r.sharedMaterial;
            var template = templates.Find(t => t.businessId == businessId)?.template;
            var face = template != null ? template.transform.Find("Tabela/Yazi") : null;
            return face != null ? face.GetComponent<Renderer>().sharedMaterial : null;
        }

        /// <summary>Kamerayı yumuşakça bir noktaya ve mesafeye götürür.</summary>
        IEnumerator ZoomTo(Vector3 target, float distance, float duration)
        {
            var ctrl = cameraController;
            var startPos = ctrl.transform.position;
            float startDist = ctrl.distance;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                ctrl.transform.position = Vector3.Lerp(startPos, new Vector3(target.x, 0, target.z), k);
                ctrl.distance = Mathf.Lerp(startDist, Mathf.Clamp(distance, ctrl.minDistance, ctrl.maxDistance), k);
                yield return null;
            }
        }

        /// <summary>Bir işletmenin ekrandaki para simgesinin dünya konumu (binanın üstü).</summary>
        public Vector3 CoinAnchor(Plot plot)
        {
            var g = town.Grid;
            return g.RectCenter(plot.cells) + Vector3.up * 13f;
        }

        public Camera ActiveCamera => cameraController != null ? cameraController.cam : Camera.main;

        /// <summary>Geliştirme için: kaydı silip oyunu baştan başlatır.</summary>
        [ContextMenu("Kaydı Sil")]
        public void ResetSave() => PlayerProfile.Delete();
    }
}
