using System.Collections;
using System.Collections.Generic;
using CityRise.City;
using CityRise.Core;
using CityRise.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityRise.Game
{
    /// <summary>
    /// Oyun arayüzü (yatay ekran): sol üstte seviye ve XP çubuğu, sağ üstte nakit,
    /// arsa satın alma ve işletme pencereleri, seviye atlama penceresi, binaların üstünde para simgeleri.
    /// Tüm metinler Loc anahtarlarıyla gelir.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        GameManager game;
        CityEconomy eco;
        RectTransform root, safeArea, modalLayer, coinLayer, interiorBar;
        Text interiorTitle, interiorInfo;
        Button collectButton, upgradeButton, buyButton, editButton, productsButton;
        RectTransform productPanel, productGrid, popupLayer;
        Text productInfo;
        Button depotButton;
        float productRefresh;
        bool openProductsOnEnter;
        readonly Dictionary<int, float> lastCityPopup = new Dictionary<int, float>();
        RectTransform editPanel, editTabs, editGrid;
        Text editHint;
        ShopOptions.Group editGroup = ShopOptions.Group.Floor;
        string interiorId;
        int interiorLevel;
        CanvasGroup fade;
        Rect lastSafe;
        OwnedBusiness interiorBusiness;
        Plot interiorPlot;
        Text levelNumber, levelLabel, xpLabel, cashLabel, toastLabel;
        Image xpFill;
        CanvasGroup toastGroup;
        Coroutine toastRoutine;
        readonly Dictionary<int, Button> coins = new Dictionary<int, Button>();
        readonly Queue<System.Action> pendingModals = new Queue<System.Action>();
        RectTransform missionPanel, missionList, eventBanner, clockPanel;
        Text clockLabel;
        Button statsButton;
        Text eventLabel;
        float eventTimer;
        Text missionHeader;
        bool missionsCollapsed;
        MissionSystem missions;

        public RectTransform Root => root;
        /// <summary>Rehber balonu açıkken bildirimler onun üstünde görünür.</summary>
        public bool GuideActive;

        public bool IsModalOpen => modalLayer != null && modalLayer.childCount > 0;

        public void Init(GameManager gm)
        {
            game = gm;
            eco = gm.Economy;
            Build();
            Refresh();
            eco.Changed += Refresh;
            eco.Collected += (plot, amount, xp) => Toast(Loc.T("collect.done", Loc.Money(amount), xp));
            eco.BusinessBuilt += (plot, owned) => Toast(Loc.T("buy.done", Loc.T("business." + owned.businessId)));
            eco.LeveledUp += (level, unlocked) => QueueModal(() => ShowLevelUp(level, unlocked));
            eco.StageUnlocked += (stage, blocks) => QueueModal(() => ShowStage(stage));
            missions = gm.Missions;
            if (missions != null)
            {
                missions.Changed += RefreshMissions;
                eco.Changed += RefreshMissions;
                missions.Completed += d => Toast(Loc.T("mission.done", MissionTitle(d)));
                missions.Claimed += d => Toast(Loc.T("mission.claimed", Loc.Money(d.rewardCash), d.rewardXp));
                RefreshMissions();
            }
        }

        // ------------------------------------------------------------ Kurulum

        void Build()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var canvasGo = new GameObject("Arayuz", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRoot = (RectTransform)canvasGo.transform;
            // Çentikli ekranlarda arayüz güvenli alanın içinde kalır.
            safeArea = UIKit.Rect("GuvenliAlan", canvasRoot).Stretch();
            root = safeArea;

            coinLayer = UIKit.Rect("Paralar", canvasRoot).Stretch();
            coinLayer.SetAsFirstSibling();

            // Sol üst: seviye rozeti, seviye/aşama yazısı ve XP çubuğu.
            var levelPanel = UIKit.Box("SeviyePaneli", root, UIKit.Panel).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(470, 118));
            var badge = UIKit.Box("Rozet", levelPanel, UIKit.Accent, UIKit.Circle).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(92, 92));
            levelNumber = UIKit.Label("Numara", badge, "1", 46, UIKit.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            levelNumber.rectTransform.Stretch();
            levelLabel = UIKit.Label("SeviyeYazi", levelPanel, "", 30, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            levelLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(124, -14), new Vector2(330, 40));
            var bar = UIKit.Box("XPCubugu", levelPanel, new Color(1, 1, 1, 0.15f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(124, 20), new Vector2(326, 34));
            xpFill = UIKit.Box("Dolu", bar, UIKit.Green);
            xpFill.rectTransform.anchorMin = Vector2.zero;
            xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.pivot = new Vector2(0, 0.5f);
            xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
            xpLabel = UIKit.Label("XPYazi", bar, "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            xpLabel.rectTransform.Stretch();

            // Sağ üst: nakit.
            var cashPanel = UIKit.Box("NakitPaneli", root, UIKit.Panel).rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(340, 88));
            var coin = UIKit.Box("Simge", cashPanel, UIKit.Accent, UIKit.Circle).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(60, 60));
            UIKit.Label("Dolar", coin, "$", 38, UIKit.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            cashLabel = UIKit.Label("Nakit", cashPanel, "", 40, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            cashLabel.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(240, 60));

            // Sağ üst, nakdin solunda: oyun saati (gün-gece döngüsü varsa).
            clockPanel = UIKit.Box("Saat", root, UIKit.Panel).rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-384, -24), new Vector2(200, 88));
            clockLabel = UIKit.Label("Yazi", clockPanel, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            clockLabel.rectTransform.Stretch(6);

            // Sağ üst, nakdin altında: haftalık etkinlik.
            var evt = UIKit.Button("Etkinlik", root, "", new Color(0.55f, 0.2f, 0.65f, 0.92f), () =>
            {
                string id = WeeklyEvent.FeaturedBusiness();
                Toast(Loc.T("event.toast", Loc.T("business." + id), WeeklyEvent.Multiplier));
            }, 24);
            eventBanner = (RectTransform)evt.transform;
            eventBanner.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -124), new Vector2(420, 84));
            eventLabel = evt.GetComponentInChildren<Text>();
            eventLabel.alignment = TextAnchor.MiddleCenter;
            UpdateEvent();

            // Alt orta: kısa bildirimler.
            var toast = UIKit.Box("Bildirim", root, UIKit.Panel).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 76));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.blocksRaycasts = false;
            toastLabel = UIKit.Label("Yazi", toast, "", 30, Color.white, TextAnchor.MiddleCenter);
            toastLabel.rectTransform.Stretch(12);

            // Seviye panelinin yanında istatistik düğmesi.
            statsButton = UIKit.Button("Istatistik", root, Loc.T("stats.button"), UIKit.Panel, ShowStats, 26);
            ((RectTransform)statsButton.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(512, -24), new Vector2(190, 88));

            // Sol: görevler (seviye panelinin altında; başlığa dokununca katlanır).
            missionPanel = UIKit.Box("Gorevler", root, UIKit.Panel).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -160), new Vector2(470, 60));
            var header = UIKit.Button("Baslik", missionPanel, "", new Color(0, 0, 0, 0), () => { missionsCollapsed = !missionsCollapsed; RefreshMissions(); }, 28);
            ((RectTransform)header.transform).Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(470, 56));
            missionHeader = header.GetComponentInChildren<Text>();
            missionHeader.alignment = TextAnchor.MiddleLeft;
            missionHeader.rectTransform.offsetMin = new Vector2(20, 0);
            missionList = UIKit.Rect("Liste", missionPanel);
            missionList.anchorMin = new Vector2(0, 0);
            missionList.anchorMax = new Vector2(1, 1);
            missionList.offsetMin = new Vector2(10, 10);
            missionList.offsetMax = new Vector2(-10, -56);

            BuildInteriorBar();
            modalLayer = UIKit.Rect("Pencereler", root).Stretch();

            var fadeImg = UIKit.Box("Gecis", canvasRoot, Color.black, UIKit.Rounded);
            fadeImg.rectTransform.Stretch(-40);
            fadeImg.raycastTarget = false;
            fade = fadeImg.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0;
            fade.blocksRaycasts = false;
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == lastSafe || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafe = sa;
            safeArea.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>İç görünümde: üstte işletme adı, altta Şehre Dön / Topla / Yükselt.</summary>
        void BuildInteriorBar()
        {
            interiorBar = UIKit.Rect("IcGorunum", root).Stretch();
            var title = UIKit.Box("Baslik", interiorBar, UIKit.Panel).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(640, 118));
            interiorTitle = UIKit.Label("Ad", title, "", 40, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            interiorTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(600, 54));
            interiorInfo = UIKit.Label("Bilgi", title, "", 26, new Color(1, 1, 1, 0.8f), TextAnchor.UpperCenter);
            interiorInfo.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(600, 40));

            var back = UIKit.Button("SehreDon", interiorBar, Loc.T("interior.back"), UIKit.Panel, () => game.CloseInterior(), 32);
            ((RectTransform)back.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(260, 100));
            collectButton = UIKit.Button("Topla", interiorBar, "", UIKit.Accent, () => { if (interiorPlot != null) eco.Collect(interiorPlot.id); }, 30);
            ((RectTransform)collectButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-416, 28), new Vector2(260, 100));
            upgradeButton = UIKit.Button("Yukselt", interiorBar, "", UIKit.Green, () =>
            {
                if (interiorPlot != null && eco.Upgrade(interiorPlot.id))
                    Toast(Loc.T("biz.upgraded", Loc.T("business." + interiorBusiness.businessId), interiorBusiness.level));
            }, 28);
            ((RectTransform)upgradeButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(372, 100));
            editButton = UIKit.Button("Duzenle", interiorBar, Loc.T("edit.open"), new Color(0.45f, 0.35f, 0.75f), () => OpenEditPanel(), 30);
            ((RectTransform)editButton.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(304, 28), new Vector2(220, 100));
            productsButton = UIKit.Button("Urunler", interiorBar, Loc.T("prod.open"), new Color(0.15f, 0.55f, 0.6f), () => OpenProductPanel(), 30);
            ((RectTransform)productsButton.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(540, 28), new Vector2(220, 100));
            BuildEditPanel();
            BuildProductPanel();
            buyButton = UIKit.Button("SatinAl", interiorBar, "", UIKit.Green, () => { if (interiorPlot != null) eco.Acquire(interiorPlot); }, 30);
            ((RectTransform)buyButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(480, 100));
            interiorBar.gameObject.SetActive(false);
        }

        /// <summary>owned boşsa işletme komşunundur (ya da kamu binasıdır).</summary>
        public void EnterInterior(Plot plot, OwnedBusiness owned, string businessId, int level)
        {
            interiorPlot = plot;
            interiorBusiness = owned;
            interiorId = businessId;
            interiorLevel = level;
            interiorBar.gameObject.SetActive(true);
            coinLayer.gameObject.SetActive(false);
            missionPanel.gameObject.SetActive(false);
            RefreshInterior();
            if (openProductsOnEnter && owned != null) OpenProductPanel();
            openProductsOnEnter = false;
        }

        public void ExitInterior()
        {
            editPanel.gameObject.SetActive(false);
            productPanel.gameObject.SetActive(false);
            interiorPlot = null;
            interiorBusiness = null;
            interiorId = null;
            interiorBar.gameObject.SetActive(false);
            coinLayer.gameObject.SetActive(true);
            missionPanel.gameObject.SetActive(true);
        }

        void RefreshInterior()
        {
            if (interiorPlot == null) return;
            bool mine = interiorBusiness != null;
            if (!mine) { editPanel.gameObject.SetActive(false); productPanel.gameObject.SetActive(false); }
            bool editing = editPanel.gameObject.activeSelf || productPanel.gameObject.activeSelf;
            collectButton.gameObject.SetActive(mine && !editing);
            upgradeButton.gameObject.SetActive(mine && !editing);
            editButton.gameObject.SetActive(mine && !editing);
            productsButton.gameObject.SetActive(mine && !editing);
            if (!mine)
            {
                var neighborDef = eco.catalog.Find(interiorId);
                interiorTitle.text = Loc.T("biz.title", Loc.T("business." + interiorId), interiorLevel);
                buyButton.gameObject.SetActive(neighborDef != null);
                if (neighborDef == null)
                {
                    interiorInfo.text = Loc.T("interior.civic");
                    return;
                }
                interiorInfo.text = Loc.T("interior.owner", Loc.Money(neighborDef.NetPerHour));
                var check = eco.CanAcquire(interiorPlot);
                long price = eco.AcquirePrice(interiorPlot);
                buyButton.SetText(check == CityEconomy.BuildCheck.LevelTooLow ? Loc.T("buy.locked", neighborDef.requiredLevel)
                    : check == CityEconomy.BuildCheck.StageLocked ? Loc.T("buy.stage", Loc.Stage(neighborDef.requiredStage))
                    : check == CityEconomy.BuildCheck.NotEnoughCash ? Loc.T("buy.nocash", Loc.Money(price))
                    : Loc.T("interior.buy", Loc.Money(price)));
                buyButton.interactable = check == CityEconomy.BuildCheck.Ok;
                return;
            }
            buyButton.gameObject.SetActive(false);
            var b = interiorBusiness;
            var def = eco.catalog.Find(b.businessId);
            interiorTitle.text = Loc.T("biz.title", Loc.T("business." + b.businessId), b.level);
            int total = b.TotalStock();
            interiorInfo.text = (total == 0 ? Loc.T("prod.empty.short") : Loc.T("prod.info", total, Mathf.RoundToInt((float)eco.SalesPerHour(b))))
                + (WeeklyEvent.IncomeMultiplier(b.businessId) > 1 ? "  ·  " + Loc.T("event.short", WeeklyEvent.Multiplier) : "");
            interiorInfo.color = total == 0 ? new Color(1f, 0.55f, 0.5f) : new Color(1, 1, 1, 0.8f);
            productsButton.image.color = total == 0 ? new Color(0.85f, 0.25f, 0.2f) : new Color(0.15f, 0.55f, 0.6f);
            collectButton.SetText(Loc.T("interior.collect", Loc.Money(b.stored)));
            collectButton.interactable = b.stored >= 1;
            bool max = def == null || b.level >= def.maxLevel;
            upgradeButton.SetText(max ? Loc.T("biz.max") : Loc.T("biz.upgrade", Loc.Money(eco.UpgradeCost(b))));
            upgradeButton.interactable = !max && eco.CanUpgrade(b);
        }

        public IEnumerator Fade(float target, float duration)
        {
            float start = fade.alpha;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            fade.alpha = target;
            fade.blocksRaycasts = target > 0.5f;
        }

        void Refresh()
        {
            var p = eco.profile;
            levelNumber.text = p.level.ToString();
            levelLabel.text = $"{Loc.T("hud.level", p.level)}  ·  {Loc.Stage(p.stage)}";
            float k = Mathf.Clamp01(p.xp / (float)p.XpToNextLevel);
            xpFill.rectTransform.anchorMax = new Vector2(k, 1);
            xpLabel.text = Loc.T("hud.xp", p.xp, p.XpToNextLevel);
            cashLabel.text = Loc.Money(p.cash).Substring(1);
        }

        void UpdateEvent()
        {
            var now = System.DateTime.Now;
            string id = WeeklyEvent.FeaturedBusiness(now);
            var left = WeeklyEvent.TimeLeft(now);
            string time = left.TotalDays >= 1 ? Loc.T("event.days", (int)left.TotalDays, left.Hours) : Loc.T("event.hours", left.Hours, left.Minutes);
            eventLabel.text = Loc.T("event.banner", Loc.T("event.name." + id)) + "\n" + Loc.T("event.sub", WeeklyEvent.Multiplier, time);
        }

        void LateUpdate()
        {
            ApplySafeArea();
            eventTimer -= Time.unscaledDeltaTime;
            if (eventTimer <= 0) { eventTimer = 20f; UpdateEvent(); }
            eventBanner.gameObject.SetActive(interiorPlot == null);
            statsButton.gameObject.SetActive(interiorPlot == null);
            var cycle = CityRise.Life.DayNightCycle.Instance;
            // Dükkan içinde saat gizlenir (başlıkla çakışmasın).
            clockPanel.gameObject.SetActive(cycle != null && interiorPlot == null);
            if (cycle != null)
            {
                int h = Mathf.FloorToInt(cycle.Hour), m = Mathf.FloorToInt((cycle.Hour - h) * 60f) / 10 * 10;
                clockLabel.text = Loc.T(cycle.IsNight ? "clock.night" : "clock.day", $"{h:00}:{m:00}");
                clockLabel.color = cycle.IsNight ? new Color(0.75f, 0.82f, 1f) : new Color(1f, 0.9f, 0.55f);
            }
            if (interiorPlot != null)
            {
                RefreshInterior();
                ShowPendingSales();
                var flow = CityRise.Life.ShopFlow.Active;
                if (!tipHintShown && flow != null && flow.SomeoneAtRegister)
                {
                    tipHintShown = true;
                    Toast(Loc.T("live.hint"));
                }
                productRefresh -= Time.unscaledDeltaTime;
                if (productRefresh <= 0) { productRefresh = 0.5f; RefreshProductPanel(); }
            }
            else UpdateCoins();
#if ENABLE_INPUT_SYSTEM
            // Android geri tuşu / Esc: iç görünümden ya da pencereden çık.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (IsModalOpen) CloseModal();
                else if (game.InInterior) game.CloseInterior();
            }
#endif
        }

        /// <summary>Geliri biriken işletmelerin üstünde dokunulabilir para simgesi.</summary>
        void UpdateCoins()
        {
            var cam = game.ActiveCamera;
            if (cam == null) return;
            foreach (var owned in eco.profile.businesses)
            {
                bool empty = owned.TotalStock() == 0 && owned.DepotTotal() == 0 && owned.deliveries.Count == 0;
                bool show = (empty || owned.stored >= Mathf.Max(1f, (float)eco.NetPerHour(owned) / 60f)) && !IsModalOpen;
                coins.TryGetValue(owned.plotId, out var button);
                if (!show)
                {
                    if (button != null) button.gameObject.SetActive(false);
                    continue;
                }
                var plot = eco.layout.FindPlot(owned.plotId);
                var sp = cam.WorldToScreenPoint(game.CoinAnchor(plot));
                if (sp.z < 0)
                {
                    if (button != null) button.gameObject.SetActive(false);
                    continue;
                }
                if (button == null)
                {
                    int id = owned.plotId;
                    button = UIKit.Button("Para", coinLayer, "$", UIKit.Accent, () =>
                    {
                        var ob = eco.profile.Find(id);
                        // Para varsa topla; stok bittiyse dükkana girip ürün paneli aç.
                        if (ob != null && ob.stored >= 1) eco.Collect(id);
                        else if (ob != null && ob.TotalStock() == 0) { openProductsOnEnter = true; game.OpenInterior(eco.layout.FindPlot(id)); }
                    }, 40);
                    button.image.sprite = UIKit.Circle;
                    button.image.type = Image.Type.Simple;
                    button.GetComponentInChildren<Text>().color = UIKit.TextDark;
                    ((RectTransform)button.transform).sizeDelta = new Vector2(78, 78);
                    coins[id] = button;
                }
                button.gameObject.SetActive(true);
                bool showMoney = owned.stored >= 1;
                button.image.color = showMoney ? UIKit.Accent : new Color(0.9f, 0.25f, 0.2f);
                var label = button.GetComponentInChildren<Text>();
                label.text = showMoney ? "$" : "!";
                label.color = showMoney ? UIKit.TextDark : Color.white;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(coinLayer, sp, null, out var local);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float bob = Mathf.Sin(Time.time * 3f + owned.plotId) * 6f;
                rt.anchoredPosition = local + new Vector2(0, bob);
            }
        }

        // ------------------------------------------------------------ Ürünler ve satış

        void BuildProductPanel()
        {
            productPanel = UIKit.Box("UrunPaneli", interiorBar, UIKit.Panel).rectTransform;
            productPanel.anchorMin = new Vector2(1, 0);
            productPanel.anchorMax = new Vector2(1, 1);
            productPanel.pivot = new Vector2(1, 0.5f);
            productPanel.offsetMin = new Vector2(-760, 28);
            productPanel.offsetMax = new Vector2(-28, -160);
            UIKit.Label("Baslik", productPanel, Loc.T("prod.title"), 38, Color.white, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(420, 50));
            var close = UIKit.Button("Kapat", productPanel, "Tamam", UIKit.Green, () => { productPanel.gameObject.SetActive(false); game.ShiftInteriorView(false); }, 34);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(150, 64));
            var fill = UIKit.Button("HepsiniDoldur", productPanel, Loc.T("prod.fillall"), UIKit.Accent, () =>
            {
                if (interiorPlot == null) return;
                int n = eco.RestockAll(interiorPlot.id);
                if (n == 0) Toast(Loc.T("prod.full.or.cash"));
                RefreshProductPanel();
            }, 24);
            ((RectTransform)fill.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-176, -12), new Vector2(200, 64));
            fill.GetComponentInChildren<Text>().color = UIKit.TextDark;
            productInfo = UIKit.Label("Bilgi", productPanel, "", 21, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft);
            productInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -80), new Vector2(380, 60));
            // Depo: doluluk, "Rafa diz", depoyu büyüt.
            var shelve = UIKit.Button("RafaDiz", productPanel, Loc.T("prod.shelve"), new Color(0.2f, 0.5f, 0.75f), () =>
            {
                if (interiorPlot == null) return;
                if (eco.RefillNow(interiorPlot.id) == 0) Toast(Loc.T("prod.shelve.none"));
                RefreshProductPanel();
            }, 20);
            ((RectTransform)shelve.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, -84), new Vector2(150, 52));
            depotButton = UIKit.Button("DepoBuyut", productPanel, "", new Color(0.55f, 0.4f, 0.75f), () =>
            {
                if (interiorPlot == null) return;
                if (!eco.UpgradeDepot(interiorPlot.id)) Toast(Loc.T("edit.nocash"));
                RefreshProductPanel();
            }, 18);
            ((RectTransform)depotButton.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -84), new Vector2(160, 52));
            productGrid = UIKit.Rect("Urunler", productPanel);
            productGrid.anchorMin = Vector2.zero;
            productGrid.anchorMax = Vector2.one;
            productGrid.offsetMin = new Vector2(16, 16);
            productGrid.offsetMax = new Vector2(-16, -146);
            productPanel.gameObject.SetActive(false);

            popupLayer = UIKit.Rect("SatisYazilari", root).Stretch();
            eco.Sold += OnSold;
            eco.SoldOut += b => Toast(Loc.T("prod.soldout", Loc.T("business." + b.businessId)));
            eco.ProductionReady += (b, slot) =>
            {
                var p = Products.Find(b.businessId, slot.product);
                Toast(Loc.T("prod.ready", Loc.T("business." + b.businessId), slot.output, p?.name));
            };
            eco.DeliveryArrived += (b, p, n) => { if (p != null) Toast(Loc.T("prod.arrived", Loc.T("business." + b.businessId), n, p.name)); };
        }

        void OpenProductPanel()
        {
            if (interiorBusiness == null) return;
            editPanel.gameObject.SetActive(false);
            productPanel.gameObject.SetActive(true);
            game.ShiftInteriorView(true);
            RefreshProductPanel();
        }

        void RefreshProductPanel()
        {
            if (productPanel == null || !productPanel.gameObject.activeSelf || interiorBusiness == null) return;
            var b = interiorBusiness;
            for (int i = productGrid.childCount - 1; i >= 0; i--) Destroy(productGrid.GetChild(i).gameObject);
            productGrid.DetachChildren();
            int cap = eco.StockCapacity(b);
            productInfo.text = Loc.T("prod.depot", b.DepotTotal(), eco.DepotCapacity(b), b.OnTheWayTotal(), Mathf.RoundToInt((float)eco.SalesPerHour(b)));
            bool maxDepot = b.depotLevel >= Products.MaxDepotLevel;
            depotButton.SetText(maxDepot ? Loc.T("prod.depot.max") : Loc.T("prod.depot.up", Loc.Money(Products.DepotUpgradeCost(b.depotLevel))));
            depotButton.interactable = !maxDepot && eco.CanUpgradeDepot(b);
            var products = Products.For(b.businessId);
            var ingredients = Products.IngredientsFor(b.businessId);
            var list = new List<Product>(ingredients);
            list.AddRange(products);
            const float cw = 172, ch = 236, gap = 8;
            float top = 0;
            if (Products.Produces(b.businessId))
            {
                MachineStrip(b);
                top = 78;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                bool isIngredient = i < ingredients.Length;
                var recipe = Products.RecipeFor(b.businessId, p.id);
                int count = b.Count(p.id);
                int col = i % 4, row = i / 4;
                var cardColor = isIngredient ? new Color(0.55f, 0.45f, 0.3f, 0.35f) : count == 0 ? new Color(0.85f, 0.25f, 0.2f, 0.35f) : new Color(1, 1, 1, 0.1f);
                var card = UIKit.Box("Urun", productGrid, cardColor).rectTransform
                    .Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * (cw + gap), -top - row * (ch + gap)), new Vector2(cw, ch));
                var icon = new GameObject("Resim", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                icon.transform.SetParent(card, false);
                icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(92, 92));
                icon.texture = ProductIcons.Get(game.interiorLibrary, p.model);
                icon.raycastTarget = false;
                UIKit.Label("Ad", card, p.name, 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(cw - 8, 26));
                string priceText = isIngredient ? Loc.T("prod.ingredient", Loc.Money(p.cost))
                    : recipe != null ? Loc.T("prod.recipe", Loc.Money(p.price), recipe.ingredientAmount, Products.Ingredient(recipe.ingredient)?.name, recipe.output)
                    : Loc.T("prod.price", Loc.Money(p.price), Loc.Money(p.cost));
                UIKit.Label("Fiyat", card, priceText, 15, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(cw - 8, 22));
                // Stok çubuğu.
                if (isIngredient)
                {
                    // Hammadde: rafta durmaz; depodaki miktar ve sipariş düğmeleri.
                    UIKit.Label("Depo", card, b.OnTheWay(p.id) > 0 ? Loc.T("prod.depotway", b.DepotCount(p.id), b.OnTheWay(p.id), ArrivalText(b, p.id)) : Loc.T("prod.depotone", b.DepotCount(p.id)), 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                        .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(cw - 6, 40));
                    OrderButtons(card, b, p, cw);
                    continue;
                }
                var bar = UIKit.Box("Stok", card, new Color(1, 1, 1, 0.15f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(cw - 20, 18));
                var fillBar = UIKit.Box("Dolu", bar, count == 0 ? new Color(0.9f, 0.3f, 0.25f) : UIKit.Green).rectTransform;
                fillBar.anchorMin = Vector2.zero;
                fillBar.anchorMax = new Vector2(Mathf.Clamp01(count / (float)cap), 1);
                fillBar.offsetMin = fillBar.offsetMax = Vector2.zero;
                UIKit.Label("Sayi", bar, Loc.T("prod.shelf", count, cap), 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
                int way = b.OnTheWay(p.id);
                UIKit.Label("Depo", card, way > 0 ? Loc.T("prod.depotway", b.DepotCount(p.id), way, ArrivalText(b, p.id)) : Loc.T("prod.depotone", b.DepotCount(p.id)), 15, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(cw - 6, 22));
                if (recipe != null)
                {
                    // Üretilen ürün: fırına koy.
                    var check = eco.CanProduce(b, p.id);
                    string pid = p.id;
                    string label = check == CityEconomy.ProduceCheck.NoSlot ? Loc.T("prod.noslot", recipe.machine)
                        : check == CityEconomy.ProduceCheck.NoIngredient ? Loc.T("prod.noingredient", Products.Ingredient(recipe.ingredient)?.name)
                        : Loc.T("prod.bake", Mathf.RoundToInt((float)recipe.seconds));
                    var bake = UIKit.Button("Pisir", card, label, check == CityEconomy.ProduceCheck.Ok ? new Color(0.85f, 0.45f, 0.15f) : UIKit.Grey, () =>
                    {
                        if (interiorPlot == null) return;
                        if (eco.StartProduction(interiorPlot.id, pid)) Toast(Loc.T("prod.baking", p.name, recipe.machine));
                        RefreshProductPanel();
                    }, 15);
                    ((RectTransform)bake.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(cw - 12, 40));
                    bake.interactable = check == CityEconomy.ProduceCheck.Ok;
                    continue;
                }
                OrderButtons(card, b, p, cw);
            }
        }

        /// <summary>Sipariş düğmeleri: normal kamyon ve hızlı teslimat.</summary>
        void OrderButtons(RectTransform card, OwnedBusiness b, Product p, float cw)
        {
            {
                long cost = eco.RestockCost(b, p, out int amount);
                long fast = eco.RestockCost(b, p, out _, true);
                bool can = amount > 0 && eco.profile.cash >= cost;
                bool canFast = amount > 0 && eco.profile.cash >= fast;
                string id = p.id;
                var order = UIKit.Button("Siparis", card, amount <= 0 ? Loc.T("prod.depotfull") : Loc.T("prod.order", amount, Loc.Money(cost)), can ? UIKit.Green : UIKit.Grey, () =>
                {
                    if (interiorPlot == null) return;
                    if (!eco.Restock(interiorPlot.id, id)) Toast(Loc.T("edit.nocash"));
                    else Toast(Loc.T("prod.ordered", Mathf.RoundToInt((float)Products.NormalDeliverySeconds)));
                    RefreshProductPanel();
                }, 16);
                ((RectTransform)order.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(6, 8), new Vector2(cw - 66, 40));
                order.interactable = can;
                var express = UIKit.Button("Hizli", card, Loc.T("prod.fast"), canFast ? UIKit.Accent : UIKit.Grey, () =>
                {
                    if (interiorPlot == null) return;
                    if (!eco.Restock(interiorPlot.id, id, true)) Toast(Loc.T("edit.nocash"));
                    else Toast(Loc.T("prod.ordered.fast", Loc.Money(fast)));
                    RefreshProductPanel();
                }, 15);
                ((RectTransform)express.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 8), new Vector2(54, 40));
                express.GetComponentInChildren<Text>().color = UIKit.TextDark;
                express.interactable = canFast;
            }
        }

        /// <summary>Makine şeridi: gözler (boş / pişiyor kalan süre / hazır) ve "Hepsini al".</summary>
        void MachineStrip(OwnedBusiness b)
        {
            int slots = Products.SlotCount(b.level);
            string machine = Products.RecipeFor(b.businessId, Products.For(b.businessId)[0].id)?.machine ?? "";
            var strip = UIKit.Box("Makine", productGrid, new Color(0.85f, 0.45f, 0.15f, 0.25f)).rectTransform
                .Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(712, 70));
            UIKit.Label("Ad", strip, machine, 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(140, 40));
            bool anyReady = false;
            for (int i = 0; i < slots; i++)
            {
                var slot = UIKit.Box("Goz", strip, new Color(0, 0, 0, 0.3f)).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150 + i * 104, 0), new Vector2(96, 56));
                string text = Loc.T("prod.slot.empty");
                Color col = new Color(1, 1, 1, 0.5f);
                if (i < b.production.Count)
                {
                    var s = b.production[i];
                    var p = Products.Find(b.businessId, s.product);
                    double left = (new System.DateTime(s.endTicks, System.DateTimeKind.Utc) - System.DateTime.UtcNow).TotalSeconds;
                    if (left <= 0) { text = Loc.T("prod.slot.ready", s.output, p?.name); col = new Color(0.6f, 1f, 0.6f); anyReady = true; }
                    else { text = $"{p?.name}\n{Mathf.CeilToInt((float)left)} sn"; col = new Color(1f, 0.85f, 0.6f); }
                    // İlerleme çubuğu.
                    double total = (s.endTicks - s.startTicks) / (double)System.TimeSpan.TicksPerSecond;
                    var fill = UIKit.Box("Ilerleme", slot, new Color(0.95f, 0.6f, 0.2f, 0.6f)).rectTransform;
                    fill.anchorMin = Vector2.zero;
                    fill.anchorMax = new Vector2(Mathf.Clamp01(1f - (float)(left / total)), 0.12f);
                    fill.offsetMin = fill.offsetMax = Vector2.zero;
                }
                UIKit.Label("Durum", slot, text, 14, col, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch(2);
            }
            var collect = UIKit.Button("Al", strip, Loc.T("prod.collectbake"), anyReady ? UIKit.Green : UIKit.Grey, () =>
            {
                if (interiorPlot == null) return;
                int n = eco.CollectProduction(interiorPlot.id);
                if (n > 0) Toast(Loc.T("prod.collected", n));
                RefreshProductPanel();
            }, 18);
            ((RectTransform)collect.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(120, 54));
            collect.interactable = anyReady;
        }

        static string ArrivalText(OwnedBusiness b, string productId)
        {
            long soonest = long.MaxValue;
            foreach (var d in b.deliveries) if (d.id == productId && d.arriveTicks < soonest) soonest = d.arriveTicks;
            double secs = System.Math.Max(0, (new System.DateTime(soonest, System.DateTimeKind.Utc) - System.DateTime.UtcNow).TotalSeconds);
            return Mathf.CeilToInt((float)secs) + " sn";
        }

        /// <summary>Satış: dükkanın içindeysen kasanın üstünde "+$fiyat ürün", şehirde binanın üstünde "+$".</summary>
        void OnSold(OwnedBusiness b, Product p, long price)
        {
            if (popupLayer == null) return;
            if (interiorPlot != null)
            {
                if (interiorBusiness != b) return;
                // Satışlar sıraya girer; yazılar arka arkaya, farklı yüksekliklerde çıkar (üst üste binmez).
                if (pendingSales.Count < 6) pendingSales.Enqueue(Loc.T("prod.sold", Loc.Money(price), p.name));
                return;
            }
            if (IsModalOpen) return;
            if (lastCityPopup.TryGetValue(b.plotId, out float last) && Time.unscaledTime - last < 1.2f) return;
            lastCityPopup[b.plotId] = Time.unscaledTime;
            var cam = game.ActiveCamera;
            var plot = eco.layout.FindPlot(b.plotId);
            if (cam == null || plot == null) return;
            var sp = cam.WorldToScreenPoint(game.CoinAnchor(plot) + Vector3.up * 1.5f);
            if (sp.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, sp, null, out var local);
            StartCoroutine(Popup("+" + Loc.Money(price), local + new Vector2(50, 0), new Color(1f, 0.95f, 0.45f), 26));
        }

        /// <summary>Dünyadaki bir noktanın üstünde yükselen yazı (kaçan satış, bahşiş).</summary>
        public void WorldPopup(Vector3 world, string text, Color color)
        {
            var cam = game.ActiveCamera;
            if (cam == null || popupLayer == null) return;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, sp, null, out var local);
            StartCoroutine(Popup(text, local, color, 26));
        }

        bool tipHintShown;

        readonly Queue<string> pendingSales = new Queue<string>();
        float saleTimer;
        int saleLane;

        void ShowPendingSales()
        {
            saleTimer -= Time.unscaledDeltaTime;
            if (saleTimer > 0 || pendingSales.Count == 0) return;
            saleTimer = 0.6f;
            if (interiorPlot == null) { pendingSales.Clear(); return; }
            saleLane = (saleLane + 1) % 3;
            var start = new Vector2(-300f + saleLane * 260f, -90f + saleLane * 80f);
            StartCoroutine(Popup(pendingSales.Dequeue(), start, new Color(0.55f, 1f, 0.55f), 30));
        }

        IEnumerator Popup(string text, Vector2 start, Color color, int size)
        {
            var label = UIKit.Label("Satis", popupLayer, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.7f);
            outline.effectDistance = new Vector2(2, -2);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420, 50);
            for (float t = 0; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = start + new Vector2(0, t * 70f);
                label.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(1.6f - t * 1.15f));
                yield return null;
            }
            Destroy(label.gameObject);
        }

        // ------------------------------------------------------------ Dükkan düzenleme

        static readonly ShopOptions.Group[] EditGroups =
        {
            ShopOptions.Group.Floor, ShopOptions.Group.Wall, ShopOptions.Group.Wainscot, ShopOptions.Group.Lamp,
            ShopOptions.Group.Decor, ShopOptions.Group.Sign, ShopOptions.Group.Awning,
        };

        /// <summary>Sağda açılan düzenleme paneli: üstte sekmeler, altta seçenek kartları. Değişiklik anında görünür.</summary>
        void BuildEditPanel()
        {
            editPanel = UIKit.Box("DuzenlePaneli", interiorBar, UIKit.Panel).rectTransform;
            editPanel.anchorMin = new Vector2(1, 0);
            editPanel.anchorMax = new Vector2(1, 1);
            editPanel.pivot = new Vector2(1, 0.5f);
            editPanel.offsetMin = new Vector2(-760, 28);
            editPanel.offsetMax = new Vector2(-28, -160);
            UIKit.Label("Baslik", editPanel, Loc.T("edit.title"), 38, Color.white, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(560, 50));
            var close = UIKit.Button("Kapat", editPanel, "Tamam", UIKit.Green, () => { editPanel.gameObject.SetActive(false); game.ShiftInteriorView(false); }, 40);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(150, 64));
            editTabs = UIKit.Rect("Sekmeler", editPanel);
            editTabs.anchorMin = new Vector2(0, 1);
            editTabs.anchorMax = new Vector2(1, 1);
            editTabs.pivot = new Vector2(0.5f, 1);
            editTabs.offsetMin = new Vector2(16, -214);
            editTabs.offsetMax = new Vector2(-16, -84);
            editGrid = UIKit.Rect("Secenekler", editPanel);
            editGrid.anchorMin = Vector2.zero;
            editGrid.anchorMax = Vector2.one;
            editGrid.offsetMin = new Vector2(16, 64);
            editGrid.offsetMax = new Vector2(-16, -226);
            editHint = UIKit.Label("Ipucu", editPanel, "", 22, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            editHint.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 14), new Vector2(680, 40));
            editPanel.gameObject.SetActive(false);
        }

        void OpenEditPanel()
        {
            if (interiorBusiness == null) return;
            productPanel.gameObject.SetActive(false);
            editPanel.gameObject.SetActive(true);
            game.ShiftInteriorView(true);
            RefreshEditPanel();
        }

        void RefreshEditPanel()
        {
            if (editPanel == null || !editPanel.gameObject.activeSelf || interiorBusiness == null) return;
            for (int i = editTabs.childCount - 1; i >= 0; i--) Destroy(editTabs.GetChild(i).gameObject);
            for (int i = editGrid.childCount - 1; i >= 0; i--) Destroy(editGrid.GetChild(i).gameObject);
            editTabs.DetachChildren();
            editGrid.DetachChildren();

            // Sekmeler: iki sıra (4 + 3).
            for (int i = 0; i < EditGroups.Length; i++)
            {
                var g = EditGroups[i];
                bool on = g == editGroup;
                var tab = UIKit.Button("Sekme", editTabs, Loc.T("edit.tab." + g), on ? UIKit.Accent : new Color(1, 1, 1, 0.12f), () => { editGroup = g; RefreshEditPanel(); }, 24);
                if (on) tab.GetComponentInChildren<Text>().color = UIKit.TextDark;
                int row = i / 4, col = i % 4;
                ((RectTransform)tab.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 178, -row * 66), new Vector2(170, 58));
            }

            var b = interiorBusiness;
            var cards = new List<(string name, Color color, Color color2, string state, bool selected, bool affordable, UnityEngine.Events.UnityAction click)>();
            if (editGroup == ShopOptions.Group.Decor)
            {
                bool food = System.Array.IndexOf(ShopOptions.FoodShops, b.businessId) >= 0;
                foreach (var d in ShopOptions.Decor)
                {
                    if (d.foodOnly && !food) continue;
                    bool has = b.look.Has(d.id);
                    long price = eco.DecorPrice(b, d.id);
                    string state = has ? Loc.T("edit.on") : price > 0 ? Loc.Money(price) : Loc.T("edit.off");
                    string id = d.id;
                    cards.Add((d.name, new Color(0.55f, 0.45f, 0.35f), Color.white, state, has, has || eco.profile.cash >= price, () => Pick(() => eco.ToggleDecor(interiorPlot.id, id))));
                }
                editHint.text = Loc.T("edit.hint.decor");
            }
            else
            {
                var group = editGroup;
                int current = b.look.Get(group);
                // İlk kart: varsayılan (tentede "yok").
                cards.Add((Loc.T(group == ShopOptions.Group.Awning ? "edit.none" : "edit.default"), new Color(0.4f, 0.4f, 0.42f), Color.white,
                    current == -1 ? Loc.T("edit.selected") : Loc.T("edit.free"), current == -1, true, () => Pick(() => eco.ChooseLook(interiorPlot.id, group, -1))));
                var opts = ShopOptions.Options(group);
                for (int i = 0; i < opts.Length; i++)
                {
                    int index = i;
                    long price = eco.LookPrice(b, group, i);
                    string state = current == i ? Loc.T("edit.selected") : price == 0 ? Loc.T("edit.bought") : Loc.Money(price);
                    cards.Add((opts[i].name, opts[i].color, opts[i].color2, state, current == i, eco.profile.cash >= price, () => Pick(() => eco.ChooseLook(interiorPlot.id, group, index))));
                }
                editHint.text = Loc.T("edit.hint");
            }

            const float cw = 164, ch = 150, gap = 10;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                int col = i % 4, row = i / 4;
                var card = UIKit.Button("Kart", editGrid, "", c.selected ? new Color(0.98f, 0.73f, 0.16f, 0.9f) : new Color(1, 1, 1, 0.1f), c.click, 20);
                ((RectTransform)card.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * (cw + gap), -row * (ch + gap)), new Vector2(cw, ch));
                var sw = UIKit.Box("Renk", card.transform, c.color).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(cw - 20, 58));
                if (editGroup == ShopOptions.Group.Sign || editGroup == ShopOptions.Group.Awning)
                    UIKit.Box("Renk2", sw, c.color2).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(editGroup == ShopOptions.Group.Sign ? 70 : 24, editGroup == ShopOptions.Group.Sign ? 14 : 58));
                var name = UIKit.Label("Ad", card.transform, c.name, 22, c.selected ? UIKit.TextDark : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(cw - 8, 32));
                var state = UIKit.Label("Durum", card.transform, c.state, 20, c.selected ? UIKit.TextDark : c.affordable ? new Color(0.6f, 0.95f, 0.6f) : new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter);
                state.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(cw - 8, 30));
            }
        }

        void Pick(System.Func<bool> action)
        {
            if (interiorPlot == null) return;
            if (!action()) Toast(Loc.T("edit.nocash"));
            RefreshEditPanel();
        }

        // ------------------------------------------------------------ İstatistik

        static readonly Color ChartInk = new Color(0.16f, 0.42f, 0.72f);
        static readonly Color ChartGrid = new Color(0, 0, 0, 0.08f);
        static readonly Color TextMuted = new Color(0.42f, 0.4f, 0.37f);

        /// <summary>İstatistik penceresi: dört özet kutusu, servet çizgi grafiği, işletme başına saatlik gelir çubukları.</summary>
        public void ShowStats()
        {
            OpenModal(new Vector2(1560, 900), out var content);
            var p = eco.profile;
            UIKit.Label("Baslik", content, Loc.T("stats.title"), 44, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 60));

            // Özet kutuları.
            var tiles = new (string label, string value, string sub)[]
            {
                (Loc.T("stats.worth"), Loc.Money(eco.NetWorth()), Loc.T("stats.worth.sub")),
                (Loc.T("stats.hourly"), Loc.Money(eco.TotalPerHour()), Loc.T("stats.hourly.sub", p.businesses.Count)),
                (Loc.T("stats.earned"), Loc.Money(p.stats.totalEarned), Loc.T("stats.earned.sub", p.stats.collects)),
                (Loc.T("stats.spent"), Loc.Money(p.stats.totalSpent), Loc.T("stats.spent.sub")),
            };
            float tw = (1504 - 3 * 16) / 4f;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = UIKit.Box("Kutu", content, new Color(1, 1, 1, 0.75f)).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * (tw + 16), -76), new Vector2(tw, 150));
                UIKit.Label("Ad", tile, tiles[i].label, 24, TextMuted, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(tw - 40, 32));
                UIKit.Label("Deger", tile, tiles[i].value, 46, UIKit.TextDark, TextAnchor.MiddleLeft, FontStyle.Bold).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -50), new Vector2(tw - 40, 56));
                UIKit.Label("Alt", tile, tiles[i].sub, 20, TextMuted, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -110), new Vector2(tw - 40, 30));
            }

            // Sol: servet geçmişi (tek seri: başlık adını söyler, gösterge kutusu gerekmez).
            var left = UIKit.Box("ServetGrafigi", content, new Color(1, 1, 1, 0.75f)).rectTransform;
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 0); left.pivot = Vector2.zero;
            left.anchoredPosition = Vector2.zero; left.sizeDelta = new Vector2(820, 580);
            UIKit.Label("Baslik", left, Loc.T("stats.chart.worth"), 28, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(760, 40));
            LineChart(left, p.stats.sampleTicks, p.stats.sampleWorth, new Rect(110, 60, 670, 420));

            // Sağ: işletme başına saatlik gelir (büyükten küçüğe yatay çubuklar).
            var right = UIKit.Box("GelirGrafigi", content, new Color(1, 1, 1, 0.75f)).rectTransform;
            right.anchorMin = new Vector2(1, 0); right.anchorMax = new Vector2(1, 0); right.pivot = new Vector2(1, 0);
            right.anchoredPosition = Vector2.zero; right.sizeDelta = new Vector2(668, 580);
            UIKit.Label("Baslik", right, Loc.T("stats.chart.income"), 28, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(620, 40));
            BarChart(right);
        }

        void LineChart(RectTransform parent, List<long> ticks, List<double> values, Rect area)
        {
            int n = values.Count;
            if (n < 2)
            {
                UIKit.Label("Bos", parent, Loc.T("stats.chart.empty"), 24, TextMuted, TextAnchor.MiddleCenter).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 120));
                return;
            }
            double min = double.MaxValue, max = double.MinValue;
            foreach (var v in values) { min = System.Math.Min(min, v); max = System.Math.Max(max, v); }
            // Eksen sıfırdan değil, verinin çevresinden başlar (servet hiç sıfıra inmez); biraz pay bırakılır.
            double pad = System.Math.Max(1, (max - min) * 0.15);
            min = System.Math.Max(0, min - pad); max += pad;
            Vector2 Pt(int i) => new Vector2(area.x + area.width * i / (n - 1f), area.y + (float)((values[i] - min) / (max - min)) * area.height);
            // Silik yatay ızgara ve değer etiketleri.
            for (int g = 0; g <= 3; g++)
            {
                float y = area.y + area.height * g / 3f;
                var line = UIKit.Box("Izgara", parent, ChartGrid, UIKit.Plain).rectTransform;
                line.anchorMin = line.anchorMax = Vector2.zero; line.pivot = new Vector2(0, 0.5f);
                line.anchoredPosition = new Vector2(area.x, y); line.sizeDelta = new Vector2(area.width, 2);
                UIKit.Label("Eksen", parent, Loc.Money(min + (max - min) * g / 3.0), 18, TextMuted, TextAnchor.MiddleRight)
                    .rectTransform.Place(Vector2.zero, new Vector2(1, 0.5f), new Vector2(area.x - 12, y), new Vector2(100, 24));
            }
            // Zaman ekseni: ilk ve son örnek.
            string When(long t) => new System.DateTime(t, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm");
            UIKit.Label("Bas", parent, When(ticks[0]), 18, TextMuted, TextAnchor.UpperLeft).rectTransform.Place(Vector2.zero, new Vector2(0, 1), new Vector2(area.x, area.y - 8), new Vector2(120, 24));
            UIKit.Label("Son", parent, When(ticks[n - 1]), 18, TextMuted, TextAnchor.UpperRight).rectTransform.Place(Vector2.zero, new Vector2(1, 1), new Vector2(area.xMax, area.y - 8), new Vector2(120, 24));
            // Çizgi: ince parçalar.
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 a = Pt(i), b = Pt(i + 1);
                var seg = UIKit.Box("Cizgi", parent, ChartInk, UIKit.Plain).rectTransform;
                seg.anchorMin = seg.anchorMax = Vector2.zero; seg.pivot = new Vector2(0, 0.5f);
                seg.anchoredPosition = a;
                seg.sizeDelta = new Vector2(Vector2.Distance(a, b) + 1.5f, 4);
                seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }
            // Son nokta işaretli ve değeri yazılı (yalnızca son nokta).
            var last = Pt(n - 1);
            var dot = UIKit.Box("Nokta", parent, ChartInk, UIKit.Circle).rectTransform;
            dot.anchorMin = dot.anchorMax = Vector2.zero; dot.pivot = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = last; dot.sizeDelta = new Vector2(14, 14);
            UIKit.Label("SonDeger", parent, Loc.Money(values[n - 1]), 22, UIKit.TextDark, TextAnchor.LowerRight, FontStyle.Bold)
                .rectTransform.Place(Vector2.zero, new Vector2(1, 0), last + new Vector2(-6, 12), new Vector2(240, 30));
        }

        void BarChart(RectTransform parent)
        {
            var rows = new List<(string name, double value)>();
            foreach (var b in eco.profile.businesses)
                rows.Add((Loc.T("biz.title", Loc.T("business." + b.businessId), b.level), eco.NetPerHour(b)));
            if (rows.Count == 0)
            {
                UIKit.Label("Bos", parent, Loc.T("stats.bars.empty"), 24, TextMuted, TextAnchor.MiddleCenter).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 120));
                return;
            }
            rows.Sort((x, y) => y.value.CompareTo(x.value));
            int count = Mathf.Min(rows.Count, 8);
            double max = rows[0].value <= 0 ? 1 : rows[0].value;
            const float top = 78, rowH = 58, labelW = 250, barMax = 300;
            for (int i = 0; i < count; i++)
            {
                float y = -top - i * rowH;
                UIKit.Label("Ad", parent, rows[i].name, 20, UIKit.TextDark, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(24, y - rowH * 0.5f), new Vector2(labelW, 40));
                float w = Mathf.Max(6, (float)(rows[i].value / max) * barMax);
                UIKit.Box("Cubuk", parent, ChartInk).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(24 + labelW, y - rowH * 0.5f), new Vector2(w, 26));
                UIKit.Label("Deger", parent, Loc.T("stats.perhour", Loc.Money(rows[i].value)), 20, TextMuted, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(24 + labelW + w + 10, y - rowH * 0.5f), new Vector2(150, 40));
            }
            if (rows.Count > count)
                UIKit.Label("Diger", parent, Loc.T("stats.more", rows.Count - count), 20, TextMuted, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(24, -top - count * rowH - 20), new Vector2(500, 40));
        }

        // ------------------------------------------------------------ Görevler

        public static string MissionTitle(MissionDef d)
        {
            string special = "mission.id." + d.id;
            if (Loc.T(special) != special) return Loc.T(special);
            string amount = d.kind == MissionKind.CollectCash ? Loc.Money(d.target) : d.target.ToString();
            string business = string.IsNullOrEmpty(d.param) ? "" : Loc.T("business." + d.param);
            return Loc.T("mission." + d.kind, amount, business);
        }

        /// <summary>Görev listesini yeniden çizer (ilerleme ya da görev değişince).</summary>
        void RefreshMissions()
        {
            if (missions == null || missionPanel == null) return;
            for (int i = missionList.childCount - 1; i >= 0; i--) Destroy(missionList.GetChild(i).gameObject);
            missionList.DetachChildren();
            int ready = 0;
            foreach (var p in missions.Active) if (missions.IsDone(p)) ready++;
            missionHeader.text = (missionsCollapsed ? "▶  " : "▼  ") + Loc.T("mission.header") + (ready > 0 ? $"  ({ready}!)" : "");
            if (missionsCollapsed || missions.Active.Count == 0)
            {
                missionPanel.sizeDelta = new Vector2(470, 56);
                return;
            }
            const float rowH = 104, gap = 8;
            float y = 0;
            foreach (var p in missions.Active)
            {
                var d = missions.Def(p.id);
                if (d == null) continue;
                bool done = missions.IsDone(p);
                var row = UIKit.Box("Gorev", missionList, done ? new Color(0.3f, 0.72f, 0.36f, 0.25f) : new Color(1, 1, 1, 0.08f)).rectTransform;
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.anchoredPosition = new Vector2(0, -y);
                row.sizeDelta = new Vector2(0, rowH);
                UIKit.Label("Ad", row, MissionTitle(d), 26, Color.white, TextAnchor.UpperLeft, FontStyle.Bold)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(420, 34));
                string reward = d.rewardXp > 0 ? Loc.T("mission.rewardXp", Loc.Money(d.rewardCash), d.rewardXp) : Loc.T("mission.reward", Loc.Money(d.rewardCash));
                if (done)
                {
                    UIKit.Label("Odul", row, reward, 22, new Color(1, 1, 1, 0.85f), TextAnchor.LowerLeft)
                        .rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 12), new Vector2(240, 34));
                    string id = p.id;
                    var claim = UIKit.Button("OdulAl", row, Loc.T("mission.claim"), UIKit.Green, () => missions.Claim(id), 24);
                    ((RectTransform)claim.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(170, 52));
                }
                else
                {
                    long v = missions.Progress(p);
                    var bar = UIKit.Box("Cubuk", row, new Color(1, 1, 1, 0.15f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 14), new Vector2(250, 26));
                    var fill = UIKit.Box("Dolu", bar, UIKit.Accent).rectTransform;
                    fill.anchorMin = Vector2.zero;
                    fill.anchorMax = new Vector2(Mathf.Clamp01(v / (float)Mathf.Max(1, d.target)), 1);
                    fill.offsetMin = fill.offsetMax = Vector2.zero;
                    string progress = d.kind == MissionKind.CollectCash ? $"{Loc.Money(v)} / {Loc.Money(d.target)}" : $"{v} / {d.target}";
                    UIKit.Label("Sayi", bar, progress, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
                    UIKit.Label("Odul", row, reward, 20, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleRight)
                        .rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 12), new Vector2(170, 30));
                }
                y += rowH + gap;
            }
            missionPanel.sizeDelta = new Vector2(470, 56 + y + 10 - gap);
        }

        // ------------------------------------------------------------ Bildirim

        public void Toast(string message)
        {
            // İç görünümde alttaki düğmelerin üstünde dursun.
            ((RectTransform)toastGroup.transform).anchoredPosition = new Vector2(0, GuideActive ? (interiorPlot != null ? 390 : 270) : interiorPlot != null ? 150 : 40);
            toastLabel.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.15f; yield return null; }
            toastGroup.alpha = 1;
            yield return new WaitForSecondsRealtime(2.2f);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1 - t / 0.4f; yield return null; }
            toastGroup.alpha = 0;
        }

        // ------------------------------------------------------------ Pencereler

        RectTransform OpenModal(Vector2 size, out RectTransform content)
        {
            ClearModal();
            var dim = UIKit.Box("Karartma", modalLayer, new Color(0, 0, 0, 0.45f), UIKit.Rounded).rectTransform.Stretch(-20);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseModal);
            var panel = UIKit.Box("Pencere", modalLayer, UIKit.PanelLight).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var close = UIKit.Button("Kapat", panel, "×", new Color(0.8f, 0.3f, 0.25f), CloseModal, 44);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(68, 68));
            content = UIKit.Rect("Icerik", panel).Stretch(28);
            return panel;
        }

        public void CloseModal()
        {
            ClearModal();
            if (pendingModals.Count > 0) pendingModals.Dequeue()();
        }

        void ClearModal()
        {
            for (int i = modalLayer.childCount - 1; i >= 0; i--) Destroy(modalLayer.GetChild(i).gameObject);
            modalLayer.DetachChildren();
        }

        void QueueModal(System.Action show)
        {
            if (IsModalOpen) pendingModals.Enqueue(show);
            else show();
        }

        public void ShowBuyPanel(Plot plot)
        {
            OpenModal(new Vector2(1100, 840), out var content);
            var title = UIKit.Label("Baslik", content, Loc.T("buy.title"), 44, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 60));
            var land = UIKit.Label("Arsa", content, Loc.T("buy.land", Loc.Money(eco.LandPrice(plot))), 26, new Color(0.35f, 0.33f, 0.3f), TextAnchor.UpperLeft);
            land.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(900, 40));

            // Kaydırılabilir liste.
            var scroll = UIKit.Rect("Liste", content);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = Vector2.zero;
            scroll.offsetMax = new Vector2(0, -110);
            var viewport = UIKit.Rect("Gorunum", scroll).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var listContent = UIKit.Rect("Satirlar", viewport);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.offsetMin = listContent.offsetMax = Vector2.zero;
            var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = listContent;
            sr.horizontal = false;
            sr.scrollSensitivity = 30;

            foreach (var def in eco.catalog.Sorted())
            {
                var check = eco.CanBuild(def, plot);
                bool locked = check == CityEconomy.BuildCheck.LevelTooLow || check == CityEconomy.BuildCheck.StageLocked;
                var row = UIKit.Box("Satir", listContent, locked ? new Color(0.88f, 0.86f, 0.82f) : Color.white);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
                var name = UIKit.Label("Ad", row.transform, Loc.T("business." + def.id), 36, locked ? UIKit.Grey : UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(560, 46));
                var info = UIKit.Label("Bilgi", row.transform, Loc.T("buy.row", Loc.Money(eco.TotalPrice(def, plot)), Loc.Money(def.NetPerHour)), 26, UIKit.Grey, TextAnchor.UpperLeft);
                info.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -62), new Vector2(560, 40));

                string text;
                Color color;
                switch (check)
                {
                    case CityEconomy.BuildCheck.Ok: text = Loc.T("buy.button", Loc.Money(eco.TotalPrice(def, plot))); color = UIKit.Green; break;
                    case CityEconomy.BuildCheck.NotEnoughCash: text = Loc.T("buy.nocash", Loc.Money(eco.TotalPrice(def, plot))); color = UIKit.Grey; break;
                    case CityEconomy.BuildCheck.StageLocked: text = Loc.T("buy.stage", Loc.Stage(def.requiredStage)); color = UIKit.Grey; break;
                    case CityEconomy.BuildCheck.PlotTooSmall: text = Loc.T("buy.small"); color = UIKit.Grey; break;
                    default: text = Loc.T("buy.locked", def.requiredLevel); color = UIKit.Grey; break;
                }
                var d = def;
                var btn = UIKit.Button("Kur", row.transform, text, color, () =>
                {
                    if (eco.Build(d, plot)) CloseModal();
                }, 26);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(380, 80));
                btn.interactable = check == CityEconomy.BuildCheck.Ok;
            }
        }

        public void ShowBusinessPanel(Plot plot, OwnedBusiness owned)
        {
            var def = eco.catalog.Find(owned.businessId);
            if (def == null) return;
            OpenModal(new Vector2(860, 520), out var content);
            UIKit.Label("Baslik", content, Loc.T("biz.title", Loc.T("business." + def.id), owned.level), 44, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(720, 60));
            UIKit.Label("Gelir", content, Loc.T("biz.income", Loc.Money(eco.NetPerHour(owned))), 30, UIKit.TextDark, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(780, 44));
            UIKit.Label("Kasa", content, Loc.T("biz.stored", Loc.Money(owned.stored), Loc.Money(eco.Capacity(owned))), 30, UIKit.TextDark, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -140), new Vector2(780, 44));

            var collect = UIKit.Button("Topla", content, Loc.T("biz.collect"), UIKit.Accent, () => { eco.Collect(plot.id); CloseModal(); }, 32);
            ((RectTransform)collect.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(320, 96));
            collect.interactable = owned.stored >= 1;

            bool max = owned.level >= def.maxLevel;
            var upgrade = UIKit.Button("Yukselt", content, max ? Loc.T("biz.max") : Loc.T("biz.upgrade", Loc.Money(eco.UpgradeCost(owned))), UIKit.Green, () =>
            {
                if (eco.Upgrade(plot.id))
                {
                    Toast(Loc.T("biz.upgraded", Loc.T("business." + def.id), owned.level));
                    CloseModal();
                }
            }, 30);
            ((RectTransform)upgrade.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(420, 96));
            upgrade.interactable = !max && eco.CanUpgrade(owned);
        }

        void ShowLevelUp(int level, List<BusinessDefinition> unlocked)
        {
            OpenModal(new Vector2(900, 560), out var content);
            UIKit.Label("Baslik", content, Loc.T("level.title", level), 58, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
            UIKit.Label("Odul", content, Loc.T("level.reward", Loc.Money(level * eco.config.levelUpCashPerLevel)), 34, UIKit.Green, TextAnchor.MiddleCenter, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(800, 50));
            if (unlocked.Count > 0)
            {
                var names = new List<string>();
                foreach (var d in unlocked) names.Add(Loc.T("business." + d.id));
                UIKit.Label("Yeni", content, Loc.T("level.unlocked", string.Join(", ", names)), 32, UIKit.TextDark, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(800, 100));
            }
            var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
        }

        void ShowStage(CityStage stage)
        {
            OpenModal(new Vector2(900, 460), out var content);
            UIKit.Label("Baslik", content, Loc.T("level.stage", Loc.Stage(stage)), 40, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
            var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
        }

        public void ShowWelcome(double earned)
        {
            QueueModal(() =>
            {
                OpenModal(new Vector2(900, 440), out var content);
                UIKit.Label("Baslik", content, Loc.T("offline.title"), 52, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
                UIKit.Label("Metin", content, Loc.T("offline.body", Loc.Money(earned)), 32, UIKit.TextDark, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(800, 140));
                var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
                ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
            });
        }
    }
}
